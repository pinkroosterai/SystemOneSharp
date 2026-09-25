# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Overview

`SystemOneSharp` is a `net10.0` C# client library for the System One HTTP protocol (hosted Jev or a local Laya `laya-serve`), plus three optional Microsoft AI integration packages. The wire contract is in `SPEC.md` (source of truth); `PLAN.md` is the original build plan; usage is in `README.md`.

## Commands

Run from the repo root in PowerShell on Windows.

- Build (keep it warning-free): `dotnet build SystemOneSharp.slnx -c Release`
- Verify core (no service or key needed): `dotnet run --project tests/SystemOneSharp.Verification -c Release`
- Verify integrations (no service or key needed): `dotnet run --project tests/SystemOneSharp.Integrations.Verification -c Release`
- Live demos: `dotnet run --project examples/SystemOneSharp.Example -c Release` and `dotnet run --project examples/SystemOneSharp.MicrosoftAI.Example -c Release` (need local Laya on port 8000, or hosted Jev with its key configured in the example's `appsettings.json`)
- Pack all four packages: `dotnet pack SystemOneSharp.slnx -c Release -o artifacts`

CI (`.github/workflows/ci.yml`) runs restore, build, both harnesses, pack and a package check on Ubuntu and Windows. `release.yml` requires the `vX.Y.Z` tag to equal `<Version>` in `Directory.Build.props`, which every package shares.

There is no unit-test framework, so there is no way to run a single test. The core harness is `tests/SystemOneSharp.Verification/Program.cs`, a console app using an in-memory fake `HttpMessageHandler`. Add `Check(condition, "descriptive label")` cases there when changing serialization, HTTP behaviour or answer handling, and read the full output. The integration harness (`tests/SystemOneSharp.Integrations.Verification`, one `*Checks.cs` file per package) uses `FakeSystemOneClient` and helpers from the non-packable `tests/SystemOneSharp.Testing`, because it tests framework composition, not HTTP.

## Architecture

Projects in `SystemOneSharp.slnx`: `src/SystemOneSharp` (core library), `src/SystemOneSharp.Extensions.AI`, `src/SystemOneSharp.Extensions.AI.Evaluation`, `src/SystemOneSharp.AgentFramework` (integrations), `tests/SystemOneSharp.Verification` and `tests/SystemOneSharp.Integrations.Verification` (harnesses), `tests/SystemOneSharp.Testing` (shared fakes), and two live examples. Shared build and package settings, including the one repository-wide `<Version>`, live in `Directory.Build.props`; package versions in `Directory.Packages.props` (central package management, so `PackageReference` items carry no version).

Library flow, all in `src/SystemOneSharp/`:

- `SystemOneRequestBuilder` (fluent, with `Choice`/`Score`/`Noul` sub-builders) produces a `SystemOneRequest` (`State` as `JsonNode`, `Questions` keyed by caller ID). It deep-clones inputs and calls the validator in `Build()`.
- `SystemOneRequestValidator` enforces request rules: state is string/object/array; Choice has 2–255 options, Score 2–10 levels, Noul criteria are exactly `true`/`false` or absent. It throws `ArgumentException` before any HTTP.
- `SystemOneClient.DecideAsync` validates, serializes `{state, model, questions}`, and POSTs to `{BaseUri}/v1/systemone`. The model is `request.Model ?? SystemOneOptions.Model`. The bearer header is sent only when `ApiKey` is set. It retries only HTTP 429/529 (honours `Retry-After`, else exponential backoff capped at 30 s) and builds a new `HttpRequestMessage` per attempt.
- Response handling: `ValidateResponse` checks by hand that every requested question has a matching-type answer with finite values in range (Choice's label must be in its probabilities). Then `System.Text.Json` deserializes into polymorphic `SystemOneAnswer` types. The client returns the server's numbers as-is and never computes its own decision.
- Errors (`SystemOneExceptions.cs`): `SystemOneApiException` (non-success status, body sanitized: API key redacted, cut to 4096 chars), `SystemOneTransportException` (network failure or timeout, not retried), `SystemOneProtocolException` (bad successful response). Caller cancellation propagates as `OperationCanceledException`.
- The `HttpClient` is injected and owned by the caller; the library never disposes it.
- Diagnostics: `DecideAsync` runs inside an `ActivitySource` span (`SystemOneDiagnostics`), with OpenTelemetry GenAI attribute names plus `systemone.*` counts. It never records state, instructions, answers or the key.

Integration packages, all in `src/`:

- `SystemOneSharp.Extensions.AI`: `SystemOneAiState` is the single projection of `ChatMessage` conversations into System One state (text, function call, function result). Every other integration builds state through it and never serializes `ChatMessage` itself.
- `SystemOneSharp.Extensions.AI.Evaluation`: `SystemOneEvaluator` sends every configured question in one request and maps each answer to a metric.
- `SystemOneSharp.AgentFramework`: `SystemOneCompletionLoopEvaluator` (a `LoopEvaluator`, experimental `MAAI001` like its base) and `SystemOneFunctionGate` (function-invocation middleware).
- Invariant: integrations reach the server only through `ISystemOneClient.DecideAsync`, and never add their own HTTP, retries, validation, parsing or tracing. Policy such as thresholds, approvals and authorization stays in caller-supplied options or delegates, never in `SystemOneClient`.

## Conventions

- Wire names use `System.Text.Json` attributes (e.g. `input_tokens`); do not rename them to match C# style. Private fields use a leading underscore.
- API keys come from environment variables. `appsettings.json` holds only the variable name.
- Conventional commits, e.g. `feat(client): ...`, `fix(example): ...`.
- Read `tasks/lessons.md` before changing established behaviour.
