# SystemOneSharp

A .NET 10 HTTP client for TypeSafe Jev and the Jev-compatible local Laya server. It sends a shared `POST /v1/systemone` request and returns typed Choice, Score, and Noul answers.

```csharp
using SystemOneSharp;

using var http = new HttpClient();
ISystemOneClient client = new SystemOneClient(http, new SystemOneOptions
{
    ApiKey = Environment.GetEnvironmentVariable("TYPESAFE_API_KEY")
});

var request = new SystemOneRequestBuilder()
    .WithState("Please refund my duplicate charge")
    .AddChoice("team", "Which team should handle this?", choice => choice
        .Option("billing", "Payments and refunds")
        .Option("support", "Product issues"))
    .AddScore("urgency", "How urgent is this?", score => score
        .Level("routine")
        .Level("soon")
        .Level("critical"))
    .AddNoul("refund", "Does the customer ask for a refund?")
    .Build();

var response = await client.DecideAsync(request);
var team = response.GetChoice("team");
var urgency = response.GetScore("urgency");
var refundProbability = response.GetNoul("refund").Noul;
```

For local Laya, set `BaseUri = new Uri("http://127.0.0.1:8000")` and omit `ApiKey` unless the server requires one. Set `Model` to a Laya checkpoint name to override its automatic routing. The caller owns the `HttpClient` and decides how to use probabilities and confidence.

`WithState` also accepts a `JsonNode` or a serializable C# object. For structured question instructions or criteria, use the `JsonNode` instruction overloads and `OptionJson`, `LevelJson`, or `CriteriaJson`. `Build()` checks the same request rules as `DecideAsync` and returns a separate snapshot each time. You can still construct `SystemOneRequest` and question objects directly. The typed answer methods throw for a missing ID or a mismatched answer type.

Run verification with `dotnet run --project tests/SystemOneSharp.Verification -c Release`. The harness uses an in-memory HTTP handler, so it needs no API key or local server.

## Console example

Start a local Laya server on `http://127.0.0.1:8000`, then run:

```powershell
dotnet run --project examples/SystemOneSharp.Example -c Release
```

The example sends one combined request with Choice, Score, and Noul questions, then sends each question separately. It prints the selected team, score rubric, answer probabilities, confidence where available, model, token usage, each call's elapsed time, and total run time. Each run makes four API calls. The combined call runs first, so its time may include server warmup. Change `examples/SystemOneSharp.Example/appsettings.json` to use another endpoint. For hosted Jev, set `BaseUri` to `https://api.typesafe.ai/`, `Model` to `jev-latest`, and `ApiKeyEnvironmentVariable` to `TYPESAFE_API_KEY`; set that environment variable outside the JSON file before running.
