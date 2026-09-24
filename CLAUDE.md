# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Overview

`SystemOneSharp` is a `net10.0` C# client library for the System One HTTP protocol (hosted Jev or a local Laya `laya-serve`). The wire contract is in `SPEC.md` (source of truth); `PLAN.md` is the original build plan; usage is in `README.md`.

## Commands

Run from the repo root in PowerShell on Windows.

- Build (keep it warning-free): `dotnet build SystemOneSharp.slnx -c Release`
- Verify (no service or key needed): `dotnet run --project tests/SystemOneSharp.Verification -c Release`
- Live demo: `dotnet run --project examples/SystemOneSharp.Example -c Release` (needs local Laya on port 8000, or hosted Jev with its key configured in the example's `appsettings.json`)
- Pack: `dotnet pack src/SystemOneSharp/SystemOneSharp.csproj -c Release`

CI (`.github/workflows/ci.yml`) runs restore, build, verify and pack on Ubuntu and Windows.

There is no unit-test framework, so there is no way to run a single test. The harness is `tests/SystemOneSharp.Verification/Program.cs`, a console app using an in-memory fake `HttpMessageHandler`. Add `Check(condition, "descriptive label")` cases there when changing serialization, HTTP behaviour or answer handling, and read the full output.

## Architecture

Three projects in `SystemOneSharp.slnx`: `src/SystemOneSharp` (library), `tests/SystemOneSharp.Verification` (harness), `examples/SystemOneSharp.Example` (live demo).

Library flow, all in `src/SystemOneSharp/`:

- `SystemOneRequestBuilder` (fluent, with `Choice`/`Score`/`Noul` sub-builders) produces a `SystemOneRequest` (`State` as `JsonNode`, `Questions` keyed by caller ID). It deep-clones inputs and calls the validator in `Build()`.
- `SystemOneRequestValidator` enforces request rules: state is string/object/array; Choice has 2–255 options, Score 2–10 levels, Noul criteria are exactly `true`/`false` or absent. It throws `ArgumentException` before any HTTP.
- `SystemOneClient.DecideAsync` validates, serializes `{state, model, questions}`, and POSTs to `{BaseUri}/v1/systemone`. The model comes from `SystemOneOptions`, not the request. The bearer header is sent only when `ApiKey` is set. It retries only HTTP 429/529 (honours `Retry-After`, else exponential backoff capped at 30 s) and builds a new `HttpRequestMessage` per attempt.
- Response handling: `ValidateResponse` checks by hand that every requested question has a matching-type answer with finite values in range (Choice's label must be in its probabilities). Then `System.Text.Json` deserializes into polymorphic `SystemOneAnswer` types. The client returns the server's numbers as-is and never computes its own decision.
- Errors (`SystemOneExceptions.cs`): `SystemOneApiException` (non-success status, body sanitized: API key redacted, cut to 4096 chars), `SystemOneTransportException` (network failure or timeout, not retried), `SystemOneProtocolException` (bad successful response). Caller cancellation propagates as `OperationCanceledException`.
- The `HttpClient` is injected and owned by the caller; the library never disposes it.

## Conventions

- Wire names use `System.Text.Json` attributes (e.g. `input_tokens`); do not rename them to match C# style. Private fields use a leading underscore.
- API keys come from environment variables. `appsettings.json` holds only the variable name.
- Conventional commits, e.g. `feat(client): ...`, `fix(example): ...`.
- Read `tasks/lessons.md` before changing established behaviour.
