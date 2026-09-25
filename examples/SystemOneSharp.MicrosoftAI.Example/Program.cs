using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;
using SystemOneSharp;
using SystemOneSharp.AgentFramework;
using SystemOneSharp.Extensions.AI;
using SystemOneSharp.Extensions.AI.Evaluation;

// Every System One call below is live. The chat model is a scripted stand-in so the example needs no
// LLM key; swap ScriptedChatClient for any IChatClient to run the same code against a real model.
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

    await ProjectConversationAsync();
    await EvaluateResponseAsync(client);
    await RunCompletionLoopAsync(client);
    await RunFunctionGateAsync(client);
    await RunWorkflowAsync(client);
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

// 1. One canonical projection turns MEAI chat history into System One state.
static Task ProjectConversationAsync()
{
    PrintHeading("1 · Chat history → System One state");
    List<ChatMessage> conversation =
    [
        new(ChatRole.User, "What's the weather in Paris?"),
        new(ChatRole.Assistant, [new FunctionCallContent("call-1", "get_weather", new Dictionary<string, object?> { ["city"] = "Paris" })]),
        new(ChatRole.Tool, [new FunctionResultContent("call-1", "Sunny, 21 °C")]),
        new(ChatRole.Assistant, "It's sunny and 21 °C in Paris.")
    ];
    Console.WriteLine(conversation.ToSystemOneState().ToJsonString(new JsonSerializerOptions
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping // display only
    }));
    Console.WriteLine();
    return Task.CompletedTask;
}

// 2. One System One request produces several MEAI evaluation metrics.
static async Task EvaluateResponseAsync(ISystemOneClient client)
{
    PrintHeading("2 · Evaluating a ChatResponse");
    var evaluator = new SystemOneEvaluator(client,
        request => request
            .AddNoul("completed", "Did the assistant fully answer the user's question?")
            .AddScore("quality", "How helpful and accurate is the assistant's answer?", score => score
                .Level("Unhelpful or wrong")
                .Level("Partly helpful")
                .Level("Helpful and accurate"))
            .AddChoice("failure", "What, if anything, went wrong?", choice => choice
                .Option("none", "Nothing went wrong")
                .Option("incomplete", "The answer leaves part of the question unanswered")
                .Option("off_topic", "The answer is about something else")),
        metrics => metrics
            .Noul("completed", "TaskCompletion", value => value >= 0.8
                ? new EvaluationMetricInterpretation(EvaluationRating.Good)
                : new EvaluationMetricInterpretation(EvaluationRating.Poor, failed: true))
            .Score("quality", "Quality")
            .Choice("failure", "FailureMode"));

    var result = await evaluator.EvaluateAsync(
        [new ChatMessage(ChatRole.User, "How long do I have to return a laptop?")],
        new ChatResponse(new ChatMessage(ChatRole.Assistant, "You can return a laptop within 30 days of delivery for a full refund.")));

    foreach (var metric in result.Metrics.Values)
    {
        var value = metric switch
        {
            NumericMetric numeric => numeric.Value?.ToString("0.000", CultureInfo.InvariantCulture),
            StringMetric text => text.Value,
            _ => null
        };
        var verdict = metric.Interpretation is { } interpretation ? $"  ({interpretation.Rating}{(interpretation.Failed ? ", failed" : "")})" : "";
        Console.WriteLine($"  {metric.Name,-16} {value}{verdict}");
    }
    Console.WriteLine($"  One System One call: model {result.Metrics.Values.First().Metadata!["eval-model"]}, " +
        $"{result.Metrics.Values.First().Metadata!["eval-duration-ms"]} ms");
    Console.WriteLine();
}

// 3. System One decides after each iteration whether a LoopAgent should run the agent again.
static async Task RunCompletionLoopAsync(ISystemOneClient client)
{
    PrintHeading("3 · SystemOneCompletionLoopEvaluator");
    string[] drafts =
    [
        "Outline: 1. Revenue 2. Costs 3. (to do)",
        "Q3 report: revenue grew 12% to $4.1M, costs fell 3%, and margin reached 18%. All three sections are complete."
    ];
    var turn = 0;
    var decisions = 0;
    var chat = new ScriptedChatClient(_ => new ChatResponse(new ChatMessage(ChatRole.Assistant, drafts[Math.Min(turn++, drafts.Length - 1)])));
    var observed = new ObservingSystemOneClient(client, response =>
    {
        decisions++;
        var probability = response.GetNoul(SystemOneCompletionLoopEvaluator.QuestionId).Noul;
        Console.WriteLine($"  Run {turn}: P(completed) = {probability.ToString("0.000", CultureInfo.InvariantCulture)}");
    });
    var loop = new LoopAgent(new ChatClientAgent(chat),
        new SystemOneCompletionLoopEvaluator(observed, new SystemOneCompletionLoopEvaluatorOptions
        {
            CompletionThreshold = 0.9,
            ContinueFeedback = "The report is not finished. Complete every section."
        }),
        new LoopAgentOptions { MaxIterations = 4 });

    var response = await loop.RunAsync("Write the Q3 report with revenue, costs and margin.");
    // LoopAgent skips the evaluator on the run that hits MaxIterations, so fewer decisions than runs means the cap ended it.
    Console.WriteLine($"  Agent runs: {turn}   Stopped by: {(decisions < turn ? "LoopAgentOptions.MaxIterations" : "System One completion policy")}");
    Console.WriteLine($"  Last answer: {response.Messages.Last().Text}");
    Console.WriteLine();
}

// 4. System One classifies a proposed tool call; application policy decides what happens.
static async Task RunFunctionGateAsync(ISystemOneClient client)
{
    PrintHeading("4 · Function-call gating middleware");
    var deleteRecords = AIFunctionFactory.Create(
        (string table) => $"Deleted every row in {table}.", "delete_records", "Permanently delete all rows in a database table.");
    var chat = new ScriptedChatClient(messages => messages.Any(message => message.Contents.OfType<FunctionResultContent>().Any())
        ? new ChatResponse(new ChatMessage(ChatRole.Assistant, "Done."))
        : new ChatResponse(new ChatMessage(ChatRole.Assistant,
            [new FunctionCallContent("call-1", "delete_records", new Dictionary<string, object?> { ["table"] = "customers" })])));

    var gate = new SystemOneFunctionGate(client, new SystemOneFunctionGateOptions
    {
        Instructions = "Classify the proposed function call by its risk to the business.",
        Categories = choice => choice
            .Option("routine", "Safe, reversible, and clearly what the user asked for")
            .Option("review", "Possibly harmful or irreversible; a person should confirm it")
            .Option("blocked", "Destructive or clearly outside what the user asked for"),
        PolicyContext = _ => new JsonObject { ["environment"] = "production" },
        Policy = (classification, _) =>
        {
            Console.WriteLine($"  Proposed: {classification.Context.Function.Name}   Classified: {classification.Category}");
            foreach (var (label, probability) in classification.Answer.Probabilities.OrderByDescending(pair => pair.Value))
                Console.WriteLine($"    {label,-8} {probability.ToString("0.000", CultureInfo.InvariantCulture)}");
            // Review handling is application code: here it declines, a real app might page an operator.
            return ValueTask.FromResult(classification.Category == "routine"
                ? SystemOneFunctionGateDecision.Invoke()
                : SystemOneFunctionGateDecision.Block($"Not run: classified as {classification.Category}."));
        }
    });

    var agent = new ChatClientAgent(chat, tools: [deleteRecords]).AsBuilder().UseSystemOneFunctionGate(gate).Build();
    await agent.RunAsync("Clean up old customer data.");
    var toolResult = chat.Calls.SelectMany(call => call).SelectMany(message => message.Contents)
        .OfType<FunctionResultContent>().Select(result => result.Result).FirstOrDefault();
    Console.WriteLine($"  Tool result the model saw: {toolResult}");
    Console.WriteLine();
}

// 5. Inside a MAF workflow, System One is an ordinary function executor followed by ordinary conditional edges.
//    No SystemOneExecutor is needed.
static async Task RunWorkflowAsync(ISystemOneClient client)
{
    PrintHeading("5 · System One inside a MAF workflow");
    Func<string, IWorkflowContext, CancellationToken, ValueTask<TriagedTicket>> triage = async (ticket, _, cancellationToken) =>
    {
        var response = await client.DecideAsync(new SystemOneRequestBuilder()
            .WithState(ticket)
            .AddNoul("refund", "Does the customer ask for a refund?")
            .Build(), cancellationToken);
        return new TriagedTicket(ticket, response.GetNoul("refund").Noul);
    };
    // An executor's return value is its declared output; WithOutputFrom surfaces it to the caller.
    Func<TriagedTicket, string> refunds = ticket =>
        $"Refunds queue (P(refund) = {ticket.RefundProbability.ToString("0.000", CultureInfo.InvariantCulture)})";
    Func<TriagedTicket, string> support = ticket =>
        $"Support queue (P(refund) = {ticket.RefundProbability.ToString("0.000", CultureInfo.InvariantCulture)})";

    var triageExecutor = triage.BindAsExecutor("triage");
    var refundsExecutor = refunds.BindAsExecutor("refunds");
    var supportExecutor = support.BindAsExecutor("support");
    // The threshold is workflow policy, written where MAF expects routing policy: on the edges.
    var workflow = new WorkflowBuilder(triageExecutor)
        .AddEdge<TriagedTicket>(triageExecutor, refundsExecutor, ticket => ticket!.RefundProbability >= 0.5)
        .AddEdge<TriagedTicket>(triageExecutor, supportExecutor, ticket => ticket!.RefundProbability < 0.5)
        .WithOutputFrom(refundsExecutor, supportExecutor)
        .Build();

    foreach (var ticket in new[] { "I was charged twice. Please refund the duplicate.", "The export button does nothing when I click it." })
    {
        await using var run = await InProcessExecution.RunAsync(workflow, ticket);
        if (run.OutgoingEvents.OfType<WorkflowErrorEvent>().FirstOrDefault() is { } error)
            throw new InvalidOperationException($"Workflow failed: {error.Data}");
        foreach (var output in run.OutgoingEvents.OfType<WorkflowOutputEvent>())
            Console.WriteLine($"  \"{ticket}\" → {output.Data}");
    }
    Console.WriteLine();
}

static void PrintHeading(string text)
{
    var previous = Console.ForegroundColor;
    if (!Console.IsOutputRedirected) Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine(text);
    if (!Console.IsOutputRedirected) Console.ForegroundColor = previous;
}

internal sealed record TriagedTicket(string Text, double RefundProbability);

internal sealed class ExampleConfig
{
    public string? BaseUri { get; init; }
    public string? Model { get; init; }
    public string? ApiKeyEnvironmentVariable { get; init; }
}

/// <summary>Passes every call through and reports each response, to show the numbers behind a decision.</summary>
internal sealed class ObservingSystemOneClient(ISystemOneClient inner, Action<SystemOneResponse> observe) : ISystemOneClient
{
    public async Task<SystemOneResponse> DecideAsync(SystemOneRequest request, CancellationToken cancellationToken = default)
    {
        var response = await inner.DecideAsync(request, cancellationToken);
        observe(response);
        return response;
    }
}

/// <summary>A stand-in chat model that answers from a script and records every call.</summary>
internal sealed class ScriptedChatClient(Func<IReadOnlyList<ChatMessage>, ChatResponse> respond) : IChatClient
{
    public List<IReadOnlyList<ChatMessage>> Calls { get; } = new();

    public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        var snapshot = messages.ToList();
        Calls.Add(snapshot);
        return Task.FromResult(respond(snapshot));
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null,
        CancellationToken cancellationToken = default) => throw new NotSupportedException("The example only uses non-streaming calls.");

    public object? GetService(Type serviceType, object? serviceKey = null) => serviceType.IsInstanceOfType(this) ? this : null;

    public void Dispose() { }
}
