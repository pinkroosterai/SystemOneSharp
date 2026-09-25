---
name: authoring-system-one-requests
description: Create, modify, review, and improve fluent SystemOneRequestBuilder definitions. Use for WithState, AddChoice, AddScore, AddNoul, question IDs, instructions, Choice options, Score levels, Noul criteria, batching, and builder validity. Keep the work limited to the request builder expression; do not implement business logic, HTTP/client behavior, model selection, or answer thresholds.
---

# Authoring SystemOneRequestBuilder requests

Use this skill to create, modify, or review fluent `SystemOneRequestBuilder` expressions.

The primary output should normally be directly usable C#:

```csharp
var request = new SystemOneRequestBuilder()
    .WithState(...)
    .AddChoice(...)
    .AddScore(...)
    .AddNoul(...)
    .Build();
```

The goal is a small set of clear typed judgments over one shared state.

## Scope

This skill may:

- choose or improve `WithState(...)`;
- choose between `AddChoice`, `AddScore`, and `AddNoul`;
- write or improve question IDs and instructions;
- define Choice option labels/descriptions;
- define ordered Score levels;
- add Noul criteria when they clarify a yes/no boundary;
- combine independent questions over the same state;
- modify an existing builder without changing its intended meaning;
- review a builder and rewrite weak or invalid questions.

Do not use this skill to implement business logic, thresholds, model selection, HTTP/client behavior, retries, authentication, or unsupported protocol fields.

## Read the current builder first

Before editing, read `src/SystemOneSharp/SystemOneRequestBuilder.cs`, `src/SystemOneSharp/SystemOneRequestValidator.cs`, `SPEC.md`, and the builder being created or modified.

If repository behavior differs from this skill, follow the repository.

Read [references/question-design.md](references/question-design.md) for question-writing guidance, [references/systemone-requestbuilder.md](references/systemone-requestbuilder.md) for the current fluent API, and [references/review-rubric.md](references/review-rubric.md) for reviews.

## Create

1. Identify the shared state being judged.
2. Identify each independent judgment needed.
3. Pick Choice, Score, or Noul for each judgment.
4. Choose a stable ID.
5. Write a complete instruction.
6. Define options, levels, or optional Noul criteria.
7. Put independent questions over the same state into one builder.
8. End with `.Build()`.

## Modify

Read the entire existing builder first. Preserve meaning unless the requested change explicitly changes it.

Keep IDs and Choice labels stable unless a rename is needed, treat Score level order as semantic, and phrase Noul so a high value naturally means yes.

## Review / improve

Look for wrong primitives, vague or compound questions, weak Choice descriptions, overlapping Score levels, confusing Noul polarity, missing fallback Choice options, IDs carrying meaning absent from instructions, unnecessary separate requests over the same state, and invalid counts.

When improvements are requested, show the improved fluent builder.

## State

State is the evidence all questions judge.

Prefer a string when the input is naturally one piece of text:

```csharp
.WithState("Please refund my duplicate charge")
```

Use structured JSON only when multiple named pieces of context genuinely help.

Do not put the question itself into State. State is evidence; questions are judgments.

## Choice

Use Choice when exactly one option should be selected from a defined set.

```csharp
.AddChoice("team", "Which team should handle this?", choice => choice
    .Option("billing", "Payments and refunds")
    .Option("support", "Product issues"))
```

Use stable short labels suitable for code. Descriptions should distinguish neighboring options. If none may fit, add a fallback such as `other`. Do not use Choice when several labels may independently be true.

## Score

Use Score for one ordered dimension.

```csharp
.AddScore("urgency", "How urgent is this?", score => score
    .Level("Routine; no time pressure")
    .Level("Needs attention soon")
    .Level("Time-sensitive or likely to escalate"))
```

Level order matters. Each level should describe a distinct point on the same dimension. Do not combine multiple dimensions in one Score.

## Noul

Use Noul for one yes/no proposition.

```csharp
.AddNoul("refund", "Does the customer explicitly request a refund?")
```

Phrase it positively so a high result means yes. Add criteria only when the yes/no boundary needs clarification.

Do not combine independent conditions into one Noul.

## IDs and instructions

IDs are for code. Instructions carry the complete model-facing meaning.

Poor:

```csharp
.AddNoul("refund_requested", "Is this true?")
```

Better:

```csharp
.AddNoul("refund_requested", "Does the customer explicitly request a refund?")
```

Use short stable IDs such as `team`, `urgency`, and `refund_requested`.

Instructions should ask one direct judgment. Do not ask for reasoning or broad analysis.

## Batch questions

If several questions judge the same State and none depends on another answer, put them in the same builder.

Use a later request only when an earlier answer genuinely determines new state or new options.

## Current builder limits

The current validator requires:

- State: JSON string, object, or array;
- at least one question;
- Choice: 2 to 255 options;
- Score: 2 to 10 levels;
- Noul criteria: absent or exactly `true` and `false`.

Duplicate question IDs and duplicate Choice labels are rejected.

Always call `.Build()`.

## Output

For create/modify work, return the finished fluent builder first. Add explanation only when it helps justify primitive selection, decomposition, wording, batching, or a validation constraint.

For review-only work, identify concrete weaknesses and show an improved builder when practical.
