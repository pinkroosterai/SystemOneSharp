# Changelog

## 0.2.0

First stable release. The code and public API are the same as `0.2.0-preview.1`; all four packages drop the prerelease suffix. `SystemOneCompletionLoopEvaluator` stays marked experimental (`MAAI001`) because Agent Framework's `LoopEvaluator` is.

## 0.2.0-preview.1

New packages, all released at the same version as the core:

- `SystemOneSharp.Extensions.AI`: one canonical projection of Microsoft.Extensions.AI conversations into System One state (`WithConversation`, `ToSystemOneState`).
- `SystemOneSharp.Extensions.AI.Evaluation`: `SystemOneEvaluator`, several MEAI evaluation metrics from one System One request.
- `SystemOneSharp.AgentFramework`: `SystemOneCompletionLoopEvaluator` for `LoopAgent` (experimental, `MAAI001`) and `SystemOneFunctionGate` function-calling middleware.

Core:

- Per-request model override: `SystemOneRequest.Model` and `SystemOneRequestBuilder.WithModel`.
- `WithState(JsonElement)` and `WithState<T>(T, JsonTypeInfo<T>)` state overloads.
- `SystemOneResponse.AdditionalProperties` keeps response fields outside the contract, such as Laya's `routing`.
- `ActivitySource` tracing of `DecideAsync` (`SystemOneDiagnostics.ActivitySourceName`).

## 0.1.0-preview.1

First public preview: typed Choice, Score, and Noul requests and answers, fluent request builder, retries for HTTP 429 and 529, and typed API, transport, and protocol exceptions.
