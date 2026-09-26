# SystemOneSharp.AgentFramework

System One components for [Microsoft Agent Framework](https://learn.microsoft.com/agent-framework/): a loop evaluator that decides whether an agent is done, and function-calling middleware that classifies a tool call before it runs.

```text
dotnet add package SystemOneSharp.AgentFramework
```

It depends on `SystemOneSharp`, `SystemOneSharp.Extensions.AI` and `Microsoft.Agents.AI`. It does not depend on the evaluation package.

## Completion loop evaluator

```csharp
using Microsoft.Agents.AI;
using SystemOneSharp.AgentFramework;

var loop = new LoopAgent(agent,
    new SystemOneCompletionLoopEvaluator(client, new SystemOneCompletionLoopEvaluatorOptions
    {
        CompletionThreshold = 0.95,                      // or IsComplete = probability => ...
        ContinueFeedback = "Finish every part of the request."
    }),
    new LoopAgentOptions { MaxIterations = 5 });
```

After each iteration the evaluator asks one Noul question over the loop's initial messages plus the latest response, and returns `LoopEvaluation.Stop()` or `LoopEvaluation.Continue(feedback)`.

- **The completion policy has no default.** Set exactly one of `CompletionThreshold` or `IsComplete`, or the constructor throws. Turning a probability into stop or continue is your decision.
- **The iteration cap is MAF's.** `LoopAgentOptions.MaxIterations` is the hard stop. The evaluator adds no counter of its own.
- **No generated feedback.** System One judges whether more work is needed; it does not write gap analysis. `ContinueFeedback` is a fixed string. For generated feedback, run `AIJudgeLoopEvaluator` after this evaluator.
- **Experimental.** `LoopEvaluator` is experimental in Agent Framework (`MAAI001`), so this type carries the same ID. Code that already suppresses `MAAI001` to use `LoopAgent` needs nothing more.

The evaluator is stateless and safe to share across concurrent loops. `DecideAsync` exceptions propagate.

## Function-call gate

```csharp
var gate = new SystemOneFunctionGate(client, new SystemOneFunctionGateOptions
{
    Categories = choice => choice
        .Option("routine", "Safe, reversible, and what the user asked for")
        .Option("review", "Possibly harmful; a person should confirm it")
        .Option("blocked", "Destructive or outside what the user asked for"),
    Policy = async (classification, cancellationToken) => classification.Category switch
    {
        "routine" => SystemOneFunctionGateDecision.Invoke(),
        "review" => await myReviewQueue.DecideAsync(classification, cancellationToken),
        _ => SystemOneFunctionGateDecision.Block("This action is not allowed.")
    },
    PolicyContext = context => new JsonObject { ["environment"] = "production" }
});

AIAgent gated = agent.AsBuilder().UseSystemOneFunctionGate(gate).Build();
```

The gate asks one Choice question. The state holds the function's name and description, the arguments it is about to be called with, the recent conversation (`MaxConversationMessages`: `null` for all, `0` for none) and optional `policy_context`. Your `Policy` turns the classification into a decision:

- `Invoke()` calls the next middleware. Nothing further down the pipeline is skipped.
- `Block(result)` skips the function and returns `result` to the model as the function's result.

What "review" means is up to you. The gate has no human-in-the-loop flow of its own. Handle review in the policy, or mark the tool with MAF's `ApprovalRequiredAIFunction` to use MAF's approval flow.

**Classification is not authorization.** System One can call an action suspicious; it cannot grant permission. Keep deterministic authorization checks in their own middleware or inside the function. `Invoke()` never bypasses them.

The arguments are the model's parsed arguments, possibly changed by earlier middleware. Nothing has checked them against the function's schema at this point. If `DecideAsync` throws, the exception propagates and the function is not invoked. The middleware requires an agent built on `FunctionInvokingChatClient`, such as `ChatClientAgent`.

## Workflows

No `SystemOneExecutor` exists, and none is needed. In a workflow, System One is an ordinary function executor, and routing on its answer uses ordinary conditional edges. The threshold goes on the edge, where MAF expects routing policy:

```csharp
Func<string, IWorkflowContext, CancellationToken, ValueTask<Triaged>> triage = async (ticket, _, ct) =>
{
    var response = await client.DecideAsync(new SystemOneRequestBuilder()
        .WithState(ticket).AddNoul("refund", "Does the customer ask for a refund?").Build(), ct);
    return new Triaged(ticket, response.GetNoul("refund").Noul);
};

var triageExecutor = triage.BindAsExecutor("triage");
var workflow = new WorkflowBuilder(triageExecutor)
    .AddEdge<Triaged>(triageExecutor, refunds, t => t!.RefundProbability >= 0.5)
    .AddEdge<Triaged>(triageExecutor, support, t => t!.RefundProbability < 0.5)
    .WithOutputFrom(refunds, support)
    .Build();
```

The [Microsoft AI example](https://github.com/pinkroosterai/SystemOneSharp/tree/master/examples/SystemOneSharp.MicrosoftAI.Example) runs this workflow, the loop evaluator and the gate against a live endpoint.

See also: [conversation projection](https://github.com/pinkroosterai/SystemOneSharp/blob/master/docs/microsoft-extensions-ai.md), [evaluation](https://github.com/pinkroosterai/SystemOneSharp/blob/master/docs/evaluation.md), [core client](https://github.com/pinkroosterai/SystemOneSharp/blob/master/README.md).
