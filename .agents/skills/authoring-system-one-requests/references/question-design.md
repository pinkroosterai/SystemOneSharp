# Question design

Use this reference only for writing `SystemOneRequestBuilder` definitions.

## State

State is shared evidence. Use a plain string for one simple text input. Use structured JSON only when several named pieces of evidence matter. Keep facts in State and judgments in questions.

## Choice

Use Choice for one-of-many selection: routing, intent, category, or another closed set.

Labels should be short and stable because code receives them. Descriptions should distinguish options.

Weak:

```csharp
.Option("billing", "Billing")
.Option("support", "Support")
```

Better:

```csharp
.Option("billing", "Payments, invoices, charges, and refunds")
.Option("support", "Product usage, bugs, and technical issues")
```

Add `other` or `none` when real inputs may match none of the listed options.

If multiple labels may independently be true, Choice is probably the wrong primitive.

## Score

Use Score for one ordered spectrum such as urgency, severity, relevance, or quality.

Order is meaningful.

Weak:

```csharp
.Level("low")
.Level("medium")
.Level("high")
```

Better:

```csharp
.Level("Routine; no time pressure")
.Level("Needs attention soon")
.Level("Time-sensitive or likely to escalate")
```

Each level should stand on its own and remain on one dimension.

## Noul

Use Noul for one yes/no proposition.

Good:

```csharp
.AddNoul("refund_requested", "Does the customer explicitly request a refund?")
```

Poor:

```csharp
.AddNoul("refund_requested", "Is the customer angry and asking for a refund?")
```

The poor version mixes two judgments.

Prefer positive polarity so a high result means the condition is present. Add true/false criteria only when they make the boundary clearer.

## Atomic questions

Each question should produce one reusable judgment.

Instead of `How urgent is this and which team should handle it?`, use a Score and a Choice.

Instead of `Is this abusive and threatening?`, use two Nouls when both conditions matter separately.

## Batch independent questions

Questions over the same State should normally share one builder/request.

```csharp
var request = new SystemOneRequestBuilder()
    .WithState(ticket)
    .AddChoice("team", ...)
    .AddScore("urgency", ...)
    .AddNoul("refund_requested", ...)
    .Build();
```

Use separate requests only for real sequential dependencies.

## Upstream guidance

TypeSafe's current skill similarly emphasizes narrow judgments, choosing Choice/Score/Noul by answer meaning, and asking independent questions over the same state together:

https://github.com/typesafe-ai/skills/tree/main/skills/typesafe-ai
