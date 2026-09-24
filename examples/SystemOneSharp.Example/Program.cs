using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using SystemOneSharp;

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
    var request = new SystemOneRequest
    {
        State = JsonValue.Create(ticket)!,
        Questions = new Dictionary<string, SystemOneQuestion>
        {
            ["department"] = new ChoiceQuestion("Which team should handle this ticket?", new Dictionary<string, string?>
            {
                ["billing"] = "Payments, invoices, and refunds",
                ["technical"] = "Bugs and product failures",
                ["account"] = "Login and account access"
            }),
            ["urgency"] = new ScoreQuestion("How urgent is this ticket?", [
                "Routine; no time pressure",
                "Needs attention soon",
                "Time-sensitive or likely to escalate"
            ]),
            ["refund_requested"] = new NoulQuestion("Does the customer explicitly request a refund?")
        }
    };

    PrintHeading("System One decision example");
    Console.WriteLine($"Ticket: {ticket}");
    Console.WriteLine();

    var response = await client.DecideAsync(request);
    var choice = (ChoiceAnswer)response.Answers["department"];
    var score = (ScoreAnswer)response.Answers["urgency"];
    var noul = (NoulAnswer)response.Answers["refund_requested"];

    PrintHeading("Choice · handling team");
    Console.WriteLine($"Selected: {choice.Choice}   Confidence: {Percent(choice.Confidence)}");
    foreach (var (label, probability) in choice.Probabilities.OrderByDescending(pair => pair.Value))
        PrintBar(label, probability);
    Console.WriteLine();

    PrintHeading("Score · urgency");
    Console.WriteLine($"Score: {score.Score.ToString("0.00", CultureInfo.InvariantCulture)} / {score.Legend.Count - 1}   Confidence: {Percent(score.Confidence)}");
    foreach (var (level, description) in score.Legend.OrderBy(pair => int.Parse(pair.Key, CultureInfo.InvariantCulture)))
        PrintBar($"{level}: {description}", score.Probabilities.GetValueOrDefault(level));
    Console.WriteLine();

    PrintHeading("Noul · refund requested");
    PrintBar("P(yes)", noul.Noul);
    Console.WriteLine();

    Console.WriteLine($"Model: {response.Model}   Tokens: {response.Usage.InputTokens} in / {response.Usage.OutputTokens} out");
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
