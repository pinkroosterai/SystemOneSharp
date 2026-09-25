# Research: SystemOneSharp Microsoft AI integration

Checked 2026-09-25. Feeds `plan.md`. The design being planned is
`docs/SystemOneSharp Microsoft AI Integration Plan.md` (called "the design doc" below).

## Planning, 2026-09-25

### What the repo looks like today

One packable project, `src/SystemOneSharp`, whose `.csproj` holds every package property
(`Version` `0.1.0-preview.1`, authors, repo URL, licence, SourceLink `Microsoft.SourceLink.GitHub`
`10.0.401`, `IncludeSymbols`/`snupkg`). The verification harness and example repeat
`TargetFramework`/`Nullable`/`ImplicitUsings` in their own `.csproj`. There is no
`Directory.Build.props` or `Directory.Packages.props`. Tag `v0.1.0-preview.1` is already published.

`.github/workflows/release.yml` reads the version with `sed` from
`src/SystemOneSharp/SystemOneSharp.csproj` and packs only that project; `ci.yml` packs only that
project too. `.github/dependabot.yml` watches the `nuget` ecosystem, so central package versions
must stay somewhere Dependabot updates (not checked whether it handles `Directory.Packages.props`;
it is a low-cost thing to confirm when phase 1 runs).

`SystemOneClient.DecideAsync` serializes an anonymous `{state, model = _options.Model, questions}`
(`src/SystemOneSharp/SystemOneClient.cs:42-45`), so a per-request model is a one-site change.
`SystemOneResponse` (`src/SystemOneSharp/SystemOneModels.cs`) has no extension-data member.
`SPEC.md:17` says to *ignore* extra response fields such as Laya's `routing`; capturing them
without validating them does not contradict that, but `SPEC.md` will need one sentence so the
contract and the code agree. The harness's canned success body already carries a `routing`
field (`tests/SystemOneSharp.Verification/Program.cs:7-12`), which gives a ready test input.

### Microsoft Agent Framework loop evaluation

`LoopEvaluator` (namespace `Microsoft.Agents.AI`, package `Microsoft.Agents.AI`) is an abstract
class with one member, `ValueTask<LoopEvaluation> EvaluateAsync(LoopContext, CancellationToken)`.
Implementations must be stateless; per-run state goes in `LoopContext.AdditionalProperties`.
`LoopContext` exposes `Agent`, `Session`, `InitialMessages` (`IReadOnlyList<ChatMessage>`),
`LastResponse` (`AgentResponse`), `Iteration`, `Feedback`, `RunOptions`, `AdditionalProperties`.
`LoopEvaluation` has `Stop()`, `Continue(string?)` and `ContinueWithMessages(...)`.

`LoopEvaluator` carries `[Experimental(DiagnosticIds.Experiments.AgentsAIExperiments)]` — the PR
below names that ID `MAAI001`. Deriving from it is a compile error unless suppressed, and every
consumer of `SystemOneCompletionLoopEvaluator` would inherit the same experimental status. The
package has to decide whether to mark its own type `[Experimental]` too.

`LoopAgent` enforces `LoopAgentOptions.MaxIterations` (default `LoopAgent.DefaultMaxIterations = 10`)
independently of evaluators — the design doc's "rely on MAF's cap" holds.

Sources: [LoopEvaluator](https://learn.microsoft.com/dotnet/api/microsoft.agents.ai.loopevaluator?view=agent-framework-dotnet-latest),
[LoopContext](https://learn.microsoft.com/dotnet/api/microsoft.agents.ai.loopcontext?view=agent-framework-dotnet-latest),
[LoopEvaluation](https://learn.microsoft.com/dotnet/api/microsoft.agents.ai.loopevaluation?view=agent-framework-dotnet-latest),
[LoopEvaluator.cs](https://github.com/microsoft/agent-framework/blob/main/dotnet/src/Microsoft.Agents.AI/Harness/Loop/LoopEvaluator.cs),
[LoopAgent.cs](https://github.com/microsoft/agent-framework/blob/main/dotnet/src/Microsoft.Agents.AI/Harness/Loop/LoopAgent.cs), all checked 2026-09-25.

### Microsoft Agent Framework function-calling middleware and approvals

Function middleware has the shape
`ValueTask<object?> (AIAgent agent, FunctionInvocationContext context, Func<FunctionInvocationContext, CancellationToken, ValueTask<object?>> next, CancellationToken)`
and is installed with `agent.AsBuilder().Use(...).Build()`. It only fires for agents built on
`FunctionInvokingChatClient` (e.g. `ChatClientAgent`). Not calling `next` skips the tool; setting
`context.Terminate = true` ends the function loop but can leave a call without a result in history.
The docs say middleware can "inspect or modify tool call arguments" — they do not say arguments are
schema-validated before middleware runs, so the design doc's "validated arguments" is not
established.

MAF's HITL mechanism is `ApprovalRequiredAIFunction`, which surfaces a `ToolApprovalRequestContent`
the caller answers with `CreateResponse(bool)`. That is the existing mechanism the design doc says
to reuse rather than reinvent for the "review" disposition.

Sources: [Agent middleware](https://learn.microsoft.com/agent-framework/concepts/agents/middleware/),
[Runtime context](https://learn.microsoft.com/agent-framework/concepts/agents/middleware/runtime-context),
[Tool approval](https://learn.microsoft.com/agent-framework/agents/tools/tool-approval), checked 2026-09-25.

### Microsoft.Extensions.AI.Evaluation

`IEvaluator.EvaluateAsync(IEnumerable<ChatMessage> messages, ChatResponse modelResponse, ChatConfiguration? chatConfiguration = null, IEnumerable<EvaluationContext>? additionalContext = null, CancellationToken)`
returns `ValueTask<EvaluationResult>`. Metrics: `NumericMetric(name, double? value, string? reason)`,
`StringMetric(name, string? value, string? reason)`, `BooleanMetric`. `StringMetric` is documented
as the fit for "one value out of a set", which matches Choice. Interpretation is
`EvaluationMetricInterpretation(EvaluationRating rating, bool failed, string? reason)` — the place
the design doc's thresholds belong. Whether metrics carry a free-form metadata dictionary (for
confidence/probabilities) was not checked.

Package `Microsoft.Extensions.AI.Evaluation` latest stable is `10.10.0` (2026-09-09), depends on
`Microsoft.Extensions.AI.Abstractions >= 10.10.0`, targets net8.0+ including net10.0.

Sources: [NumericMetric](https://learn.microsoft.com/dotnet/api/microsoft.extensions.ai.evaluation.numericmetric?view=net-11.0-pp),
[StringMetric](https://learn.microsoft.com/dotnet/api/microsoft.extensions.ai.evaluation.stringmetric?view=net-11.0-pp),
[EvaluationMetricInterpretation](https://learn.microsoft.com/dotnet/api/microsoft.extensions.ai.evaluation.evaluationmetricinterpretation.-ctor?view=net-11.0-pp),
[IEvaluator.EvaluateAsync](https://learn.microsoft.com/dotnet/api/microsoft.extensions.ai.evaluation.ievaluator.evaluateasync?view=net-11.0-pp),
[NuGet](https://www.nuget.org/packages/Microsoft.Extensions.AI.Evaluation), checked 2026-09-25.

### Microsoft.Extensions.AI content types

`FunctionCallContent(callId, name, IDictionary<string, object?>? arguments)` — argument values
are arbitrary objects, not guaranteed `JsonElement`. `FunctionResultContent(callId, object? result)`
— the result is an arbitrary object. Both carry an `Exception` that is not serialized. The
projection therefore needs a serializer choice for arbitrary objects; MEAI's own
`AIJsonUtilities.DefaultOptions` is the obvious candidate (not checked in detail).

Other content types exist and the design doc defers them: `TextReasoningContent`, `DataContent`,
`UriContent`, `ErrorContent`, `UsageContent`, `ToolApprovalRequestContent`/`ResponseContent`,
`InputRequestContent`, hosted/MCP/code-interpreter tool call and result contents.

Source: [dotnet/extensions `Contents/`](https://github.com/dotnet/extensions/tree/main/src/Libraries/Microsoft.Extensions.AI.Abstractions/Contents), checked 2026-09-25.

### Package versions

`Microsoft.Agents.AI` latest stable `1.22.0` (2026-09-18), targets net8.0/9.0/10.0, depends on
`Microsoft.Extensions.AI >= 10.10.0` and `Microsoft.Agents.AI.Abstractions >= 1.22.0`. So all three
Microsoft dependencies line up on MEAI `10.10.0`. The design doc's claim that MAF APIs are
"prerelease" is out of date for the package — it is stable — but individual types like
`LoopEvaluator` are still `[Experimental]`, which is the same practical risk.

Source: [NuGet Microsoft.Agents.AI](https://www.nuget.org/packages/Microsoft.Agents.AI), checked 2026-09-25.

### The Microsoft decision abstraction and the overlapping MAF PR

[dotnet/extensions#7764](https://github.com/dotnet/extensions/issues/7764) (opened 2026-09-19)
proposes `IDecisionClient` with `BinaryQuestion`/`ChoiceQuestion`/`ScoreQuestion`. Labels:
`untriaged`, `area-ai`. Nothing shipped.

[microsoft/agent-framework#8563](https://github.com/microsoft/agent-framework/pull/8563) (opened
2026-09-20, open, review required) adds an `[Experimental(MAAI001)]` copy of that contract to
`Microsoft.Agents.AI.Abstractions`, a generic `DecisionLoopEvaluator` (one binary question per
iteration, completion threshold default 0.90, `FailureBehavior`/`TransientFailureBehavior`), and a
`Microsoft.Agents.AI.TypeSafe` package with its own Jev HTTP client. If merged, it overlaps
`SystemOneCompletionLoopEvaluator` directly. The user chose on 2026-09-25 to build the loop
evaluator as the design doc specifies regardless of this PR. Its failure-behavior split is a
useful reference for the evaluator's own failure handling.

Checked 2026-09-25 via `gh`.

## Ruled out

- **Gating or dropping the loop evaluator on #8563** — offered; the user chose to build it as
  written (2026-09-25).
- **Implementing an `IDecisionClient` adapter now** — the design doc forbids depending on an
  unshipped API, and #7764 is untriaged.

## Still open

- **OpenTelemetry attribute names for the core `ActivitySource`** — carried into phase 2's
  `Settle first`; whether to follow the OTel GenAI semantic conventions was not checked.
- **Whether MEAI evaluation metrics can carry confidence/probabilities as metadata** — carried into
  phase 4's `Settle first`.
- **What "validated arguments" means at the function-middleware point** — carried into phase 5's
  `Settle first`; the docs do not say arguments are validated before middleware runs.
- **How to propagate `[Experimental]` from `LoopEvaluator`** — carried into phase 5's `Settle first`.
- **Version number for the first multi-package release** — carried into phase 6's `Settle first`;
  `v0.1.0-preview.1` is taken.
- **Which MAF package the workflow example needs** (workflows live outside `Microsoft.Agents.AI`;
  not checked) — carried into phase 6's `Settle first`.

## Execution, phase 1 — 2026-09-25

### Dependabot and `Directory.Packages.props`

Dependabot's `nuget` ecosystem updates `PackageVersion` items in a single root
`Directory.Packages.props`; known failures involve several such files or `VersionOverride`,
neither of which this repo uses. No change to `.github/dependabot.yml` needed.

Sources: [dependabot-core#4261](https://github.com/dependabot/dependabot-core/issues/4261),
[dependabot-core#12149](https://github.com/dependabot/dependabot-core/issues/12149), checked 2026-09-25.

### SourceLink as `GlobalPackageReference` loses repository branch/commit

Declaring `Microsoft.SourceLink.GitHub` 10.0.401 as a CPM `GlobalPackageReference` still restored
it, but the packed nuspec's `<repository>` element lost its `branch` and `commit` attributes. A
plain `PackageReference ... PrivateAssets="all"` (version central) restores them. Cause not
investigated further; observed in this repo on the .NET 10 SDK, 2026-09-25.

## Execution, phase 2 — 2026-09-25

### Core diagnostics

The OpenTelemetry GenAI span conventions (status: Development) moved to
`open-telemetry/semantic-conventions-genai`. They set span kind `CLIENT`, span name
`{gen_ai.operation.name} {gen_ai.request.model}`, require `gen_ai.operation.name` and
`gen_ai.provider.name`, and recommend `gen_ai.request.model`, `gen_ai.response.model`,
`gen_ai.usage.input_tokens`, `gen_ai.usage.output_tokens`, plus `error.type` on failure. When no
predefined operation applies, a system-specific name is allowed.

Decision: follow those names for everything they cover. `gen_ai.operation.name` = `decide` (no
predefined value fits a classification call); `gen_ai.provider.name` = `systemone` (the client
cannot tell Jev from Laya, and both speak the System One protocol). Question counts and retries use
a `systemone.` prefix. Duration is the span's own; success/failure is span status plus `error.type`
(HTTP status code for API errors, exception type name otherwise). The source name is exposed as a
public constant so callers can subscribe. Because the conventions are Development-status, the
attribute names may change upstream — this is recorded next to the constants in code.

Source: [gen-ai-spans.md](https://github.com/open-telemetry/semantic-conventions-genai/blob/main/docs/gen-ai/gen-ai-spans.md), checked 2026-09-25.

## Execution, phase 3 — 2026-09-25

### Projection serialization

Function-call arguments (`IDictionary<string, object?>`) and function results (`object?`) are
serialized with `AIJsonUtilities.DefaultOptions` unless the caller passes other options. It is
MEAI's own default, carries source-generated contracts for MEAI exchange types (Native AOT safe),
and uses string enums and relaxed escaping. `WriteIndented` does not matter because the projection
produces a `JsonNode`, not text.

Source: [AIJsonUtilities.DefaultOptions](https://learn.microsoft.com/dotnet/api/microsoft.extensions.ai.aijsonutilities.defaultoptions), checked 2026-09-25.

### Package versions (update)

`Microsoft.Extensions.AI.Abstractions` now has `10.10.1` (latest stable); `Microsoft.Extensions.AI.Evaluation`
`10.10.0` and `Microsoft.Agents.AI` `1.22.0` are unchanged. Both still require MEAI `>= 10.10.0`,
so `10.10.1` is compatible. Checked on the NuGet flat-container index, 2026-09-25.

## Execution, phase 4 — 2026-09-25

### Evaluation metric metadata

Every `EvaluationMetric` has `Metadata` (`IDictionary<string, string>?`), `Diagnostics`,
`Interpretation`, `Context` and `Reason`, with `AddOrUpdateMetadata` helpers. So Score/Choice
confidence and probability distributions go into `Metadata` as invariant-culture strings (the
distribution as a JSON object string) rather than as extra top-level metrics. Built-in evaluators
record model, tokens and duration under `eval-model`, `eval-input-tokens`, `eval-output-tokens`,
`eval-total-tokens`, `eval-duration-ms`; those names are `internal` constants in
`BuiltInMetricUtilities`, so SystemOneEvaluator writes the same strings so reports show them
alongside built-in metrics. `IEvaluator` is `EvaluationMetricNames` plus the one `EvaluateAsync`
already recorded; `EvaluationRating` is Unknown/Inconclusive/Unacceptable/Poor/Average/Good/Exceptional.

Sources: [EvaluationMetric.cs](https://github.com/dotnet/extensions/blob/main/src/Libraries/Microsoft.Extensions.AI.Evaluation/EvaluationMetric.cs),
[EvaluationMetricExtensions.cs](https://github.com/dotnet/extensions/blob/main/src/Libraries/Microsoft.Extensions.AI.Evaluation/EvaluationMetricExtensions.cs),
[BuiltInMetricUtilities.cs](https://github.com/dotnet/extensions/blob/main/src/Libraries/Microsoft.Extensions.AI.Evaluation/Utilities/BuiltInMetricUtilities.cs),
[IEvaluator.cs](https://github.com/dotnet/extensions/blob/main/src/Libraries/Microsoft.Extensions.AI.Evaluation/IEvaluator.cs), checked 2026-09-25.

