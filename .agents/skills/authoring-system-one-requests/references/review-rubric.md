# Review rubric

Review only the fluent `SystemOneRequestBuilder` definition.

## State

- Is State the evidence the questions need?
- Is unrelated context included?
- Would a simple structured object be clearer than an ambiguous concatenated string?

## Primitive

For every question:

- Choice = one option from a set.
- Score = one ordered dimension.
- Noul = one yes/no proposition.

Flag mismatches.

## Atomicity

Does each question have one stable meaning? Split questions that combine independent judgments.

## IDs

Check that IDs are concise, stable, nonblank, unique, and suitable for code lookup. The instruction must still contain the full meaning.

## Instructions

Check for a direct complete question, explicit subject, one judgment, no vague "analyze this", and no request for reasoning/explanation.

## Choice

Check 2–255 options, unique stable labels, distinguishing descriptions, a fallback when none may fit, and that only one option should win.

## Score

Check 2–10 levels, one dimension, meaningful low-to-high order, distinct neighboring levels, and descriptions stronger than unexplained low/medium/high.

## Noul

Check one proposition, positive polarity where possible, high value clearly means yes, no combined A-and-B judgment, and criteria only when useful.

## Batching

If questions use the same State and do not depend on each other's answers, keep them in one builder.

## Validity

Confirm State exists, at least one question exists, counts are valid, IDs and Choice labels are unique, and `.Build()` is present.

## Review output

State each concrete issue and then show the improved fluent builder when practical.
