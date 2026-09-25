using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;

namespace SystemOneSharp.Extensions.AI.Evaluation;

/// <summary>An <see cref="IEvaluator"/> that answers every configured question with one System One request.</summary>
/// <remarks>
/// <para>
/// The state is the conversation plus the model response, projected by <see cref="SystemOneAiState"/>.
/// The caller adds the questions; the metric map turns each answer into a metric. Values are the model's
/// numbers as returned; confidence and probability distributions are kept in each metric's
/// <see cref="EvaluationMetric.Metadata"/>.
/// </para>
/// <para>
/// <see cref="ChatConfiguration"/> is not used: no generative model is involved. <c>additionalContext</c> is
/// ignored. Exceptions from <see cref="ISystemOneClient.DecideAsync"/> (API, transport, protocol, cancellation)
/// propagate unchanged rather than becoming metric diagnostics, so a failed decision is never reported as a score.
/// </para>
/// </remarks>
public sealed class SystemOneEvaluator : IEvaluator
{
    /// <summary>Metadata key holding the Score or Choice answer's confidence.</summary>
    public const string ConfidenceMetadataName = "systemone-confidence";
    /// <summary>Metadata key holding the Score or Choice answer's probability distribution as a JSON object.</summary>
    public const string ProbabilitiesMetadataName = "systemone-probabilities";

    // Same names as the built-in evaluators' internal BuiltInMetricUtilities constants, so reports show them together.
    private const string ModelMetadataName = "eval-model";
    private const string InputTokensMetadataName = "eval-input-tokens";
    private const string OutputTokensMetadataName = "eval-output-tokens";
    private const string TotalTokensMetadataName = "eval-total-tokens";
    private const string DurationMetadataName = "eval-duration-ms";

    private readonly ISystemOneClient _client;
    private readonly Action<SystemOneRequestBuilder> _configureRequest;
    private readonly IReadOnlyList<SystemOneMetricMapping> _mappings;
    private readonly SystemOneAiStateOptions? _stateOptions;

    /// <summary>Creates an evaluator.</summary>
    /// <param name="client">The client every evaluation calls.</param>
    /// <param name="configureRequest">Adds the questions (and optionally a model) to a builder whose state is already set.</param>
    /// <param name="configureMetrics">Maps question IDs onto metrics.</param>
    /// <param name="stateOptions">Options for the conversation projection.</param>
    public SystemOneEvaluator(ISystemOneClient client, Action<SystemOneRequestBuilder> configureRequest,
        Action<SystemOneMetricMap> configureMetrics, SystemOneAiStateOptions? stateOptions = null)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _configureRequest = configureRequest ?? throw new ArgumentNullException(nameof(configureRequest));
        ArgumentNullException.ThrowIfNull(configureMetrics);
        var map = new SystemOneMetricMap();
        configureMetrics(map);
        if (map.Mappings.Count == 0)
            throw new ArgumentException("At least one metric must be mapped.", nameof(configureMetrics));
        _mappings = map.Mappings.ToArray();
        _stateOptions = stateOptions;
        EvaluationMetricNames = _mappings.Select(mapping => mapping.MetricName).ToArray();
    }

    public IReadOnlyCollection<string> EvaluationMetricNames { get; }

    public async ValueTask<EvaluationResult> EvaluateAsync(IEnumerable<ChatMessage> messages, ChatResponse modelResponse,
        ChatConfiguration? chatConfiguration = null, IEnumerable<EvaluationContext>? additionalContext = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(messages);
        ArgumentNullException.ThrowIfNull(modelResponse);

        var builder = new SystemOneRequestBuilder().WithConversation(messages.Concat(modelResponse.Messages), _stateOptions);
        _configureRequest(builder);
        var request = builder.Build();
        foreach (var mapping in _mappings)
        {
            if (!request.Questions.TryGetValue(mapping.QuestionId, out var question) || question.GetType() != mapping.QuestionType)
                throw new InvalidOperationException(
                    $"Metric '{mapping.MetricName}' expects a {mapping.QuestionType.Name} with ID '{mapping.QuestionId}' in the request.");
        }

        var stopwatch = Stopwatch.StartNew();
        var response = await _client.DecideAsync(request, cancellationToken).ConfigureAwait(false);
        var duration = stopwatch.Elapsed;

        return new EvaluationResult(_mappings.Select(mapping => CreateMetric(mapping, response, duration)));
    }

    private static EvaluationMetric CreateMetric(SystemOneMetricMapping mapping, SystemOneResponse response, TimeSpan duration)
    {
        EvaluationMetric metric;
        object value;
        switch (response.Answers[mapping.QuestionId])
        {
            case NoulAnswer noul:
                metric = new NumericMetric(mapping.MetricName, noul.Noul);
                value = noul.Noul;
                break;
            case ScoreAnswer score:
                metric = new NumericMetric(mapping.MetricName, score.Score);
                value = score.Score;
                AddDistribution(metric, score.Confidence, score.Probabilities);
                break;
            case ChoiceAnswer choice:
                metric = new StringMetric(mapping.MetricName, choice.Choice);
                value = choice.Choice;
                AddDistribution(metric, choice.Confidence, choice.Probabilities);
                break;
            default:
                throw new InvalidOperationException($"Answer '{mapping.QuestionId}' has an unsupported type.");
        }

        metric.Interpretation = mapping.Interpret?.Invoke(value);
        metric.AddOrUpdateMetadata(ModelMetadataName, response.Model);
        metric.AddOrUpdateMetadata(InputTokensMetadataName, response.Usage.InputTokens.ToString(CultureInfo.InvariantCulture));
        metric.AddOrUpdateMetadata(OutputTokensMetadataName, response.Usage.OutputTokens.ToString(CultureInfo.InvariantCulture));
        metric.AddOrUpdateMetadata(TotalTokensMetadataName,
            (response.Usage.InputTokens + response.Usage.OutputTokens).ToString(CultureInfo.InvariantCulture));
        metric.AddOrUpdateMetadata(DurationMetadataName, duration.TotalMilliseconds.ToString("F2", CultureInfo.InvariantCulture));
        return metric;
    }

    private static void AddDistribution(EvaluationMetric metric, double confidence, IReadOnlyDictionary<string, double> probabilities)
    {
        metric.AddOrUpdateMetadata(ConfidenceMetadataName, confidence.ToString("R", CultureInfo.InvariantCulture));
        metric.AddOrUpdateMetadata(ProbabilitiesMetadataName, JsonSerializer.Serialize(probabilities, SystemOneEvaluationJsonContext.Default.IReadOnlyDictionaryStringDouble));
    }
}
