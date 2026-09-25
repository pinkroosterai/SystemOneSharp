# SystemOneSharp Microsoft AI Integration Plan

## Objective

Extend SystemOneSharp into the Microsoft AI ecosystem while preserving a small, framework-independent core and avoiding duplicate logic across Microsoft.Extensions.AI, evaluation, and Microsoft Agent Framework integrations.

The repository will remain .NET 10 only and use one shared version for all published packages.

The intended package structure is:

```text
SystemOneSharp
    │
    ├── SystemOneSharp.Extensions.AI
    │       shared Microsoft.Extensions.AI integration
    │       and chat/agent-state projection
    │
    ├── SystemOneSharp.Extensions.AI.Evaluation
    │       Microsoft.Extensions.AI.Evaluation integration
    │
    └── SystemOneSharp.AgentFramework
            genuinely MAF-specific runtime components
```

`SystemOneSharp.Extensions.AI.Evaluation` and `SystemOneSharp.AgentFramework` both reuse `SystemOneSharp.Extensions.AI` where Microsoft AI types must be converted into System One state.

There will be no SystemOne-specific `IChatClient`, no home-grown `IDecisionClient` equivalent, and no generic MAF executor abstraction.

Microsoft currently has an open proposal for a provider-neutral decision-client abstraction that maps closely to System One's Noul/Choice/Score model. The design should leave room to implement that abstraction later without exposing a competing API today.

## Architectural principles

The core package owns everything that is intrinsic to the System One protocol or useful independently of Microsoft frameworks.

`SystemOneSharp.Extensions.AI` owns translation between Microsoft.Extensions.AI representations and System One state.

`SystemOneSharp.Extensions.AI.Evaluation` owns translation between System One answers and Microsoft evaluation metrics.

`SystemOneSharp.AgentFramework` owns only behavior that specifically implements Agent Framework abstractions such as `LoopEvaluator` or function-call middleware.

Application policy remains outside the core client. Thresholds, approval rules, retry-vs-escalate decisions, and authorization logic must not become properties of `SystemOneClient`.

MAF workflows should continue using MAF's own executors, conditional edges, switches, checkpoints, HITL support, and middleware pipeline. SystemOneSharp should not recreate those abstractions.

## Implementation plan

1. **Centralize repository configuration before adding packages.**

   Add `Directory.Build.props` at repository root and move common settings into it:

   ```text
   TargetFramework = net10.0
   Version
   Authors
   RepositoryUrl
   RepositoryType
   PackageLicenseExpression
   Nullable
   ImplicitUsings
   GenerateDocumentationFile
   symbol/source-link configuration
   ```

   Keep package-specific properties such as `PackageId`, description, tags, and README inside individual project files.

   Add `Directory.Packages.props` and centrally manage Microsoft package versions there.

   All four NuGet packages will use the same repository-wide version.

   The release workflow should read that single version rather than inspecting one package project.

2. **Optimize the base `SystemOneSharp` request model before building adapters.**

   Add an optional per-request model override directly to `SystemOneRequest`:

   ```csharp
   public string? Model { get; init; }
   ```

   and:

   ```csharp
   SystemOneRequestBuilder.WithModel(string model)
   ```

   `SystemOneClient` should resolve the model as:

   ```text
   request.Model ?? SystemOneOptions.Model
   ```

   This is preferable to introducing another `DecideAsync` overload or request-options abstraction because `model` is already part of the actual System One wire request. It also preserves the existing `ISystemOneClient` interface unchanged.

   Existing callers continue using `SystemOneOptions.Model` as the default.

3. **Improve structured-state handling in the core package.**

   Keep the existing:

   ```csharp
   WithState(string)
   WithState(JsonNode)
   WithState<T>(T)
   ```

   and add:

   ```csharp
   WithState(JsonElement)
   WithState<T>(T state, JsonTypeInfo<T> typeInfo)
   ```

   The `JsonTypeInfo<T>` overload provides an AOT/source-generation-friendly path without changing the simple reflection-based overload.

   Any serialization utility required by several packages should be implemented here only when it is framework-independent.

4. **Preserve provider response metadata without modeling every server extension.**

   Add extension-data capture to `SystemOneResponse`, for example through `[JsonExtensionData]`.

   This preserves fields such as Laya/Jev routing information without adding provider-specific strongly typed properties to the core API.

   Integration packages can inspect that metadata when useful without creating a second response parser.

   Existing protocol validation should continue validating only the required System One contract.

5. **Add core OpenTelemetry-compatible diagnostics.**

   Instrument `SystemOneClient.DecideAsync` with `System.Diagnostics.ActivitySource`.

   Record non-sensitive operational information such as:

   ```text
   model
   number of questions
   number of Choice questions
   number of Score questions
   number of Noul questions
   retry count
   input token usage
   output token usage
   response model
   duration
   success/failure
   ```

   Do not record state, instructions, criteria, API keys, function arguments, or returned textual data by default.

   Higher-level integrations should rely on these activities instead of independently instrumenting the System One HTTP call.

6. **Create `SystemOneSharp.Extensions.AI`.**

   This package should reference:

   ```text
   SystemOneSharp
   Microsoft.Extensions.AI.Abstractions
   ```

   It should not depend on Agent Framework or the evaluation packages.

   Its primary responsibility in the first release is producing a consistent System One representation of Microsoft AI conversations.

   Microsoft.Extensions.AI represents conversations through `ChatMessage`, `AIContent`, `FunctionCallContent`, `FunctionResultContent`, and related types.

   Introduce one shared projection implementation, conceptually:

   ```csharp
   SystemOneAiState.Create(IEnumerable<ChatMessage> messages)

   SystemOneAiState.Create(
       IEnumerable<ChatMessage> messages,
       SystemOneAiStateOptions options)
   ```

   and convenience extensions such as:

   ```csharp
   requestBuilder.WithState(messages);

   messages.ToSystemOneState();
   ```

   The projected state should use a stable SystemOneSharp-owned JSON schema rather than directly serializing every property on `ChatMessage`.

   For example:

   ```json
   {
     "messages": [
       {
         "role": "user",
         "contents": [
           {
             "type": "text",
             "text": "..."
           }
         ]
       },
       {
         "role": "assistant",
         "contents": [
           {
             "type": "function_call",
             "call_id": "...",
             "name": "...",
             "arguments": {}
           }
         ]
       }
     ]
   }
   ```

   Initially support the content types that matter to agent decisions:

   ```text
   TextContent
   FunctionCallContent
   FunctionResultContent
   ```

   Add other MEAI content types only when their semantics for System One are clear.

   Do not automatically include `RawRepresentation`, provider-specific metadata, hidden reasoning content, or arbitrary `AdditionalProperties`.

   This projection layer becomes the **single implementation** used by both evaluation and Agent Framework integration.

7. **Keep `SystemOneSharp.Extensions.AI` free of a decision-client abstraction for now.**

   Do not introduce:

   ```text
   IDecisionClient
   DecisionRequest
   DecisionResponse
   BinaryDecision
   ```

   under SystemOneSharp namespaces.

   Microsoft's proposed provider-neutral decision API is still unresolved. If it becomes public, implement an adapter from that abstraction to `ISystemOneClient` in this package.

   The native SystemOneSharp API should continue exposing System One terminology:

   ```text
   Choice
   Score
   Noul
   ```

   The future MEAI adapter can translate Noul to whatever binary/probabilistic primitive Microsoft standardizes.

8. **Create `SystemOneSharp.Extensions.AI.Evaluation`.**

   Dependencies should be:

   ```text
   SystemOneSharp
   SystemOneSharp.Extensions.AI
   Microsoft.Extensions.AI.Evaluation
   ```

   It should not reference Microsoft Agent Framework.

   `IEvaluator` is already the Microsoft.Extensions.AI evaluation abstraction, and Agent Framework's .NET evaluation infrastructure builds on the same evaluation stack.

   Implement a configurable:

   ```csharp
   SystemOneEvaluator : IEvaluator
   ```

   rather than creating a separate class for every possible evaluation question.

   The evaluator should be able to submit multiple System One questions in one request so that one Jev/Laya inference can produce several metrics.

   For example:

   ```text
   task_completed       Noul
   instructions_followed Noul
   quality               Score
   failure_mode           Choice
   ```

   The evaluator should use the shared conversation projection from `SystemOneSharp.Extensions.AI`; it must not independently serialize `ChatMessage` or `ChatResponse`.

9. **Make evaluation metric projection explicit and reusable.**

   Avoid hardcoding a large set of evaluator classes such as:

   ```text
   SystemOneCompletenessEvaluator
   SystemOneQualityEvaluator
   SystemOneAdherenceEvaluator
   SystemOneRoutingEvaluator
   ```

   They would all duplicate the same request/execution machinery.

   Instead, provide configuration that maps System One question IDs onto evaluation metrics.

   Conceptually:

   ```csharp
   new SystemOneEvaluator(
       client,
       requestFactory,
       metrics =>
       {
           metrics.Noul("completed", "TaskCompletion");
           metrics.Noul("followed", "InstructionAdherence");
           metrics.Score("quality", "Quality");
           metrics.Choice("failure", "FailureMode");
       });
   ```

   Exact API names can be finalized during implementation, but the important design constraint is:

   ```text
   one System One request
       ↓
   several answers
       ↓
   several MEAI evaluation metrics
   ```

   Noul and Score naturally map to numeric evaluation metrics; Choice naturally maps to a string/categorical metric. `confidence` and probability distributions may be exposed as metric metadata or diagnostics rather than automatically creating additional top-level metrics.

10. **Do not make evaluation thresholds part of the evaluator's inference logic.**

    The evaluator should report the model output faithfully.

    Interpretation such as:

    ```text
    TaskCompletion >= 0.95 → pass
    ```

    belongs in evaluation configuration/reporting rather than in `SystemOneClient`.

    Where `Microsoft.Extensions.AI.Evaluation` supports metric interpretation or ratings, provide optional configuration there instead of creating SystemOne-specific pass/fail infrastructure.

11. **Create `SystemOneSharp.AgentFramework`.**

    Dependencies should be:

    ```text
    SystemOneSharp
    SystemOneSharp.Extensions.AI
    Microsoft Agent Framework
    ```

    It should not depend on `SystemOneSharp.Extensions.AI.Evaluation` unless a concrete feature truly requires it.

    Do not implement:

    ```text
    SystemOneExecutor<TInput>
    SystemOneExecutor<TInput,TOutput>
    SystemOneRouter
    ```

    MAF already owns workflow execution and conditional routing.

    A System One call inside an ordinary workflow can be expressed through existing MAF function/delegate executors, followed by normal conditional edges.

12. **Make the first Agent Framework component a System One loop evaluator.**

    MAF exposes `LoopEvaluator`, whose job is specifically to inspect a `LoopContext` and return a `LoopEvaluation` indicating whether an agent should be invoked again. It is designed to be stateless and shared across concurrent loop runs.

    Implement:

    ```csharp
    SystemOneCompletionLoopEvaluator : LoopEvaluator
    ```

    rather than a completely generic wrapper.

    Its purpose is:

    ```text
    initial request
       +
    latest agent response
       ↓
    System One Noul decision
       ↓
    complete / continue
       ↓
    LoopEvaluation.Stop()
       or
    LoopEvaluation.Continue()
    ```

    The `LoopContext` already exposes initial messages, latest response, iteration number, feedback history, session, and run options.

    Use the shared `SystemOneSharp.Extensions.AI` state projector for the initial conversation and response.

13. **Require loop policy to be explicit.**

    Turning a probability into `Stop()` or `Continue()` is a MAF-specific policy decision, not a System One protocol concern.

    `SystemOneCompletionLoopEvaluatorOptions` should therefore expose the relevant policy explicitly, for example:

    ```csharp
    CompletionThreshold = 0.95
    ```

    or an equivalent decision delegate.

    Do not hide a consequential threshold inside the core library.

    Also rely on MAF's `LoopAgentOptions.MaxIterations` as the hard loop safety cap rather than creating a second maximum-iteration mechanism. MAF already bounds the loop independently of its evaluators.

14. **Do not attempt generative feedback in the first loop evaluator.**

    Jev/System One is naturally suited to determining whether more work is necessary, but it does not generate open-ended gap-analysis prose.

    The initial evaluator should therefore primarily implement:

    ```text
    continue
    stop
    ```

    Optional predefined feedback may later be selected through a Choice question.

    Do not try to imitate `AIJudgeLoopEvaluator`'s generated gap analysis unless there is an explicit generative model involved. Microsoft's existing AI judge can already produce that kind of feedback.

15. **Add System One function-call gating as the second MAF-specific feature.**

    MAF exposes function-calling middleware around `FunctionInvocationContext`, and that middleware can inspect a proposed function and its validated arguments before invoking the function.

    Implement a reusable System One gate around this extension point.

    The decision state should contain only the information required for classification:

    ```text
    function name
    function description where available
    validated arguments
    relevant conversation context
    optional application-provided policy context
    ```

    The System One request can classify the proposed action, for example:

    ```text
    routine
    review
    blocked
    ```

    The integration must clearly separate **semantic assessment** from **authorization**.

    System One may classify an action as routine or suspicious; it must not override deterministic authorization checks.

16. **Keep tool-gate policy outside the classifier.**

    Separate:

    ```text
    classification
    ```

    from:

    ```text
    what the application does with that classification
    ```

    The middleware should expose a result/disposition to application policy rather than embedding assumptions about what "review" means.

    In particular, do not invent a custom human-approval implementation if MAF already has an appropriate HITL mechanism.

    Initial application policy can map dispositions to:

    ```text
    invoke next middleware
    block execution
    hand control to application-defined review handling
    ```

17. **Reuse one System One decision execution path everywhere.**

    Neither evaluation nor MAF should reproduce HTTP, retry, validation, request serialization, response parsing, or diagnostics.

    Every integration ultimately calls:

    ```csharp
    ISystemOneClient.DecideAsync(...)
    ```

    The dependency flow should remain:

    ```text
    framework object
        ↓
    shared adapter/projection
        ↓
    SystemOneRequest
        ↓
    ISystemOneClient
        ↓
    SystemOneResponse
        ↓
    framework-specific interpretation
    ```

    This should be treated as an architectural invariant.

18. **Create shared test infrastructure instead of duplicating fake clients.**

    Add a non-packable test-support project:

    ```text
    tests/SystemOneSharp.Testing
    ```

    containing:

    ```text
    FakeSystemOneClient
    captured-request support
    canonical Choice responses
    canonical Score responses
    canonical Noul responses
    deterministic response factories
    common assertion helpers
    ```

    The core HTTP/serialization tests should continue using an HTTP stub because that layer specifically needs transport verification.

    Integration tests should use `FakeSystemOneClient` because they are testing framework composition, not HTTP.

19. **Keep the number of executable test projects small.**

    Instead of creating one verification executable for every package, use:

    ```text
    tests/SystemOneSharp.Verification
        core protocol/HTTP behavior

    tests/SystemOneSharp.Integrations.Verification
        Extensions.AI
        Evaluation
        Agent Framework
    ```

    Both can reference shared `SystemOneSharp.Testing` where appropriate.

    This keeps CI simple while avoiding duplicated fixtures.

20. **Add integration tests around shared boundaries, not just individual classes.**

    Required scenarios should include:

    ```text
    ChatMessage conversation → canonical System One state

    FunctionCallContent → canonical function-call state

    FunctionResultContent → canonical function-result state

    several evaluator metrics → one DecideAsync call

    Noul evaluation → NumericMetric

    Score evaluation → NumericMetric

    Choice evaluation → categorical metric

    LoopContext → System One completion request

    completion probability below threshold → Continue()

    completion probability above threshold → Stop()

    proposed MAF function call → gate request

    allow disposition → function executes

    block disposition → function does not execute

    cancellation token → propagated to ISystemOneClient

    SystemOneException → preserves existing failure semantics
    ```

21. **Use one Microsoft-integration example rather than duplicating sample applications.**

    Keep the existing core example.

    Add:

    ```text
    examples/SystemOneSharp.MicrosoftAI.Example
    ```

    which can demonstrate several scenarios in one project:

    ```text
    converting MEAI chat history to System One state
    evaluating a ChatResponse
    using SystemOneCompletionLoopEvaluator
    installing function-call gating middleware
    using System One inside a MAF workflow via normal MAF executors
    ```

    The workflow sample should explicitly demonstrate that no `SystemOneExecutor` is needed.

22. **Split documentation by conceptual layer.**

    Keep `SPEC.md` limited to the native System One wire/client contract.

    Add:

    ```text
    docs/microsoft-extensions-ai.md
    docs/evaluation.md
    docs/agent-framework.md
    ```

    The README should provide only the high-level overview and links.

    This prevents Microsoft-framework documentation from becoming part of the core protocol specification.

23. **Update the solution structure.**

    The resulting repository should approximately become:

    ```text
    SystemOneSharp/
    ├── Directory.Build.props
    ├── Directory.Packages.props
    ├── SystemOneSharp.slnx
    │
    ├── src/
    │   ├── SystemOneSharp/
    │   ├── SystemOneSharp.Extensions.AI/
    │   ├── SystemOneSharp.Extensions.AI.Evaluation/
    │   └── SystemOneSharp.AgentFramework/
    │
    ├── tests/
    │   ├── SystemOneSharp.Testing/
    │   ├── SystemOneSharp.Verification/
    │   └── SystemOneSharp.Integrations.Verification/
    │
    ├── examples/
    │   ├── SystemOneSharp.Example/
    │   └── SystemOneSharp.MicrosoftAI.Example/
    │
    └── docs/
        ├── microsoft-extensions-ai.md
        ├── evaluation.md
        └── agent-framework.md
    ```

24. **Update CI around the solution, not individual package assumptions.**

    CI should:

    ```text
    restore solution
    build solution in Release
    run core verification
    run integration verification
    pack every publishable project
    validate produced packages
    ```

    Continue running on both Ubuntu and Windows.

    The core verification harness should still require no Jev API key or Laya instance.

    Integration verification should likewise use fake clients and require no external services.

25. **Update release automation for a multi-package repository.**

    A `vX.Y.Z` tag must equal the version in `Directory.Build.props`.

    The release job should explicitly pack:

    ```text
    SystemOneSharp
    SystemOneSharp.Extensions.AI
    SystemOneSharp.Extensions.AI.Evaluation
    SystemOneSharp.AgentFramework
    ```

    into one artifacts directory.

    Publish all `.nupkg` files to NuGet and attach all `.nupkg` and `.snupkg` artifacts to the GitHub release.

    All packages receive the same version.

26. **Treat Microsoft framework dependencies as replaceable integration boundaries.**

    Agent Framework currently documents several relevant APIs as prerelease and subject to change, including `LoopEvaluator`.

    Keep all direct MAF references inside `SystemOneSharp.AgentFramework`.

    Keep all direct `Microsoft.Extensions.AI.Evaluation` references inside the evaluation package.

    This limits churn when Microsoft APIs change.

27. **Track the proposed Microsoft decision abstraction without blocking this work.**

    `SystemOneSharp.Extensions.AI` should be deliberately structured so that a future adapter can be added without moving existing logic.

    If Microsoft eventually ships an `IDecisionClient`-style abstraction, the likely future structure becomes:

    ```text
    Microsoft IDecisionClient
          ↓
    SystemOneDecisionClient adapter
          ↓
    ISystemOneClient
    ```

    The existing chat-state projection, core telemetry, evaluation integration, and MAF components can then migrate toward that abstraction incrementally.

    Do not make the current implementation dependent on an unshipped API.

## Initial public API target

The first implementation should aim for a deliberately small public API surface.

Core additions:

```text
SystemOneRequest.Model
SystemOneRequestBuilder.WithModel(...)
SystemOneRequestBuilder.WithState(JsonElement)
SystemOneRequestBuilder.WithState<T>(..., JsonTypeInfo<T>)
SystemOneResponse additional/extension metadata
core ActivitySource diagnostics
```

`SystemOneSharp.Extensions.AI`:

```text
SystemOneAiState
SystemOneAiStateOptions
ChatMessage/SystemOneRequestBuilder projection extensions
```

`SystemOneSharp.Extensions.AI.Evaluation`:

```text
SystemOneEvaluator
SystemOneEvaluatorOptions / metric mapping configuration
```

`SystemOneSharp.AgentFramework`:

```text
SystemOneCompletionLoopEvaluator
SystemOneCompletionLoopEvaluatorOptions

System One function-gating middleware/helper
function-gate configuration/disposition types
```

Avoid publishing additional convenience abstractions until repeated application code demonstrates that they are necessary.

## Explicit non-goals for this implementation

Do not implement an `IChatClient`.

Do not clone the proposed Microsoft `IDecisionClient`.

Do not add generic MAF executor wrappers.

Do not replace MAF workflow routing.

Do not implement a second retry mechanism.

Do not implement duplicate serialization of Microsoft chat objects in evaluation and MAF.

Do not turn Noul probabilities into Booleans in the core package.

Do not put authorization policy into System One.

Do not create separate classes for every possible evaluation metric.

Do not duplicate OpenTelemetry instrumentation in every integration.

Do not create a SystemOne-specific human-in-the-loop framework.

## Recommended implementation order

The safest sequence is:

```text
repository configuration
        ↓
core API improvements
        ↓
core diagnostics
        ↓
SystemOneSharp.Extensions.AI
        ↓
shared Microsoft chat-state projection tests
        ↓
SystemOneSharp.Extensions.AI.Evaluation
        ↓
SystemOneCompletionLoopEvaluator
        ↓
function-call gating
        ↓
combined Microsoft AI example
        ↓
documentation
        ↓
CI / packaging / release changes
```

This order establishes the shared foundations before either consumer package is implemented, which is the main mechanism for preventing duplicate logic.

## Definition of done

The work is complete when:

```text
SystemOneSharp still works independently of all Microsoft AI packages.

All packages target net10.0.

All packages use one repository-wide version.

The existing ISystemOneClient contract remains usable.

A request can override the client's default model.

MEAI conversations have exactly one canonical System One projection implementation.

Evaluation can produce several metrics from one System One request.

MAF can use System One as a LoopEvaluator.

MAF function calls can be semantically classified before execution.

No integration reimplements HTTP, retries, response parsing, or request validation.

No integration independently serializes ChatMessage conversations.

No generic MAF workflow abstraction is duplicated.

Core telemetry works regardless of which integration initiated the decision.

All deterministic tests run without Jev/Laya credentials.

CI builds and packs all four packages on Windows and Linux.

One repository tag releases all packages at the same version.
```