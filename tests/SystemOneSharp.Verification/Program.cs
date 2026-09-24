using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using SystemOneSharp;

const string success = """
    {"model":"jev-1.13.0","answers":{
      "department":{"type":"choice","choice":"billing","probabilities":{"billing":0.9,"support":0.1},"confidence":0.8},
      "urgency":{"type":"score","score":1.25,"legend":{"0":"routine","1":"soon","2":"critical"},"probabilities":{"0":0,"1":0.75,"2":0.25},"confidence":0.7},
      "refund":{"type":"noul","noul":0.95}},
      "usage":{"input_tokens":42,"output_tokens":12},"routing":{"model":"english"}}
    """;

var request = new SystemOneRequest
{
    State = JsonNode.Parse("""{"body":"Refund needed"}""")!,
    Questions = new Dictionary<string, SystemOneQuestion>
    {
        ["department"] = new ChoiceQuestion("Which team?", new Dictionary<string, string?> { ["billing"] = "refunds", ["support"] = "bugs" }),
        ["urgency"] = new ScoreQuestion("How urgent?", ["routine", "soon", "critical"]),
        ["refund"] = new NoulQuestion("Does the customer ask for a refund?")
    }
};

var mixedHandler = new StubHandler(async (message, cancellationToken) =>
{
    Check(message.Method == HttpMethod.Post, "POST method");
    Check(message.RequestUri!.ToString() == "https://api.typesafe.ai/v1/systemone", "Jev endpoint");
    Check(message.Headers.Authorization?.ToString() == "Bearer test-key", "bearer header");
    Check(message.Content!.Headers.ContentType?.MediaType == "application/json", "content type");
    using var body = JsonDocument.Parse(await message.Content.ReadAsStringAsync());
    var root = body.RootElement;
    Check(root.GetProperty("model").GetString() == "jev-latest", "model serialized");
    Check(root.GetProperty("state").GetProperty("body").GetString() == "Refund needed", "state serialized");
    var questions = root.GetProperty("questions");
    Check(questions.GetProperty("department").GetProperty("type").GetString() == "choice", "choice serialized");
    Check(questions.GetProperty("department").GetProperty("criteria").GetProperty("billing").GetString() == "refunds", "choice criteria");
    Check(questions.GetProperty("urgency").GetProperty("criteria")[2].GetString() == "critical", "score order");
    Check(questions.GetProperty("refund").GetProperty("type").GetString() == "noul", "noul serialized");
    Check(!questions.GetProperty("refund").TryGetProperty("criteria", out _), "optional noul criteria omitted");
    return JsonResponse(HttpStatusCode.OK, success);
});
using (var http = new HttpClient(mixedHandler))
{
    var result = await new SystemOneClient(http, new SystemOneOptions { ApiKey = "test-key" }).DecideAsync(request);
    Check(result.Answers["department"] is ChoiceAnswer { Choice: "billing" }, "choice parsed");
    Check(result.Answers["urgency"] is ScoreAnswer { Score: 1.25 }, "score parsed");
    Check(result.Answers["refund"] is NoulAnswer { Noul: 0.95 }, "noul parsed");
    Check(result.GetChoice("department").Choice == "billing", "typed choice lookup");
    Check(result.GetScore("urgency").Score == 1.25, "typed score lookup");
    Check(result.GetNoul("refund").Noul == 0.95, "typed noul lookup");
    CheckThrows<KeyNotFoundException>(() => result.GetChoice("absent"));
    CheckThrows<InvalidOperationException>(() => result.GetScore("department"));
    Check(result.Usage.InputTokens == 42 && mixedHandler.Calls == 1, "usage and call count");
}

var stateNode = JsonNode.Parse("""{"body":"Refund needed"}""")!;
var instructionNode = JsonNode.Parse("""{"task":"route ticket"}""")!;
var optionNode = JsonNode.Parse("""{"description":"refunds"}""")!;
var fluentBuilder = new SystemOneRequestBuilder()
    .WithState(stateNode)
    .AddChoice("department", instructionNode, choice => choice
        .OptionJson("billing", optionNode)
        .Option("support", null))
    .AddScore("urgency", "How urgent?", score => score
        .Level("routine")
        .LevelJson(JsonNode.Parse("""{"label":"soon"}""")!))
    .AddNoul("refund", "Refund requested?", noul => noul
        .CriteriaJson(JsonNode.Parse("""{"means":"yes"}""")!, JsonValue.Create("no")!));
var fluentRequest = fluentBuilder.Build();
stateNode["body"] = "changed source";
instructionNode["task"] = "changed source";
optionNode["description"] = "changed source";
Check(fluentRequest.State["body"]!.GetValue<string>() == "Refund needed", "builder clones state input");
Check(((ChoiceQuestion)fluentRequest.Questions["department"]).Instructions["task"]!.GetValue<string>() == "route ticket",
    "builder clones instruction input");
var fluentHandler = new StubHandler(async (message, _) =>
{
    using var body = JsonDocument.Parse(await message.Content!.ReadAsStringAsync());
    var root = body.RootElement;
    Check(root.GetProperty("state").GetProperty("body").GetString() == "Refund needed", "fluent object state serialized");
    var questions = root.GetProperty("questions");
    Check(questions.GetProperty("department").GetProperty("instructions").GetProperty("task").GetString() == "route ticket",
        "structured instructions serialized");
    Check(questions.GetProperty("department").GetProperty("criteria").GetProperty("billing")
        .GetProperty("description").GetString() == "refunds", "structured choice option serialized");
    Check(questions.GetProperty("department").GetProperty("criteria").GetProperty("support").ValueKind == JsonValueKind.Null,
        "null choice option serialized");
    Check(questions.GetProperty("urgency").GetProperty("criteria")[1].GetProperty("label").GetString() == "soon",
        "structured score level serialized");
    Check(questions.GetProperty("refund").GetProperty("criteria").GetProperty("true").GetProperty("means").GetString() == "yes",
        "structured noul criteria serialized");
    return JsonResponse(HttpStatusCode.OK, success);
});
using (var http = new HttpClient(fluentHandler))
    await new SystemOneClient(http, new SystemOneOptions()).DecideAsync(fluentRequest);

var firstBuild = fluentBuilder.Build();
firstBuild.State["body"] = "changed build";
((ChoiceQuestion)firstBuild.Questions["department"]).Instructions["task"] = "changed build";
var secondBuild = fluentBuilder.Build();
Check(secondBuild.State["body"]!.GetValue<string>() == "Refund needed" &&
    ((ChoiceQuestion)secondBuild.Questions["department"]).Instructions["task"]!.GetValue<string>() == "route ticket",
    "build returns independent snapshots");
var objectState = new SystemOneRequestBuilder().WithState(new { body = "from object" })
    .AddNoul("refund", "Refund requested?").Build();
Check(objectState.State["body"]!.GetValue<string>() == "from object", "serializable object state");
var stringState = new SystemOneRequestBuilder().WithState("ticket")
    .AddNoul("refund", "Refund requested?").Build();
Check(stringState.State.GetValue<string>() == "ticket", "string state");
Check(((NoulQuestion)stringState.Questions["refund"]).Criteria is null, "fluent noul criteria optional");
CheckThrows<ArgumentException>(() => new SystemOneRequestBuilder().WithState(42)
    .AddNoul("refund", "Refund requested?").Build());
CheckThrows<ArgumentException>(() => new SystemOneRequestBuilder().WithState("ticket")
    .AddChoice("team", "Route?", choice => choice.Option("billing", "refunds")).Build());
CheckThrows<ArgumentException>(() => new SystemOneRequestBuilder().WithState("ticket")
    .AddNoul("refund", "Refund requested?").AddNoul("refund", "Again?"));
CheckThrows<ArgumentException>(() => new SystemOneRequestBuilder().WithState("ticket")
    .AddChoice("team", "Route?", choice => choice.Option("billing", "refunds").Option("billing", "again")));

var localHandler = new StubHandler((message, _) =>
{
    Check(message.RequestUri!.ToString() == "http://127.0.0.1:8000/v1/systemone", "Laya endpoint");
    Check(message.Headers.Authorization is null, "no local bearer header");
    return Task.FromResult(JsonResponse(HttpStatusCode.OK, success));
});
using (var http = new HttpClient(localHandler))
    await new SystemOneClient(http, new SystemOneOptions { BaseUri = new Uri("http://127.0.0.1:8000") }).DecideAsync(request);

var retryCalls = 0;
var retryHandler = new StubHandler((_, _) =>
{
    retryCalls++;
    return Task.FromResult(JsonResponse(retryCalls == 1 ? (HttpStatusCode)429 : HttpStatusCode.OK,
        retryCalls == 1 ? """{"detail":"slow down"}""" : success));
});
using (var http = new HttpClient(retryHandler))
    await new SystemOneClient(http, new SystemOneOptions { MaxRetries = 1, InitialRetryDelay = TimeSpan.Zero }).DecideAsync(request);
Check(retryHandler.Calls == 2, "429 retry");

var overloaded = new StubHandler((_, _) => Task.FromResult(JsonResponse((HttpStatusCode)529, """{"error":"busy"}""")));
using (var http = new HttpClient(overloaded))
{
    var client = new SystemOneClient(http, new SystemOneOptions { MaxRetries = 1, InitialRetryDelay = TimeSpan.Zero });
    var error = await Throws<SystemOneApiException>(() => client.DecideAsync(request));
    Check(error.StatusCode == (HttpStatusCode)529 && error.Attempts == 2, "529 exhausted retry");
}

var unauthorized = new StubHandler((_, _) => Task.FromResult(JsonResponse(HttpStatusCode.Unauthorized, """{"detail":"test-key invalid"}""")));
using (var http = new HttpClient(unauthorized))
{
    var client = new SystemOneClient(http, new SystemOneOptions { ApiKey = "test-key" });
    var error = await Throws<SystemOneApiException>(() => client.DecideAsync(request));
    Check(error.Attempts == 1 && !error.ResponseBody.Contains("test-key"), "401 final and key redacted");
}

var missing = new StubHandler((_, _) => Task.FromResult(JsonResponse(HttpStatusCode.OK,
    """{"model":"jev-1","answers":{},"usage":{"input_tokens":1,"output_tokens":1}}""")));
using (var http = new HttpClient(missing))
{
    var client = new SystemOneClient(http, new SystemOneOptions());
    await Throws<SystemOneProtocolException>(() => client.DecideAsync(request));
    using var source = new CancellationTokenSource();
    source.Cancel();
    await Throws<OperationCanceledException>(() => client.DecideAsync(request, source.Token));
    var invalid = new SystemOneRequest { State = JsonValue.Create(123)!, Questions = request.Questions };
    await Throws<ArgumentException>(() => client.DecideAsync(invalid));
}

var network = new StubHandler((_, _) => throw new HttpRequestException("connection refused"));
using (var http = new HttpClient(network))
    await Throws<SystemOneTransportException>(() => new SystemOneClient(http, new SystemOneOptions()).DecideAsync(request));

Console.WriteLine("All SystemOneSharp verification checks passed.");

static HttpResponseMessage JsonResponse(HttpStatusCode status, string body) => new(status)
{
    Content = new StringContent(body, Encoding.UTF8, "application/json")
};

static void Check(bool condition, string name)
{
    if (!condition) throw new Exception($"Verification failed: {name}");
    Console.WriteLine($"PASS {name}");
}

static void CheckThrows<T>(Action action) where T : Exception
{
    try { action(); }
    catch (T) { Console.WriteLine($"PASS throws {typeof(T).Name}"); return; }
    throw new Exception($"Expected {typeof(T).Name}.");
}

static async Task<T> Throws<T>(Func<Task> action) where T : Exception
{
    try { await action(); }
    catch (T error) { Console.WriteLine($"PASS throws {typeof(T).Name}"); return error; }
    throw new Exception($"Expected {typeof(T).Name}.");
}

internal sealed class StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
{
    public int Calls { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Calls++;
        cancellationToken.ThrowIfCancellationRequested();
        return respond(request, cancellationToken);
    }
}
