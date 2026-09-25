# SystemOneSharp.Extensions.AI.Evaluation

An [`IEvaluator`](https://learn.microsoft.com/dotnet/ai/evaluation/libraries) for Microsoft.Extensions.AI.Evaluation. It sends one System One request per evaluation and reports each answer as a metric. Several metrics cost one inference.

```text
dotnet add package SystemOneSharp.Extensions.AI.Evaluation --prerelease
```

It depends on `SystemOneSharp`, `SystemOneSharp.Extensions.AI` and `Microsoft.Extensions.AI.Evaluation`. It does not depend on Agent Framework.

## Use

```csharp
using Microsoft.Extensions.AI.Evaluation;
using SystemOneSharp.Extensions.AI.Evaluation;

var evaluator = new SystemOneEvaluator(client,
    request => request
        .AddNoul("completed", "Did the assistant complete the user's task?")
        .AddScore("quality", "How good is the answer?", score => score.Level("poor").Level("fair").Level("good"))
        .AddChoice("failure", "What went wrong, if anything?", choice => choice
            .Option("none", "Nothing went wrong")
            .Option("off_topic", "The answer is off topic")),
    metrics => metrics
        .Noul("completed", "TaskCompletion", value => value >= 0.95
            ? new EvaluationMetricInterpretation(EvaluationRating.Good)
            : new EvaluationMetricInterpretation(EvaluationRating.Unacceptable, failed: true))
        .Score("quality", "Quality")
        .Choice("failure", "FailureMode"));

EvaluationResult result = await evaluator.EvaluateAsync(messages, chatResponse);
```

The state is `messages` plus `chatResponse.Messages`, built by the [shared projection](https://github.com/pinkroosterai/SystemOneSharp/blob/master/docs/microsoft-extensions-ai.md). Your delegate adds the questions, and can also call `WithModel`.

## Metrics

| Answer | Metric | Value |
|---|---|---|
| Noul | `NumericMetric` | the probability, 0–1 |
| Score | `NumericMetric` | the weighted score, 0 to levels − 1 |
| Choice | `StringMetric` | the selected label |

For Score and Choice metrics, confidence and the full distribution are kept in `Metadata` under `systemone-confidence` and `systemone-probabilities` (a JSON object). They are not separate metrics. Every metric also carries `eval-model`, `eval-input-tokens`, `eval-output-tokens`, `eval-total-tokens` and `eval-duration-ms`, which are the names the built-in evaluators use.

## Thresholds

The evaluator reports the model's numbers as returned. A verdict such as "TaskCompletion ≥ 0.95 passes" comes from the optional interpretation delegate you pass per metric, which sets MEAI's own `EvaluationMetricInterpretation`. Without a delegate, a metric has a value and no verdict.

## Failures and ignored inputs

- Exceptions from `ISystemOneClient.DecideAsync` (API, transport, protocol, cancellation) propagate unchanged, so a failed decision is never reported as a score.
- A metric mapped to a question that is missing, or of another type, throws `InvalidOperationException`.
- `ChatConfiguration` is not used, because no generative model is involved. `additionalContext` is ignored.

See also: [Agent Framework](https://github.com/pinkroosterai/SystemOneSharp/blob/master/docs/agent-framework.md), [core client](https://github.com/pinkroosterai/SystemOneSharp/blob/master/README.md).
