# SystemOneSharp

[![CI](https://github.com/pinkroosterai/SystemOneSharp/actions/workflows/ci.yml/badge.svg)](https://github.com/pinkroosterai/SystemOneSharp/actions/workflows/ci.yml) [![NuGet](https://img.shields.io/nuget/vpre/SystemOneSharp.svg)](https://www.nuget.org/packages/SystemOneSharp)

A .NET 10 HTTP client for TypeSafe Jev and the Jev-compatible local Laya server. It sends a shared `POST /v1/systemone` request and returns typed Choice, Score, and Noul answers. The library has no runtime package dependencies.

This is an unofficial, community-maintained client. It is not affiliated with or endorsed by TypeSafe or the Laya project.

## Install and requirements

Install the package from [NuGet](https://www.nuget.org/packages/SystemOneSharp) for .NET 10:

```text
dotnet add package SystemOneSharp --prerelease
```

The client needs an endpoint compatible with the System One API. Hosted Jev requires a key; the local Laya server requires a key only if configured to do so. Keep keys in environment variables.

## Quick start

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

For local Laya, set `BaseUri = new Uri("http://127.0.0.1:8000")` and omit `ApiKey` unless the server requires one. Set `Model` to a Laya checkpoint name to override its automatic routing, or call `WithModel(...)` on the builder to override the configured model for one request. The caller owns the `HttpClient` and decides how to use probabilities and confidence.

`WithState` also accepts a `JsonNode`, a `JsonElement`, or a serializable C# object; pass a `JsonTypeInfo<T>` from a `JsonSerializerContext` to serialize without reflection. For structured question instructions or criteria, use the `JsonNode` instruction overloads and `OptionJson`, `LevelJson`, or `CriteriaJson`. `Build()` checks the same request rules as `DecideAsync` and returns a separate snapshot each time. You can still construct `SystemOneRequest` and question objects directly. The typed answer methods throw for a missing ID or a mismatched answer type.

`SystemOneOptions` also controls `MaxRetries` and `InitialRetryDelay`. The client retries HTTP 429 and 529, honors `Retry-After`, and otherwise returns typed transport, API, or protocol exceptions. The caller's cancellation token stops the request or retry delay. Response fields outside the contract, such as Laya's `routing`, are kept in `SystemOneResponse.AdditionalProperties` without validation. See [SPEC.md](https://github.com/pinkroosterai/SystemOneSharp/blob/master/SPEC.md) for the full contract.

Each `DecideAsync` call is traced through an `ActivitySource` named `SystemOneDiagnostics.ActivitySourceName` (`"SystemOneSharp"`), using OpenTelemetry GenAI attribute names where they apply: model, question counts per type, retry count, token usage, and outcome. State, instructions, criteria, answers, and the API key are never recorded.

## Microsoft AI integrations

Three optional packages connect the client to Microsoft's AI stack. Each one calls `ISystemOneClient.DecideAsync`, so retries, validation and tracing come from the core package.

| Package | What it adds |
|---|---|
| `SystemOneSharp.Extensions.AI` | One canonical projection of `ChatMessage` conversations into System One state. [Guide](https://github.com/pinkroosterai/SystemOneSharp/blob/master/docs/microsoft-extensions-ai.md) |
| `SystemOneSharp.Extensions.AI.Evaluation` | `SystemOneEvaluator`: several MEAI evaluation metrics from one System One request. [Guide](https://github.com/pinkroosterai/SystemOneSharp/blob/master/docs/evaluation.md) |
| `SystemOneSharp.AgentFramework` | A completion `LoopEvaluator` and function-calling middleware for Microsoft Agent Framework. [Guide](https://github.com/pinkroosterai/SystemOneSharp/blob/master/docs/agent-framework.md) |

The core package does not depend on any of them.

## Build and verify

```text
dotnet build SystemOneSharp.slnx -c Release
dotnet run --project tests/SystemOneSharp.Verification -c Release
dotnet run --project tests/SystemOneSharp.Integrations.Verification -c Release
dotnet pack SystemOneSharp.slnx -c Release
```

The core harness uses an in-memory HTTP handler and the integration harness uses a fake client, so neither needs an API key or a local server. The examples below do make live calls.

## Console example

Start a local Laya server on `http://127.0.0.1:8000`, then run:

```powershell
dotnet run --project examples/SystemOneSharp.Example -c Release
```

The example sends one combined request with Choice, Score, and Noul questions, then sends each question separately. It prints the selected team, score rubric, answer probabilities, confidence where available, model, token usage, each call's elapsed time, and total run time. Each run makes four API calls. The combined call runs first, so its time may include server warmup. Change `examples/SystemOneSharp.Example/appsettings.json` to use another endpoint. For hosted Jev, set `BaseUri` to `https://api.typesafe.ai/`, `Model` to `jev-latest`, and `ApiKeyEnvironmentVariable` to `TYPESAFE_API_KEY`; set that environment variable outside the JSON file before running.

`examples/SystemOneSharp.MicrosoftAI.Example` uses the same `appsettings.json` settings. It projects a conversation, evaluates a response, runs the completion loop evaluator and the function-call gate, and routes tickets through a Microsoft Agent Framework workflow. Its chat model is a scripted stand-in, so only the System One calls are live.

## Contributing and security

See [CONTRIBUTING.md](https://github.com/pinkroosterai/SystemOneSharp/blob/master/CONTRIBUTING.md) for development and pull requests, [SECURITY.md](https://github.com/pinkroosterai/SystemOneSharp/blob/master/SECURITY.md) for private vulnerability reporting, and [LICENSE](https://github.com/pinkroosterai/SystemOneSharp/blob/master/LICENSE) for the MIT terms.
