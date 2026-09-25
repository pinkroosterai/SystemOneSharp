using System.Net;
using System.Text.Json.Nodes;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using SystemOneSharp.AgentFramework;
using SystemOneSharp.Testing;
using static SystemOneSharp.Testing.Verify;

namespace SystemOneSharp.Integrations.Verification;

internal static class AgentFrameworkChecks
{
    public static async Task RunAsync()
    {
        await LoopEvaluatorChecksAsync();
        await FunctionGateChecksAsync();
    }

    private static async Task LoopEvaluatorChecksAsync()
    {
        var chat = new ScriptedChatClient(_ => new ChatResponse(new ChatMessage(ChatRole.Assistant, "Working on it.")));
        AIAgent inner = new ChatClientAgent(chat);
        var session = await inner.CreateSessionAsync();
        var initial = new List<ChatMessage> { new(ChatRole.User, "Write the report.") };
        LoopContext Context(string latest) => new(inner, session, initial, new AgentResponse(new ChatMessage(ChatRole.Assistant, latest)), null);

        var low = new FakeSystemOneClient(SystemOneResponses.AllNoul(0.3));
        var evaluation = await new SystemOneCompletionLoopEvaluator(low, new() { CompletionThreshold = 0.95, ContinueFeedback = "Keep going." })
            .EvaluateAsync(Context("Half done."));
        Check(evaluation.ShouldReinvoke && evaluation.Feedback == "Keep going.", "completion probability below threshold → Continue()");
        Check(low.Requests.Single().Questions[SystemOneCompletionLoopEvaluator.QuestionId] is NoulQuestion &&
            low.Requests[0].State.ToJsonString() == initial.Append(new ChatMessage(ChatRole.Assistant, "Half done.")).ToSystemOneStateJson(),
            "LoopContext → System One completion request over the shared projection");

        var high = new FakeSystemOneClient(SystemOneResponses.AllNoul(0.97));
        Check(!(await new SystemOneCompletionLoopEvaluator(high, new() { CompletionThreshold = 0.95 }).EvaluateAsync(Context("Done."))).ShouldReinvoke,
            "completion probability above threshold → Stop()");
        Check(!(await new SystemOneCompletionLoopEvaluator(low, new() { IsComplete = probability => probability > 0.2 }).EvaluateAsync(Context("Done."))).ShouldReinvoke,
            "completion policy delegate decides instead of a threshold");
        Throws<ArgumentException>(() => new SystemOneCompletionLoopEvaluator(low, new()), "loop evaluator requires an explicit completion policy");
        Throws<ArgumentException>(() => new SystemOneCompletionLoopEvaluator(low, new() { CompletionThreshold = 0.9, IsComplete = _ => true }),
            "loop evaluator rejects two completion policies");

        var probabilities = new Queue<double>([0.2, 0.99]);
        var sequenced = new FakeSystemOneClient(request => SystemOneResponses.AllNoul(probabilities.Dequeue())(request));
        var loop = new LoopAgent(inner, new SystemOneCompletionLoopEvaluator(sequenced, new() { CompletionThreshold = 0.95 }),
            new LoopAgentOptions { MaxIterations = 5 });
        var chatCallsBefore = chat.Calls.Count;
        await loop.RunAsync("Write the report.");
        Check(chat.Calls.Count - chatCallsBefore == 2 && sequenced.Requests.Count == 2, "LoopAgent re-invokes until System One reports completion");

        using var source = new CancellationTokenSource();
        source.Cancel();
        var cancelling = new FakeSystemOneClient(SystemOneResponses.AllNoul(0.9));
        await ThrowsAsync<OperationCanceledException>(async () => await new SystemOneCompletionLoopEvaluator(cancelling, new() { CompletionThreshold = 0.9 })
            .EvaluateAsync(Context("Done."), source.Token), "loop evaluator cancellation throws");
        Check(cancelling.Tokens.Single() == source.Token, "loop evaluator cancellation token → propagated to ISystemOneClient");
        var apiError = new SystemOneApiException(HttpStatusCode.ServiceUnavailable, "down", 1);
        var thrown = await ThrowsAsync<SystemOneApiException>(async () => await new SystemOneCompletionLoopEvaluator(
            new FakeSystemOneClient(apiError), new() { CompletionThreshold = 0.9 }).EvaluateAsync(Context("Done.")),
            "loop evaluator SystemOneException propagates");
        Check(ReferenceEquals(thrown, apiError), "loop evaluator preserves existing failure semantics");
    }

    private static async Task FunctionGateChecksAsync()
    {
        var deletions = 0;
        var deleteFile = AIFunctionFactory.Create((string path) => { deletions++; return $"Deleted {path}"; }, "delete_file", "Delete a file from the workspace.");

        async Task<(ScriptedChatClient Chat, FakeSystemOneClient SystemOne)> RunAgentAsync(string category, Func<SystemOneFunctionClassification, SystemOneFunctionGateDecision> policy)
        {
            var chat = new ScriptedChatClient(messages => messages.Any(message => message.Contents.OfType<FunctionResultContent>().Any())
                ? new ChatResponse(new ChatMessage(ChatRole.Assistant, "Finished."))
                : new ChatResponse(new ChatMessage(ChatRole.Assistant,
                    [new FunctionCallContent("call-1", "delete_file", new Dictionary<string, object?> { ["path"] = "notes.txt" })])));
            var systemOne = new FakeSystemOneClient(SystemOneResponses.ById(new Dictionary<string, SystemOneAnswer>
            {
                [SystemOneFunctionGate.QuestionId] = SystemOneAnswers.Choice(new Dictionary<string, double>
                {
                    ["routine"] = category == "routine" ? 0.9 : 0.05,
                    ["review"] = 0.05,
                    ["blocked"] = category == "blocked" ? 0.9 : 0.05
                })
            }));
            var gate = new SystemOneFunctionGate(systemOne, new()
            {
                Categories = choice => choice
                    .Option("routine", "Safe, expected action")
                    .Option("review", "Needs a person to look")
                    .Option("blocked", "Must not run"),
                Policy = (classification, _) => ValueTask.FromResult(policy(classification)),
                PolicyContext = _ => new JsonObject { ["workspace"] = "sandbox" }
            });
            var agent = new ChatClientAgent(chat, tools: [deleteFile]).AsBuilder().UseSystemOneFunctionGate(gate).Build();
            await agent.RunAsync("Clean up my notes.");
            return (chat, systemOne);
        }

        var allowPolicy = (SystemOneFunctionClassification classification) => classification.Category == "routine"
            ? SystemOneFunctionGateDecision.Invoke()
            : SystemOneFunctionGateDecision.Block("Blocked by policy.");

        var (allowChat, allowSystemOne) = await RunAgentAsync("routine", allowPolicy);
        var state = allowSystemOne.Requests.Single().State;
        Check(state["function"]!["name"]!.GetValue<string>() == "delete_file" &&
            state["function"]!["description"]!.GetValue<string>() == "Delete a file from the workspace." &&
            state["function"]!["arguments"]!["path"]!.GetValue<string>() == "notes.txt" &&
            state["conversation"]!["messages"]!.AsArray().Count > 0 &&
            state["policy_context"]!["workspace"]!.GetValue<string>() == "sandbox" &&
            allowSystemOne.Requests[0].Questions[SystemOneFunctionGate.QuestionId] is ChoiceQuestion,
            "proposed MAF function call → gate request");
        Check(deletions == 1 && FunctionResults(allowChat).Single() == "Deleted notes.txt", "allow disposition → function executes");

        var (blockChat, _) = await RunAgentAsync("blocked", allowPolicy);
        Check(deletions == 1 && FunctionResults(blockChat).Single() == "Blocked by policy.",
            "block disposition → function does not execute and the model sees the block result");

        var context = new FunctionInvocationContext
        {
            Function = deleteFile,
            Arguments = new AIFunctionArguments { ["path"] = "notes.txt" },
            CallContent = new FunctionCallContent("call-2", "delete_file"),
            Messages = [new ChatMessage(ChatRole.User, "Clean up.")]
        };
        var nextCalls = 0;
        ValueTask<object?> Next(FunctionInvocationContext _, CancellationToken __) { nextCalls++; return ValueTask.FromResult<object?>("ran"); }
        AIAgent anyAgent = new ChatClientAgent(new ScriptedChatClient(_ => new ChatResponse()));

        using var source = new CancellationTokenSource();
        source.Cancel();
        var cancelling = new FakeSystemOneClient(SystemOneResponses.AllNoul(0.9));
        var cancellingGate = new SystemOneFunctionGate(cancelling, new()
        {
            Categories = choice => choice.Option("routine", null).Option("blocked", null),
            Policy = (_, _) => ValueTask.FromResult(SystemOneFunctionGateDecision.Invoke())
        });
        await ThrowsAsync<OperationCanceledException>(async () => await cancellingGate.InvokeAsync(anyAgent, context, Next, source.Token),
            "gate cancellation throws");
        Check(cancelling.Tokens.Single() == source.Token && nextCalls == 0, "gate cancellation token → propagated to ISystemOneClient");

        var apiError = new SystemOneApiException(HttpStatusCode.ServiceUnavailable, "down", 1);
        var failingGate = new SystemOneFunctionGate(new FakeSystemOneClient(apiError), new()
        {
            Categories = choice => choice.Option("routine", null).Option("blocked", null),
            Policy = (_, _) => ValueTask.FromResult(SystemOneFunctionGateDecision.Invoke())
        });
        var thrown = await ThrowsAsync<SystemOneApiException>(async () => await failingGate.InvokeAsync(anyAgent, context, Next, CancellationToken.None),
            "gate SystemOneException propagates");
        Check(ReferenceEquals(thrown, apiError) && nextCalls == 0, "gate failure preserves exception and does not invoke the function");
    }

    private static IEnumerable<string?> FunctionResults(ScriptedChatClient chat) =>
        chat.Calls.SelectMany(call => call).SelectMany(message => message.Contents).OfType<FunctionResultContent>()
            .Select(result => result.Result?.ToString()).Distinct();

    private static string ToSystemOneStateJson(this IEnumerable<ChatMessage> messages) =>
        SystemOneSharp.Extensions.AI.SystemOneAiStateExtensions.ToSystemOneState(messages).ToJsonString();
}

/// <summary>An <see cref="IChatClient"/> that answers from a script and records every call's messages.</summary>
internal sealed class ScriptedChatClient(Func<IReadOnlyList<ChatMessage>, ChatResponse> respond) : IChatClient
{
    public List<IReadOnlyList<ChatMessage>> Calls { get; } = new();

    public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        var snapshot = messages.ToList();
        Calls.Add(snapshot);
        return Task.FromResult(respond(snapshot));
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null,
        CancellationToken cancellationToken = default) => throw new NotSupportedException("The harness only uses non-streaming calls.");

    public object? GetService(Type serviceType, object? serviceKey = null) => serviceType.IsInstanceOfType(this) ? this : null;

    public void Dispose() { }
}
