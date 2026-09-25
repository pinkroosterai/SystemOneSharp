using System.Text.Json.Nodes;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using SystemOneSharp.Extensions.AI;

namespace SystemOneSharp.AgentFramework;

/// <summary>Function-calling middleware that classifies a proposed tool call with System One before it runs.</summary>
/// <remarks>
/// <para>
/// The gate asks one Choice question over the function's name and description, the arguments it is about to be
/// called with, the recent conversation and optional application policy context. It then hands the classification
/// to <see cref="SystemOneFunctionGateOptions.Policy"/>, which decides what happens. The gate itself attaches no
/// meaning to any category.
/// </para>
/// <para>
/// Classification is a semantic assessment, not authorization. <see cref="SystemOneFunctionGateDecision.Invoke"/>
/// only calls the next middleware, so deterministic authorization checks further down the pipeline still run and
/// cannot be bypassed. Arguments are the model's parsed arguments; they are not schema-validated at this point.
/// If <see cref="ISystemOneClient.DecideAsync"/> throws, the exception propagates and the function is not invoked.
/// Install with <see cref="SystemOneFunctionGateExtensions.UseSystemOneFunctionGate"/>; it requires an agent built on
/// <see cref="FunctionInvokingChatClient"/>, such as <see cref="ChatClientAgent"/>.
/// </para>
/// </remarks>
public sealed class SystemOneFunctionGate
{
    /// <summary>The question ID the gate sends.</summary>
    public const string QuestionId = "classification";

    private readonly ISystemOneClient _client;
    private readonly SystemOneFunctionGateOptions _options;

    public SystemOneFunctionGate(ISystemOneClient client, SystemOneFunctionGateOptions options)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Instructions, nameof(options));
        if (options.Categories is null || options.Policy is null)
            throw new ArgumentException("Categories and Policy are required.", nameof(options));
        if (options.MaxConversationMessages is < 0)
            throw new ArgumentException("MaxConversationMessages must be nonnegative.", nameof(options));
    }

    /// <summary>The middleware callback; matches Agent Framework's function-invocation middleware signature.</summary>
    public async ValueTask<object?> InvokeAsync(AIAgent agent, FunctionInvocationContext context,
        Func<FunctionInvocationContext, CancellationToken, ValueTask<object?>> next, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        var builder = new SystemOneRequestBuilder()
            .WithState(CreateState(context))
            .AddChoice(QuestionId, _options.Instructions, _options.Categories);
        if (_options.Model is not null)
            builder.WithModel(_options.Model);

        var response = await _client.DecideAsync(builder.Build(), cancellationToken).ConfigureAwait(false);
        var classification = new SystemOneFunctionClassification(context, response.GetChoice(QuestionId), response);
        var decision = await _options.Policy(classification, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The function gate policy returned no decision.");

        return decision.ShouldInvoke ? await next(context, cancellationToken).ConfigureAwait(false) : decision.Result;
    }

    private JsonObject CreateState(FunctionInvocationContext context)
    {
        // The proposed call goes through the shared projection so arguments are serialized exactly as in conversations.
        var proposedCall = new FunctionCallContent(context.CallContent.CallId, context.Function.Name, context.Arguments);
        var projectedCall = SystemOneAiState.Create([new ChatMessage(ChatRole.Assistant, [proposedCall])], _options.StateOptions ?? new())
            ["messages"]![0]!["contents"]![0]!["arguments"]!.DeepClone();

        var state = new JsonObject
        {
            ["function"] = new JsonObject
            {
                ["name"] = context.Function.Name,
                ["description"] = string.IsNullOrWhiteSpace(context.Function.Description) ? null : context.Function.Description,
                ["arguments"] = projectedCall
            }
        };

        if (_options.MaxConversationMessages is not 0)
        {
            var messages = _options.MaxConversationMessages is { } max ? context.Messages.TakeLast(max) : context.Messages;
            state["conversation"] = SystemOneAiState.Create(messages, _options.StateOptions ?? new());
        }

        if (_options.PolicyContext?.Invoke(context) is { } policyContext)
            state["policy_context"] = policyContext.DeepClone();

        return state;
    }
}

/// <summary>Configures <see cref="SystemOneFunctionGate"/>.</summary>
public sealed class SystemOneFunctionGateOptions
{
    /// <summary>Gets the Choice question's instructions.</summary>
    public string Instructions { get; init; } = "Classify the proposed function call.";

    /// <summary>Gets the classification categories, for example <c>routine</c>, <c>review</c> and <c>blocked</c>.</summary>
    public required Action<ChoiceQuestionBuilder> Categories { get; init; }

    /// <summary>Gets the application policy that turns a classification into a decision. Review handling belongs here.</summary>
    public required Func<SystemOneFunctionClassification, CancellationToken, ValueTask<SystemOneFunctionGateDecision>> Policy { get; init; }

    /// <summary>Gets optional application policy context added to the state as <c>policy_context</c>.</summary>
    public Func<FunctionInvocationContext, JsonNode?>? PolicyContext { get; init; }

    /// <summary>Gets how many trailing conversation messages to include; <see langword="null"/> for all, 0 for none.</summary>
    public int? MaxConversationMessages { get; init; }

    /// <summary>Gets a model ID that overrides the client's configured model, or <see langword="null"/>.</summary>
    public string? Model { get; init; }

    /// <summary>Gets options for the conversation projection.</summary>
    public SystemOneAiStateOptions? StateOptions { get; init; }
}

/// <summary>The System One classification of a proposed function call, handed to application policy.</summary>
public sealed record SystemOneFunctionClassification(
    FunctionInvocationContext Context,
    ChoiceAnswer Answer,
    SystemOneResponse Response)
{
    /// <summary>Gets the selected category.</summary>
    public string Category => Answer.Choice;
}

/// <summary>What the gate does with a proposed function call.</summary>
public sealed class SystemOneFunctionGateDecision
{
    private SystemOneFunctionGateDecision(bool shouldInvoke, object? result)
    {
        ShouldInvoke = shouldInvoke;
        Result = result;
    }

    /// <summary>Gets whether the next middleware, and eventually the function, runs.</summary>
    public bool ShouldInvoke { get; }

    /// <summary>Gets the result returned to the model in place of the function's result when blocked.</summary>
    public object? Result { get; }

    /// <summary>Calls the next middleware; any authorization after the gate still applies.</summary>
    public static SystemOneFunctionGateDecision Invoke() => new(true, null);

    /// <summary>Skips the function and returns <paramref name="result"/> to the model as its result.</summary>
    public static SystemOneFunctionGateDecision Block(object? result) => new(false, result);
}

/// <summary>Installs <see cref="SystemOneFunctionGate"/> on an agent builder.</summary>
public static class SystemOneFunctionGateExtensions
{
    /// <summary>Adds the gate as function-invocation middleware.</summary>
    public static AIAgentBuilder UseSystemOneFunctionGate(this AIAgentBuilder builder, SystemOneFunctionGate gate)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(gate);
        return builder.Use(gate.InvokeAsync);
    }
}
