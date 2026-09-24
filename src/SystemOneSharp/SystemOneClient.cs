using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace SystemOneSharp;

public sealed class SystemOneClient : ISystemOneClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        AllowOutOfOrderMetadataProperties = true
    };

    private readonly HttpClient _httpClient;
    private readonly SystemOneOptions _options;
    private readonly Uri _endpoint;

    public SystemOneClient(HttpClient httpClient, SystemOneOptions options)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options ?? throw new ArgumentNullException(nameof(options));

        if (options.BaseUri is null || !options.BaseUri.IsAbsoluteUri ||
            options.BaseUri.Scheme is not ("http" or "https") ||
            !string.IsNullOrEmpty(options.BaseUri.Query) || !string.IsNullOrEmpty(options.BaseUri.Fragment))
            throw new ArgumentException("BaseUri must be an absolute HTTP(S) URI without a query or fragment.", nameof(options));
        if (string.IsNullOrWhiteSpace(options.Model))
            throw new ArgumentException("Model is required.", nameof(options));
        if (options.MaxRetries < 0 || options.InitialRetryDelay < TimeSpan.Zero)
            throw new ArgumentException("Retry settings must be nonnegative.", nameof(options));

        var baseUri = new Uri(options.BaseUri.AbsoluteUri.TrimEnd('/') + "/");
        _endpoint = new Uri(baseUri, "v1/systemone");
    }

    public async Task<SystemOneResponse> DecideAsync(SystemOneRequest request, CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);
        var payload = JsonSerializer.SerializeToUtf8Bytes(new
        {
            state = request.State,
            model = _options.Model,
            questions = request.Questions
        }, JsonOptions);

        for (var attempt = 1; ; attempt++)
        {
            using var message = new HttpRequestMessage(HttpMethod.Post, _endpoint)
            {
                Content = new ByteArrayContent(payload)
            };
            message.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            if (!string.IsNullOrEmpty(_options.ApiKey))
                message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);

            HttpResponseMessage response;
            try
            {
                response = await _httpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new SystemOneTransportException("System One request timed out.", new TimeoutException("HTTP request timed out."));
            }
            catch (HttpRequestException ex)
            {
                throw new SystemOneTransportException("System One request failed during transport.", ex);
            }

            using (response)
            {
                if (response.StatusCode is (HttpStatusCode)429 or (HttpStatusCode)529 && attempt <= _options.MaxRetries)
                {
                    var delay = RetryDelay(response.Headers.RetryAfter, attempt);
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                    throw new SystemOneApiException(response.StatusCode, Sanitize(body), attempt);

                try
                {
                    using var document = JsonDocument.Parse(body);
                    ValidateResponse(document.RootElement, request);
                    return JsonSerializer.Deserialize<SystemOneResponse>(body, JsonOptions)
                        ?? throw new SystemOneProtocolException("System One returned an empty response.");
                }
                catch (JsonException ex)
                {
                    throw new SystemOneProtocolException("System One returned invalid JSON or an unsupported answer type.", ex);
                }
            }
        }
    }

    private TimeSpan RetryDelay(RetryConditionHeaderValue? retryAfter, int attempt)
    {
        if (retryAfter?.Delta is { } delta)
            return delta > TimeSpan.Zero ? delta : TimeSpan.Zero;
        if (retryAfter?.Date is { } date)
        {
            var remaining = date - DateTimeOffset.UtcNow;
            return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
        }
        return TimeSpan.FromMilliseconds(Math.Min(_options.InitialRetryDelay.TotalMilliseconds * Math.Pow(2, attempt - 1), 30_000));
    }

    private string Sanitize(string body)
    {
        if (!string.IsNullOrEmpty(_options.ApiKey))
            body = body.Replace(_options.ApiKey, "[redacted]", StringComparison.Ordinal);
        return body.Length <= 4096 ? body : body[..4096];
    }

    private static void ValidateRequest(SystemOneRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.State is null || !IsStructuredOrString(request.State))
            throw new ArgumentException("State must be a JSON string, object, or array.", nameof(request));
        if (request.Questions is null || request.Questions.Count == 0)
            throw new ArgumentException("At least one question is required.", nameof(request));

        foreach (var (id, question) in request.Questions)
        {
            if (string.IsNullOrWhiteSpace(id) || question is null ||
                question.Instructions is null || !IsStructuredOrString(question.Instructions))
                throw new ArgumentException($"Question '{id}' needs a valid ID and instructions.", nameof(request));

            switch (question)
            {
                case ChoiceQuestion choice when choice.Criteria is { Count: >= 2 and <= 255 } &&
                    choice.Criteria.All(pair => !string.IsNullOrWhiteSpace(pair.Key) &&
                        (pair.Value is null || IsStructuredOrString(pair.Value))):
                    break;
                case ScoreQuestion score when score.Criteria is { Count: >= 2 and <= 10 } &&
                    score.Criteria.All(item => item is not null && IsStructuredOrString(item)):
                    break;
                case NoulQuestion noul when noul.Criteria is null ||
                    noul.Criteria.Count == 2 && noul.Criteria.ContainsKey("true") &&
                    noul.Criteria.ContainsKey("false") && noul.Criteria.Values.All(IsStructuredOrString):
                    break;
                default:
                    throw new ArgumentException($"Question '{id}' has invalid criteria.", nameof(request));
            }
        }
    }

    private static bool IsStructuredOrString(JsonNode node) =>
        node.GetValueKind() is JsonValueKind.String or JsonValueKind.Object or JsonValueKind.Array;

    private static void ValidateResponse(JsonElement root, SystemOneRequest request)
    {
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("model", out var model) || model.ValueKind != JsonValueKind.String ||
            !root.TryGetProperty("answers", out var answers) || answers.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("usage", out var usage) || usage.ValueKind != JsonValueKind.Object ||
            !NonnegativeInteger(usage, "input_tokens") || !NonnegativeInteger(usage, "output_tokens"))
            throw new SystemOneProtocolException("System One response is missing model, answers, or usage.");

        foreach (var (id, question) in request.Questions)
        {
            if (!answers.TryGetProperty(id, out var answer) || answer.ValueKind != JsonValueKind.Object ||
                !answer.TryGetProperty("type", out var type) || type.ValueKind != JsonValueKind.String)
                throw new SystemOneProtocolException($"Answer '{id}' is missing or malformed.");

            switch (question)
            {
                case ChoiceQuestion when type.GetString() == "choice" &&
                    answer.TryGetProperty("choice", out var selected) && selected.ValueKind == JsonValueKind.String &&
                    answer.TryGetProperty("probabilities", out var choiceProbabilities) &&
                    ValidProbabilities(choiceProbabilities) &&
                    choiceProbabilities.TryGetProperty(selected.GetString()!, out _) &&
                    UnitNumber(answer, "confidence"):
                    break;
                case ScoreQuestion when type.GetString() == "score" &&
                    FiniteNumber(answer, "score") && UnitNumber(answer, "confidence") &&
                    answer.TryGetProperty("legend", out var legend) && legend.ValueKind == JsonValueKind.Object &&
                    legend.EnumerateObject().Any() && legend.EnumerateObject().All(item => item.Value.ValueKind == JsonValueKind.String) &&
                    answer.TryGetProperty("probabilities", out var scoreProbabilities) &&
                    ValidProbabilities(scoreProbabilities):
                    break;
                case NoulQuestion when type.GetString() == "noul" && UnitNumber(answer, "noul"):
                    break;
                default:
                    throw new SystemOneProtocolException($"Answer '{id}' does not match its question or has invalid values.");
            }
        }
    }

    private static bool NonnegativeInteger(JsonElement value, string name) =>
        value.TryGetProperty(name, out var property) && property.TryGetInt32(out var number) && number >= 0;

    private static bool FiniteNumber(JsonElement value, string name) =>
        value.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.Number &&
        property.TryGetDouble(out var number) && double.IsFinite(number);

    private static bool UnitNumber(JsonElement value, string name) =>
        value.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.Number &&
        property.TryGetDouble(out var number) && double.IsFinite(number) && number is >= 0 and <= 1;

    private static bool ValidProbabilities(JsonElement value) =>
        value.ValueKind == JsonValueKind.Object && value.EnumerateObject().Any() &&
        value.EnumerateObject().All(item => item.Value.ValueKind == JsonValueKind.Number &&
            item.Value.TryGetDouble(out var number) && double.IsFinite(number) && number is >= 0 and <= 1);
}
