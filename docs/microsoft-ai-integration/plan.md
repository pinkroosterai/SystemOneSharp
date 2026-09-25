# Plan: SystemOneSharp Microsoft AI integration

**Goal** — Ship `SystemOneSharp.Extensions.AI`, `SystemOneSharp.Extensions.AI.Evaluation` and
`SystemOneSharp.AgentFramework` beside the core package, all on one repository-wide version, done
when `dotnet build SystemOneSharp.slnx -c Release` is warning-free, both verification harnesses
pass with no service or key, and packing produces all four `.nupkg` files at the same version.
**Status** — phase 4 in progress
**Research** — `research.md`

## Context

The design is `docs/SystemOneSharp Microsoft AI Integration Plan.md` (the "design doc"); its
numbered items, API targets, non-goals and definition of done are the requirements, and tasks
below cite them as `design §N`. Today the repo is one packable project with every package
property in its own `.csproj`, a console verification harness using a fake `HttpMessageHandler`,
and release automation that reads the version from that one `.csproj`. The core must stay usable
without any Microsoft AI package, and every integration must reach the server only through
`ISystemOneClient.DecideAsync` (design §17). An open MAF PR (#8563) would add an overlapping
`DecisionLoopEvaluator`; the loop evaluator is built as specified anyway (`research.md § The
Microsoft decision abstraction and the overlapping MAF PR`). Read `tasks/lessons.md` before
changing established behaviour. Phases 4 and 5 both depend only on phase 3 and can run in either order.

## Phase 1 — Repository configuration

**Status** — done
**Rests on** — `src/SystemOneSharp/SystemOneSharp.csproj` still holds all shared package
properties and the SourceLink reference; `release.yml` still reads `<Version>` from that file.
**Settle first** — Whether Dependabot's `nuget` ecosystem updates versions in
`Directory.Packages.props`; answer goes in `research.md § Package versions`.
**Tasks**
- [x] Move shared build and package settings into a root `Directory.Build.props`, leaving
      package-specific ones (ID, description, tags, README) in each project — list in design §1,
      current values in `src/SystemOneSharp/SystemOneSharp.csproj`.
- [x] Mark test, example and future test-support projects non-packable so the root settings
      don't make them packages — projects under `tests/` and `examples/`.
- [x] Introduce central package management for every external package reference, starting with
      SourceLink — current reference in `src/SystemOneSharp/SystemOneSharp.csproj`.
- [x] Make the release tag check read the version from `Directory.Build.props` — `.github/workflows/release.yml`.
- [x] Confirm the packed core package is unchanged in ID, version, README and symbols — compare
      against a pack from before the change.
**Done when** — Release build is warning-free, `tests/SystemOneSharp.Verification` passes, and
`dotnet pack src/SystemOneSharp` yields `SystemOneSharp.<same version>.nupkg` plus `.snupkg`
with the README included.

## Phase 2 — Core additions and diagnostics

**Status** — done
**Rests on** — Phase 1 done; `SystemOneClient.DecideAsync` still builds the payload in one place
(`src/SystemOneSharp/SystemOneClient.cs`); `ISystemOneClient` still has its single method.
**Settle first** — Tag and span names for the `ActivitySource`, and whether they follow the
OpenTelemetry GenAI semantic conventions; answer goes in `research.md § Core diagnostics`.
**Tasks**
- [x] Add an optional per-request model that overrides `SystemOneOptions.Model`, settable from
      the builder, with `ISystemOneClient` unchanged — design §2; request model in
      `SystemOneModels.cs`, builder in `SystemOneRequestBuilder.cs`, validation rules in
      `SystemOneRequestValidator.cs`.
- [x] Add the `JsonElement` and `JsonTypeInfo<T>` state overloads, keeping deep-clone and
      validation behaviour of the existing overloads — design §3; `SystemOneRequestBuilder.cs`.
- [x] Capture unknown response fields on `SystemOneResponse` without adding them to validation —
      design §4; `SystemOneModels.cs`, response validation in `SystemOneClient.cs`.
- [x] Update `SPEC.md` so the contract says extra response fields are preserved but not
      validated — the current wording at `SPEC.md:17` says "ignore".
- [x] Instrument `DecideAsync` with an `ActivitySource` recording only the non-sensitive fields
      in design §5, including retry count and success/failure — retry loop in `SystemOneClient.cs`.
- [x] Add harness checks for: model override on the wire, default model when unset, both new
      state overloads, `routing` preserved on the response, activity tags present and no state or
      key in them — `tests/SystemOneSharp.Verification/Program.cs` (its canned body already has
      `routing`).
- [x] Document the new API in `README.md` and add a `CHANGELOG.md` entry. (`REFERENCE_GUIDE.md` is about
      token limits and instruction writing, not the client API, so it was left alone.)
**Done when** — Release build warning-free; verification harness passes with the new checks
listed in its output; `SystemOneSharp.csproj` still references no Microsoft AI package.

## Phase 3 — Extensions.AI package, shared test support, integration harness

**Status** — done
**Rests on** — Phase 2 done (the projection uses the new state overloads); MEAI Abstractions
`10.10.0` is still the version MAF and Evaluation depend on (`research.md § Package versions`).
**Settle first** — How argument values and function results (`object?`) are serialized into
the projection, e.g. whether MEAI's `AIJsonUtilities.DefaultOptions` is used; answer goes in
`research.md § Microsoft.Extensions.AI content types`.
**Tasks**
- [x] Create `src/SystemOneSharp.Extensions.AI` referencing only the core and
      `Microsoft.Extensions.AI.Abstractions` — design §6, §26.
- [x] Implement the single conversation projection with its own stable JSON schema, options,
      and the builder / `ChatMessage` extensions — design §6 (schema example, the three supported
      content types, the exclusions list); API names in the design doc's "Initial public API target".
- [x] Make unsupported content types behave predictably and document how — design §6 "add other
      types only when semantics are clear"; type list in `research.md § Microsoft.Extensions.AI content types`.
- [x] Add no decision-client abstraction and no `IChatClient` — design §7 and non-goals.
- [x] Create the non-packable `tests/SystemOneSharp.Testing` with the fake client and canonical
      answers — design §18.
- [x] Create `tests/SystemOneSharp.Integrations.Verification` as a console harness in the same
      `Check(...)` style as the core harness — design §19; pattern in `tests/SystemOneSharp.Verification/Program.cs`.
- [x] Cover the three projection scenarios from design §20 plus: excluded properties absent,
      stable output for identical input.
- [x] Add the new projects to `SystemOneSharp.slnx`.
**Done when** — Release build warning-free; both harnesses pass; the projection is the only
code in `src/` that reads `ChatMessage` contents (grep for `FunctionCallContent` hits only this
package).

## Phase 4 — Evaluation package

**Status** — done
**Rests on** — Phase 3 done; `IEvaluator.EvaluateAsync` still has the signature in
`research.md § Microsoft.Extensions.AI.Evaluation`.
**Settle first** — Whether evaluation metrics can carry confidence and probabilities as
metadata or diagnostics; answer goes in `research.md § Microsoft.Extensions.AI.Evaluation`.
**Tasks**
- [x] Create `src/SystemOneSharp.Extensions.AI.Evaluation` referencing core, Extensions.AI and
      `Microsoft.Extensions.AI.Evaluation`, and nothing from MAF — design §8, §26.
- [x] Implement one configurable `SystemOneEvaluator` that sends all configured questions in one
      request and maps each answer to a metric (Noul/Score numeric, Choice string) — design §8, §9.
- [x] Build state only through the phase 3 projection — design §8, §17.
- [x] Offer optional interpretation through MEAI's own interpretation type, never inside the
      evaluator's inference path — design §10.
- [x] Decide and document what the evaluator does when `DecideAsync` throws, keeping the
      existing exception semantics — design §20 last scenario; exceptions in `src/SystemOneSharp/SystemOneExceptions.cs`.
- [x] Add the evaluation scenarios from design §20 (several metrics → one call, metric types,
      cancellation propagated, exceptions preserved) to the integration harness.
**Done when** — Release build warning-free; integration harness passes the listed scenarios,
including a check that the fake client recorded exactly one call for a multi-metric evaluator.

## Phase 5 — Agent Framework package

**Status** — not started
**Rests on** — Phase 3 done; `LoopEvaluator`, `LoopContext`, `LoopEvaluation` and the function
middleware signature still match `research.md § Microsoft Agent Framework loop evaluation` and
`§ Microsoft Agent Framework function-calling middleware and approvals` in `Microsoft.Agents.AI` 1.22.0 or later.
**Settle first**
- How `[Experimental]` on `LoopEvaluator` is handled: suppression in the package, and whether
  `SystemOneCompletionLoopEvaluator` is itself marked experimental; answer goes in
  `research.md § Microsoft Agent Framework loop evaluation`.
- What state the gate may rely on as "validated arguments", since the docs don't say arguments
  are validated before middleware; answer goes in `research.md § Microsoft Agent Framework function-calling middleware and approvals`.
**Tasks**
- [ ] Create `src/SystemOneSharp.AgentFramework` referencing core, Extensions.AI and
      `Microsoft.Agents.AI`, not the evaluation package — design §11, §26.
- [ ] Implement `SystemOneCompletionLoopEvaluator` with an explicit completion policy (threshold
      or delegate), no own iteration cap, no generated feedback, stateless across runs — design
      §12–§14; failure-handling reference in `research.md § The Microsoft decision abstraction and the overlapping MAF PR`.
- [ ] Implement function-call gating as function middleware that classifies the proposed call
      and hands a disposition to application policy — design §15, §16; middleware shape in `research.md`.
- [ ] Route the "review" disposition to application code or MAF's existing approval mechanism,
      never a home-grown HITL flow, and never let a classification override authorization —
      design §15, §16; `ApprovalRequiredAIFunction` in `research.md`.
- [ ] Add no executor, router or workflow wrapper — design §11 and non-goals.
- [ ] Add the loop and gate scenarios from design §20 to the integration harness.
**Done when** — Release build warning-free; integration harness passes: below-threshold →
Continue, above → Stop, allow → function runs, block → function does not run, cancellation and
exceptions propagate.

## Phase 6 — Example, docs, CI and release

**Status** — not started
**Rests on** — Phases 4 and 5 done.
**Settle first**
- The version number for the first multi-package release (`v0.1.0-preview.1` is taken); answer
  goes in `research.md § Release`.
- Which MAF package the workflow demo needs; answer goes in `research.md § Package versions`.
**Tasks**
- [ ] Add `examples/SystemOneSharp.MicrosoftAI.Example` covering the five scenarios in design
      §21, configured like the existing example (key via environment variable name in
      `appsettings.json`) — pattern in `examples/SystemOneSharp.Example/`.
- [ ] Write `docs/microsoft-extensions-ai.md`, `docs/evaluation.md`, `docs/agent-framework.md`,
      and link them from `README.md` without growing `SPEC.md` — design §22.
- [ ] Give each new package its own ID, description, tags and README — design §1; shared
      settings already in `Directory.Build.props`.
- [ ] Update CI to run both harnesses and pack every publishable project on both OSes, then
      check the produced packages — design §24; `.github/workflows/ci.yml`.
- [ ] Update release to pack all four packages into one folder, push all `.nupkg`, attach all
      `.nupkg`/`.snupkg` — design §25; `.github/workflows/release.yml`.
- [ ] Update `CLAUDE.md` commands and architecture for the new projects, and `CHANGELOG.md`.
- [ ] Walk the design doc's "Definition of done" and record each line's evidence in the Log.
**Done when** — Release build warning-free; both harnesses pass; packing the solution locally
yields four `.nupkg` at the same version; both workflow files reference all four packages and
both harnesses. The CI run itself is checked on the next push.

## Log

- 2026-09-25, phase 1 — Added `Directory.Build.props` (shared build, package, symbol settings and
  the SourceLink reference) and `Directory.Packages.props` (central versions). Test and example
  projects set `IsPackable=false`. `release.yml` reads the version via
  `dotnet msbuild -getProperty:Version` instead of `sed` on one `.csproj`. SourceLink first went in
  as a `GlobalPackageReference`; that dropped `branch`/`commit` from the nuspec, so it is a normal
  `PackageReference` in `Directory.Build.props` with its version central. Done when: build 0
  warnings, 47 PASS + "All SystemOneSharp verification checks passed.", solution pack yields only
  `SystemOneSharp.0.1.0-preview.1.nupkg`/`.snupkg`, nuspec and file list identical to a pre-change
  pack except the commit hash.
- 2026-09-25, phase 2 — Added `SystemOneRequest.Model` + `WithModel` (validator rejects a blank
  model), `WithState(JsonElement)`, `WithState<T>(T, JsonTypeInfo<T>)`,
  `SystemOneResponse.AdditionalProperties` (`[JsonExtensionData]`), and `SystemOneDiagnostics` with
  an `ActivitySource` around `DecideAsync` (names per `research.md § Core diagnostics`; validation
  errors throw before a span starts). `SPEC.md` now says extra fields are preserved and a request
  may override the model. Correction: the docs task named `REFERENCE_GUIDE.md`, which does not cover
  the client API; only `README.md` and `CHANGELOG.md` changed. Done when: build 0 warnings; harness
  64 PASS + "All SystemOneSharp verification checks passed." (new: model override/default/blank,
  JsonElement and JsonTypeInfo state, `routing` preserved, span name/kind/tags/status, retry count,
  API and transport failure `error.type`, no state/instructions/key in tags); core `.csproj` has no
  package references.
- 2026-09-25, phase 3 — Added `src/SystemOneSharp.Extensions.AI` (MEAI Abstractions `10.10.1`):
  `SystemOneAiState.Create`, `SystemOneAiStateOptions` (`SerializerOptions`,
  `ThrowOnUnsupportedContent`), and `ToSystemOneState` / `WithConversation` extensions. Assumption:
  the builder extension is `WithConversation`, not the design doc's conceptual `WithState(messages)`,
  because the instance `WithState<T>(T)` would bind first and reflection-serialize whole
  `ChatMessage` objects. Unsupported content is skipped by default (turn kept with empty `contents`)
  and throws `NotSupportedException` when configured. Added non-packable `tests/SystemOneSharp.Testing`
  (`FakeSystemOneClient`, `SystemOneAnswers`, `SystemOneResponses`, `Verify`) and
  `tests/SystemOneSharp.Integrations.Verification` (one checks file per package). Done when: build 0
  warnings; core harness and integration harness both print their "All ... passed." line (10
  integration checks); grep for `FunctionCallContent|ChatMessage` in `src/` hits only
  `src/SystemOneSharp.Extensions.AI/SystemOneAiState.cs`.
- 2026-09-25, phase 4 — Added `src/SystemOneSharp.Extensions.AI.Evaluation` (MEAI Evaluation
  `10.10.0`): `SystemOneEvaluator(client, configureRequest, configureMetrics, stateOptions?)` and
  `SystemOneMetricMap` (`Noul`/`Score` → `NumericMetric`, `Choice` → `StringMetric`, each with an
  optional interpretation delegate for thresholds). State = conversation + response messages through
  the phase 3 projection. Confidence and distributions go into metric `Metadata`
  (`systemone-confidence`, `systemone-probabilities`) plus the built-in `eval-*` model/token/duration
  keys (`research.md § Evaluation metric metadata`). Decision on failures: `DecideAsync` exceptions
  propagate unchanged (a failed decision is never reported as a score); `ChatConfiguration` and
  `additionalContext` are ignored and documented as such; a metric mapped to a missing or
  wrong-type question throws `InvalidOperationException`. The design doc names a
  `SystemOneEvaluatorOptions`; the only option needed was the projection options, so it is a
  constructor parameter and no options class was added. Done when: build 0 warnings; both harnesses
  pass (integration adds 15 evaluation checks, including "several evaluator metrics → one DecideAsync
  call" with `client.Requests.Count == 1`); no `Microsoft.Agents` package in the evaluation assets.
