# SystemOneSharp specification

## Scope and decisions

Build a `net10.0` C# class library that calls the shared Jev/Laya System One HTTP protocol. The caller chooses one endpoint when constructing the client. The library does not start Laya, select a checkpoint, or switch providers automatically. A dependency-free console verification project exercises the public API with an in-memory HTTP handler.

## Wire contract

`POST /v1/systemone` with `Content-Type: application/json`. Hosted Jev uses `https://api.typesafe.ai/` and `Authorization: Bearer <key>`. Laya's `laya-serve` accepts the same route on a configured local base URL and only needs the bearer header when its `LAYA_API_KEY` is set. Use the configured base URL plus the fixed relative route, regardless of provider. The request JSON contains required `state` (string, object, or array), `model` (default `jev-latest`; a request may override the client's configured model), and a nonempty `questions` object keyed by caller question ID. Laya ignores a Jev model ID and routes automatically; a recognized Laya checkpoint ID can be set explicitly.

| Question | Request fields | Answer fields |
| --- | --- | --- |
| Choice | `type: "choice"`, `instructions`, `criteria` object with 2–255 named options and JSON descriptions (including `null`) | `type`, `choice` label, `probabilities` map, `confidence` |
| Score | `type: "score"`, `instructions`, ordered `criteria` array with 2–10 JSON descriptions | `type`, fractional zero-based `score`, `legend` map, `probabilities` map, `confidence` |
| Noul | `type: "noul"`, `instructions`, optional `criteria` object keyed `true` and `false` | `type`, `noul` probability; no separate confidence |

`instructions` and non-null descriptions may be JSON string, object, or array. A response contains `model`, an `answers` map under the same question IDs, and `usage` with `input_tokens` and `output_tokens`. Preserve additional response fields such as Laya's `routing` without validating them; they are not part of the contract. Validate the known answer type and required fields; preserve returned numeric values rather than calculating a second decision. Do not silently substitute missing answers.

Sources: [TypeSafe API reference](https://docs.typesafe.ai/api), [TypeSafe quick start](https://docs.typesafe.ai/introduction/quickstart), [Laya HTTP server](https://github.com/NandhaKishorM/laya/blob/main/laya/serve.py).

## Public C# contract

Namespace `SystemOneSharp`:

- `SystemOneOptions`: `BaseUri` (absolute HTTP(S), default `https://api.typesafe.ai/`), `ApiKey` (optional; required by hosted Jev), `Model` (default `jev-latest`), `MaxRetries` (default 2, nonnegative), `InitialRetryDelay` (default 250 ms, nonnegative). A caller supplies options and an injected `HttpClient`; the library does not dispose the injected client. API keys are never included in exception text.
- `ISystemOneClient.DecideAsync(SystemOneRequest request, CancellationToken cancellationToken = default)` returns `Task<SystemOneResponse>`.
- `SystemOneClient` implements the interface. Each call uses the client's configured `Model`; `SystemOneRequest` contains `State` and `Questions`.
- `SystemOneRequest`: `JsonNode State` and `IReadOnlyDictionary<string, SystemOneQuestion> Questions`. `JsonNode` preserves JSON string/object/array state without a lossy conversion.
- `SystemOneQuestion` abstract base with `JsonNode Instructions`; `ChoiceQuestion` adds `IReadOnlyDictionary<string, JsonNode?> Criteria`; `ScoreQuestion` adds `IReadOnlyList<JsonNode> Criteria`; `NoulQuestion` adds optional `IReadOnlyDictionary<string, JsonNode> Criteria`. The JSON discriminator `type` is emitted as `choice`, `score`, or `noul`. Convenience constructors accept plain strings and convert them to JSON values.
- `SystemOneResponse`: `string Model`, `IReadOnlyDictionary<string, SystemOneAnswer> Answers`, `TokenUsage Usage`.
- `SystemOneAnswer` abstract base with `ChoiceAnswer` (`Choice`, `Probabilities`, `Confidence`), `ScoreAnswer` (`Score`, `Legend`, `Probabilities`, `Confidence`), and `NoulAnswer` (`Noul`). The JSON discriminator selects the concrete answer type.
- `SystemOneApiException`: non-success HTTP status, sanitized response body, and request attempt count. `SystemOneTransportException`: exhausted network failures. `SystemOneProtocolException`: malformed or incomplete successful response. Invalid caller requests raise `ArgumentException` before sending.

## Behavior

Validate configuration at construction and validate request before HTTP execution. Preserve question IDs and criteria order when serializing. Send a new `HttpRequestMessage` for each attempt. Retry only HTTP 429 and 529 up to `MaxRetries`, using exponential backoff and honoring `Retry-After` when present. Transport failures remain typed errors and are not retried. Cancellation always propagates as `OperationCanceledException`. Return a typed HTTP exception for all final non-success statuses, including Jev 401/422 and Laya 400/413/422/500. Accept Laya's FastAPI `detail` error shape and TypeSafe's JSON errors without requiring either form.

Validate that every requested question has a response with the matching type; that Choice's selected label is in its probability map; and that required probabilities and confidence values are finite and within 0–1. For Score, validate finite score and a nonempty legend/probability map. Do not require probability sums to equal exactly 1 because server rounding can alter the sum slightly.

## Verification and exclusions

The console harness must verify JSON request shape, bearer handling, URL mapping, typed deserialization for all three primitives, error handling, retry behavior, and cancellation using a fake HTTP handler. Run `dotnet build` and the full harness before handoff. A live Jev call requires a user key and a running local Laya call requires a user-managed server; neither is required for deterministic verification.

No automatic Jev-to-Laya fallback, DI registration package, local model runtime, model-listing API, or business-specific confidence threshold is part of this release.
