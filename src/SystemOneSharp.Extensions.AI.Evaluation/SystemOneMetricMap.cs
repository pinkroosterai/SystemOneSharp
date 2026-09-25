using Microsoft.Extensions.AI.Evaluation;

namespace SystemOneSharp.Extensions.AI.Evaluation;

/// <summary>Maps System One question IDs onto evaluation metrics for <see cref="SystemOneEvaluator"/>.</summary>
/// <remarks>
/// Noul and Score answers become <see cref="NumericMetric"/>s; Choice answers become <see cref="StringMetric"/>s.
/// An interpretation delegate is the place for thresholds such as "completion ≥ 0.95 passes"; without one the
/// metric carries the model's value and no verdict.
/// </remarks>
public sealed class SystemOneMetricMap
{
    private readonly List<SystemOneMetricMapping> _mappings = new();

    internal IReadOnlyList<SystemOneMetricMapping> Mappings => _mappings;

    /// <summary>Reports a Noul answer's probability as a numeric metric.</summary>
    public SystemOneMetricMap Noul(string questionId, string metricName, Func<double, EvaluationMetricInterpretation?>? interpret = null) =>
        Add(new SystemOneMetricMapping(questionId, metricName, typeof(NoulQuestion), interpret is null ? null : value => interpret((double)value)));

    /// <summary>Reports a Score answer's weighted score as a numeric metric.</summary>
    public SystemOneMetricMap Score(string questionId, string metricName, Func<double, EvaluationMetricInterpretation?>? interpret = null) =>
        Add(new SystemOneMetricMapping(questionId, metricName, typeof(ScoreQuestion), interpret is null ? null : value => interpret((double)value)));

    /// <summary>Reports a Choice answer's selected label as a string metric.</summary>
    public SystemOneMetricMap Choice(string questionId, string metricName, Func<string, EvaluationMetricInterpretation?>? interpret = null) =>
        Add(new SystemOneMetricMapping(questionId, metricName, typeof(ChoiceQuestion), interpret is null ? null : value => interpret((string)value)));

    private SystemOneMetricMap Add(SystemOneMetricMapping mapping)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mapping.QuestionId, "questionId");
        ArgumentException.ThrowIfNullOrWhiteSpace(mapping.MetricName, "metricName");
        if (_mappings.Any(existing => existing.MetricName == mapping.MetricName))
            throw new ArgumentException($"Metric '{mapping.MetricName}' is already mapped.", "metricName");
        _mappings.Add(mapping);
        return this;
    }
}

internal sealed record SystemOneMetricMapping(
    string QuestionId,
    string MetricName,
    Type QuestionType,
    Func<object, EvaluationMetricInterpretation?>? Interpret);
