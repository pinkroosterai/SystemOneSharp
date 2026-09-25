# Changelog

## Unreleased

- Per-request model override: `SystemOneRequest.Model` and `SystemOneRequestBuilder.WithModel`.
- `WithState(JsonElement)` and `WithState<T>(T, JsonTypeInfo<T>)` state overloads.
- `SystemOneResponse.AdditionalProperties` keeps response fields outside the contract, such as Laya's `routing`.
- `ActivitySource` tracing of `DecideAsync` (`SystemOneDiagnostics.ActivitySourceName`).

## 0.1.0-preview.1

First public preview: typed Choice, Score, and Noul requests and answers, fluent request builder, retries for HTTP 429 and 529, and typed API, transport, and protocol exceptions.
