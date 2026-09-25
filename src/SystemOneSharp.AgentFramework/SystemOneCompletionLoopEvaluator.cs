using System.Diagnostics.CodeAnalysis;
using Microsoft.Agents.AI;
using SystemOneSharp.Extensions.AI;

namespace SystemOneSharp.AgentFramework;

/// <summary>A <see cref="LoopEvaluator"/> that asks System One whether the agent has completed the request.</summary>
/// <remarks>
/// <para>
/// Each iteration sends one Noul question over the loop's initial messages plus the latest response, projected by
/// <see cref="SystemOneAiState"/>. The completion policy in <see cref="SystemOneCompletionLoopEvaluatorOptions"/> turns the
/// probability into <see cref="LoopEvaluation.Stop"/> or <see cref="LoopEvaluation.Continue(string)"/>. The hard iteration
/// cap stays with <c>LoopAgentOptions.MaxIterations</c>; this evaluator adds none. It produces no generated feedback.
/// </para>
/// <para>
/// The evaluator holds no per-run state and is safe to share across concurrent loops. Exceptions from
/// <see cref="ISystemOneClient.DecideAsync"/> propagate unchanged. Marked experimental with Agent Framework's own
/// diagnostic ID because <see cref="LoopEvaluator"/> is experimental: code that already opts into <c>LoopAgent</c>
/// needs no second suppression.
/// </para>
/// </remarks>
[Experimental("MAAI001")]
public sealed class SystemOneCompletionLoopEvaluator : LoopEvaluator
{
    /// <summary>The question ID the evaluator sends.</summary>
    public const string QuestionId = "completed";

    private readonly ISystemOneClient _client;
    private readonly SystemOneCompletionLoopEvaluatorOptions _options;

    public SystemOneCompletionLoopEvaluator(ISystemOneClient client, SystemOneCompletionLoopEvaluatorOptions options)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        if (options.CompletionThreshold is null == options.IsComplete is null)
            throw new ArgumentException("Set exactly one of CompletionThreshold or IsComplete.", nameof(options));
        if (options.CompletionThreshold is { } threshold && !(threshold is >= 0 and <= 1))
            throw new ArgumentException("CompletionThreshold must be between 0 and 1.", nameof(options));
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Instructions, nameof(options));
    }

    public override async ValueTask<LoopEvaluation> EvaluateAsync(LoopContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var builder = new SystemOneRequestBuilder()
            .WithConversation(context.InitialMessages.Concat(context.LastResponse.Messages), _options.StateOptions)
            .AddNoul(QuestionId, _options.Instructions);
        if (_options.Model is not null)
            builder.WithModel(_options.Model);

        var response = await _client.DecideAsync(builder.Build(), cancellationToken).ConfigureAwait(false);
        var probability = response.GetNoul(QuestionId).Noul;
        var complete = _options.IsComplete?.Invoke(probability) ?? probability >= _options.CompletionThreshold!.Value;
        return complete ? LoopEvaluation.Stop() : LoopEvaluation.Continue(_options.ContinueFeedback);
    }
}

/// <summary>Configures <see cref="SystemOneCompletionLoopEvaluator"/>. The completion policy has no default on purpose.</summary>
public sealed class SystemOneCompletionLoopEvaluatorOptions
{
    /// <summary>Gets the Noul probability at or above which the loop stops. Set this or <see cref="IsComplete"/>, not both.</summary>
    public double? CompletionThreshold { get; init; }

    /// <summary>Gets a policy that decides completion from the Noul probability. Set this or <see cref="CompletionThreshold"/>, not both.</summary>
    public Func<double, bool>? IsComplete { get; init; }

    /// <summary>Gets the Noul question's instructions.</summary>
    public string Instructions { get; init; } = "Has the assistant fully completed the user's request, with nothing left to do?";

    /// <summary>Gets fixed feedback carried into the next iteration when the loop continues, or <see langword="null"/> for none.</summary>
    public string? ContinueFeedback { get; init; }

    /// <summary>Gets a model ID that overrides the client's configured model, or <see langword="null"/>.</summary>
    public string? Model { get; init; }

    /// <summary>Gets options for the conversation projection.</summary>
    public SystemOneAiStateOptions? StateOptions { get; init; }
}
