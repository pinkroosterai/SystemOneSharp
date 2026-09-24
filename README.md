# SystemOneSharp

A .NET 10 HTTP client for TypeSafe Jev and the Jev-compatible local Laya server. It sends a shared `POST /v1/systemone` request and returns typed Choice, Score, and Noul answers.

```csharp
using System.Text.Json.Nodes;
using SystemOneSharp;

using var http = new HttpClient();
ISystemOneClient client = new SystemOneClient(http, new SystemOneOptions
{
    ApiKey = Environment.GetEnvironmentVariable("TYPESAFE_API_KEY")
});

var response = await client.DecideAsync(new SystemOneRequest
{
    State = JsonValue.Create("Please refund my duplicate charge")!,
    Questions = new Dictionary<string, SystemOneQuestion>
    {
        ["team"] = new ChoiceQuestion("Which team should handle this?", new Dictionary<string, string?>
        {
            ["billing"] = "Payments and refunds",
            ["support"] = "Product issues"
        }),
        ["urgency"] = new ScoreQuestion("How urgent is this?", ["routine", "soon", "critical"]),
        ["refund"] = new NoulQuestion("Does the customer ask for a refund?")
    }
});

var team = (ChoiceAnswer)response.Answers["team"];
var urgency = (ScoreAnswer)response.Answers["urgency"];
var refundProbability = ((NoulAnswer)response.Answers["refund"]).Noul;
```

For local Laya, set `BaseUri = new Uri("http://127.0.0.1:8000")` and omit `ApiKey` unless the server requires one. Set `Model` to a Laya checkpoint name to override its automatic routing. The caller owns the `HttpClient` and decides how to use probabilities and confidence.

Run verification with `dotnet run --project tests/SystemOneSharp.Verification -c Release`. The harness uses an in-memory HTTP handler, so it needs no API key or local server.

## Console example

Start a local Laya server on `http://127.0.0.1:8000`, then run:

```powershell
dotnet run --project examples/SystemOneSharp.Example -c Release
```

The example sends one support ticket with Choice, Score, and Noul questions. It prints the selected team, score rubric, answer probabilities, confidence where available, model, and token usage. Change `examples/SystemOneSharp.Example/appsettings.json` to use another endpoint. For hosted Jev, set `BaseUri` to `https://api.typesafe.ai/`, `Model` to `jev-latest`, and `ApiKeyEnvironmentVariable` to `TYPESAFE_API_KEY`; set that environment variable outside the JSON file before running.
