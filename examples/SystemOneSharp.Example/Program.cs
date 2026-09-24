using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using SystemOneSharp;

var runStarted = Stopwatch.GetTimestamp();

try
{
    var configPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
    var config = JsonSerializer.Deserialize<ExampleConfig>(await File.ReadAllTextAsync(configPath))
        ?? throw new InvalidOperationException("appsettings.json must contain a JSON object.");

    if (string.IsNullOrWhiteSpace(config.BaseUri) || string.IsNullOrWhiteSpace(config.Model))
        throw new InvalidOperationException("appsettings.json needs BaseUri and Model.");
    if (!Uri.TryCreate(config.BaseUri, UriKind.Absolute, out var baseUri))
        throw new InvalidOperationException("BaseUri in appsettings.json must be an absolute URL.");

    string? apiKey = null;
    if (config.ApiKeyEnvironmentVariable is not null)
    {
        if (string.IsNullOrWhiteSpace(config.ApiKeyEnvironmentVariable))
            throw new InvalidOperationException("ApiKeyEnvironmentVariable must name an environment variable.");
        apiKey = Environment.GetEnvironmentVariable(config.ApiKeyEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException($"Set {config.ApiKeyEnvironmentVariable} before running the example.");
    }

    using var http = new HttpClient();
    ISystemOneClient client = new SystemOneClient(http, new SystemOneOptions
    {
        BaseUri = baseUri,
        Model = config.Model,
        ApiKey = apiKey
    });

    const string ticket = "I was charged twice for March. Please refund the duplicate today or I will cancel.";
    var request = new SystemOneRequestBuilder()
        .WithState(ticket)
        .AddChoice("department", "Which team should handle this ticket?", choice => choice
            .Option("billing", "Payments, invoices, and refunds")
            .Option("technical", "Bugs and product failures")
            .Option("account", "Login and account access"))
        .AddScore("urgency", "How urgent is this ticket?", score => score
            .Level("Routine; no time pressure")
            .Level("Needs attention soon")
            .Level("Time-sensitive or likely to escalate"))
        .AddNoul("refund_requested", "Does the customer explicitly request a refund?")
        .Build();

    PrintHeading("System One decision example");
    Console.WriteLine($"Ticket: {ticket}");
    Console.WriteLine();

    PrintHeading("Combined request · all three questions");
    var (combined, combinedTime) = await TimedDecisionAsync(client, request);
    PrintChoice(combined.GetChoice("department"));
    PrintScore(combined.GetScore("urgency"));
    PrintNoul(combined.GetNoul("refund_requested"));
    PrintCallSummary(combined, combinedTime);

    PrintHeading("Separate requests · one question each");
    Console.WriteLine();

    var (choiceOnly, choiceTime) = await TimedDecisionAsync(client, SingleQuestion(request, "department"));
    PrintChoice(choiceOnly.GetChoice("department"));
    PrintCallSummary(choiceOnly, choiceTime);

    var (scoreOnly, scoreTime) = await TimedDecisionAsync(client, SingleQuestion(request, "urgency"));
    PrintScore(scoreOnly.GetScore("urgency"));
    PrintCallSummary(scoreOnly, scoreTime);

    var (noulOnly, noulTime) = await TimedDecisionAsync(client, SingleQuestion(request, "refund_requested"));
    PrintNoul(noulOnly.GetNoul("refund_requested"));
    PrintCallSummary(noulOnly, noulTime);

    Console.WriteLine($"Total console run: {Milliseconds(Stopwatch.GetElapsedTime(runStarted))} ms");
    return 0;
}
catch (SystemOneApiException ex)
{
    Console.Error.WriteLine($"Decision request failed: HTTP {(int)ex.StatusCode}. {ex.ResponseBody}");
    return 1;
}
catch (Exception ex) when (ex is IOException or JsonException or ArgumentException or InvalidOperationException
    or SystemOneTransportException or SystemOneProtocolException)
{
    Console.Error.WriteLine($"Example failed: {ex.Message}");
    return 1;
}

static string Percent(double value) => (value * 100).ToString("0.0", CultureInfo.InvariantCulture) + "%";

static string Milliseconds(TimeSpan elapsed) => elapsed.TotalMilliseconds.ToString("0.0", CultureInfo.InvariantCulture);

static SystemOneRequest SingleQuestion(SystemOneRequest source, string id) => new()
{
    State = source.State,
    Questions = new Dictionary<string, SystemOneQuestion> { [id] = source.Questions[id] }
};

static async Task<(SystemOneResponse Response, TimeSpan Elapsed)> TimedDecisionAsync(
    ISystemOneClient client, SystemOneRequest request)
{
    var started = Stopwatch.GetTimestamp();
    var response = await client.DecideAsync(request);
    return (response, Stopwatch.GetElapsedTime(started));
}

static void PrintChoice(ChoiceAnswer answer)
{
    PrintHeading("Choice · handling team");
    Console.WriteLine($"Selected: {answer.Choice}   Confidence: {Percent(answer.Confidence)}");
    foreach (var (label, probability) in answer.Probabilities.OrderByDescending(pair => pair.Value))
        PrintBar(label, probability);
    Console.WriteLine();
}

static void PrintScore(ScoreAnswer answer)
{
    PrintHeading("Score · urgency");
    Console.WriteLine($"Score: {answer.Score.ToString("0.00", CultureInfo.InvariantCulture)} / {answer.Legend.Count - 1}   Confidence: {Percent(answer.Confidence)}");
    foreach (var (level, description) in answer.Legend.OrderBy(pair => int.Parse(pair.Key, CultureInfo.InvariantCulture)))
        PrintBar($"{level}: {description}", answer.Probabilities.GetValueOrDefault(level));
    Console.WriteLine();
}

static void PrintNoul(NoulAnswer answer)
{
    PrintHeading("Noul · refund requested");
    PrintBar("P(yes)", answer.Noul);
    Console.WriteLine();
}

static void PrintCallSummary(SystemOneResponse response, TimeSpan elapsed)
{
    Console.WriteLine($"Call time: {Milliseconds(elapsed)} ms   Model: {response.Model}   Tokens: {response.Usage.InputTokens} in / {response.Usage.OutputTokens} out");
    Console.WriteLine();
}

static void PrintHeading(string text)
{
    var previous = Console.ForegroundColor;
    if (!Console.IsOutputRedirected) Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine(text);
    if (!Console.IsOutputRedirected) Console.ForegroundColor = previous;
}

static void PrintBar(string label, double probability)
{
    var filled = (int)Math.Round(probability * 20);
    Console.WriteLine($"  {label,-44} {new string('█', filled)}{new string('░', 20 - filled)} {Percent(probability),6}");
}

internal sealed class ExampleConfig
{
    public string? BaseUri { get; init; }
    public string? Model { get; init; }
    public string? ApiKeyEnvironmentVariable { get; init; }
}
