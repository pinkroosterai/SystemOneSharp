# SystemOneRequestBuilder API reference

Verified against the repository on 2026-09-25. Source code is authoritative.

## Basic shape

```csharp
var request = new SystemOneRequestBuilder()
    .WithState(...)
    .AddChoice(...)
    .AddScore(...)
    .AddNoul(...)
    .Build();
```

Any mix of Choice, Score, and Noul questions is allowed.

## WithState

Overloads:

```csharp
WithState(string state)
WithState(JsonNode state)
WithState<T>(T state)
```

At `Build()`, State must be a JSON string, object, or array. `WithState(JsonNode)` deep-clones the supplied node. Each `Build()` returns an independent snapshot.

## AddChoice

```csharp
.AddChoice("team", "Which team should handle this?", choice => choice
    .Option("billing", "Payments and refunds")
    .Option("support", "Product issues"))
```

Option methods are `Option(string, string?)` and `OptionJson(string, JsonNode?)`.

Rules: ID must be nonblank and unique; labels must be nonblank and unique; `Build()` requires 2 to 255 options.

## AddScore

```csharp
.AddScore("urgency", "How urgent is this?", score => score
    .Level("routine")
    .Level("soon")
    .Level("critical"))
```

Level methods are `Level(string)` and `LevelJson(JsonNode)`.

Order is preserved and meaningful. `Build()` requires 2 to 10 levels.

## AddNoul

```csharp
.AddNoul("refund", "Does the customer ask for a refund?")
```

Optional criteria:

```csharp
.AddNoul("refund", "Does the customer ask for a refund?", noul => noul
    .Criteria("Yes definition", "No definition"))
```

Criteria methods are `Criteria(string, string)` and `CriteriaJson(JsonNode, JsonNode)`.

Criteria may be omitted. When present, they contain exactly `true` and `false`.

## Validation timing

Blank/duplicate question IDs, blank/duplicate Choice labels, and null required arguments are rejected immediately.

Cardinality and complete request validity are checked by `.Build()`. Always finish the fluent definition with `.Build()`.

## Structured overloads

Instructions and criteria can also use `JsonNode`. Start with strings and use structured forms only when structure clearly improves the question.

## Sub-builders

Treat `ChoiceQuestionBuilder`, `ScoreQuestionBuilder`, and `NoulQuestionBuilder` as callback-local. Do not capture them for later mutation.

## Verification

When the library's builder behavior itself changes, run:

```text
dotnet build SystemOneSharp.slnx -c Release
dotnet run --project tests/SystemOneSharp.Verification -c Release
```
