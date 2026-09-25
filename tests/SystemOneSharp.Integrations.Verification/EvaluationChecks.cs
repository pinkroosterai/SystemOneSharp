using System.Net;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;
using SystemOneSharp.Extensions.AI;
using SystemOneSharp.Extensions.AI.Evaluation;
using SystemOneSharp.Testing;
using static SystemOneSharp.Testing.Verify;

namespace SystemOneSharp.Integrations.Verification;

internal static class EvaluationChecks
{
    private static readonly ChatMessage[] Conversation = [new(ChatRole.User, "Summarize the refund policy.")];
    private static readonly ChatResponse Response = new(new ChatMessage(ChatRole.Assistant, "Refunds within 30 days."));

    public static async Task RunAsync()
    {
        var client = new FakeSystemOneClient(SystemOneResponses.ById(new Dictionary<string, SystemOneAnswer>
        {
            ["completed"] = SystemOneAnswers.Noul(0.97),
            ["followed"] = SystemOneAnswers.Noul(0.4),
            ["quality"] = SystemOneAnswers.Score(1.6, ["poor", "fair", "good"], [0.1, 0.2, 0.7]),
            ["failure"] = SystemOneAnswers.Choice(new Dictionary<string, double> { ["none"] = 0.85, ["off_topic"] = 0.15 })
        }));
        var evaluator = CreateEvaluator(client);
        Check(evaluator.EvaluationMetricNames.SequenceEqual(["TaskCompletion", "InstructionAdherence", "Quality", "FailureMode"]),
            "evaluator exposes mapped metric names");

        var result = await evaluator.EvaluateAsync(Conversation, Response);
        Check(client.Requests.Count == 1 && client.Requests[0].Questions.Count == 4, "several evaluator metrics → one DecideAsync call");
        Check(client.Requests[0].State.ToJsonString() ==
            Conversation.Concat(Response.Messages).ToSystemOneState().ToJsonString(), "evaluator state is the shared projection");

        var completion = result.Get<NumericMetric>("TaskCompletion");
        Check(completion.Value == 0.97, "Noul evaluation → NumericMetric");
        Check(completion.Interpretation is { Failed: false, Rating: EvaluationRating.Good } &&
            result.Get<NumericMetric>("InstructionAdherence").Interpretation is null, "interpretation applied only where configured");
        var quality = result.Get<NumericMetric>("Quality");
        Check(quality.Value == 1.6 && quality.Metadata![SystemOneEvaluator.ConfidenceMetadataName] == "0.7" &&
            JsonDocument.Parse(quality.Metadata[SystemOneEvaluator.ProbabilitiesMetadataName]).RootElement.GetProperty("2").GetDouble() == 0.7,
            "Score evaluation → NumericMetric with confidence and distribution metadata");
        var failure = result.Get<StringMetric>("FailureMode");
        Check(failure.Value == "none" && failure.Metadata![SystemOneEvaluator.ConfidenceMetadataName] == "0.8",
            "Choice evaluation → categorical StringMetric");
        Check(completion.Metadata!["eval-model"] == SystemOneResponses.Model && completion.Metadata["eval-input-tokens"] == "10" &&
            completion.Metadata["eval-total-tokens"] == "12" && completion.Metadata.ContainsKey("eval-duration-ms"),
            "metrics carry model, token and duration metadata");

        var failing = await CreateEvaluator(new FakeSystemOneClient(SystemOneResponses.ById(new Dictionary<string, SystemOneAnswer>
        {
            ["completed"] = SystemOneAnswers.Noul(0.5),
            ["followed"] = SystemOneAnswers.Noul(0.5),
            ["quality"] = SystemOneAnswers.Score(0, ["poor", "fair", "good"], [1, 0, 0]),
            ["failure"] = SystemOneAnswers.Choice(new Dictionary<string, double> { ["none"] = 0.1, ["off_topic"] = 0.9 })
        }))).EvaluateAsync(Conversation, Response);
        Check(failing.Get<NumericMetric>("TaskCompletion").Interpretation is { Failed: true }, "threshold lives in the interpretation delegate");

        using var source = new CancellationTokenSource();
        var cancelling = new FakeSystemOneClient(SystemOneResponses.AllNoul(0.9));
        source.Cancel();
        await ThrowsAsync<OperationCanceledException>(async () => await CreateEvaluator(cancelling).EvaluateAsync(Conversation, Response,
            cancellationToken: source.Token), "evaluator cancellation throws");
        Check(cancelling.Tokens.Single() == source.Token, "cancellation token → propagated to ISystemOneClient");

        var apiError = new SystemOneApiException(HttpStatusCode.TooManyRequests, "busy", 3);
        var thrown = await ThrowsAsync<SystemOneApiException>(async () =>
            await CreateEvaluator(new FakeSystemOneClient(apiError)).EvaluateAsync(Conversation, Response),
            "SystemOneException → propagates from the evaluator");
        Check(ReferenceEquals(thrown, apiError), "SystemOneException → preserves existing failure semantics");

        var misconfigured = new SystemOneEvaluator(new FakeSystemOneClient(SystemOneResponses.AllNoul(0.9)),
            request => request.AddNoul("completed", "Is the task complete?"),
            metrics => metrics.Score("completed", "Quality"));
        await ThrowsAsync<InvalidOperationException>(async () => await misconfigured.EvaluateAsync(Conversation, Response),
            "metric mapped to a question of another type is rejected");
        Throws<ArgumentException>(() => new SystemOneEvaluator(client, _ => { }, metrics => metrics
            .Noul("a", "Same").Noul("b", "Same")), "duplicate metric names rejected");
    }

    private static SystemOneEvaluator CreateEvaluator(ISystemOneClient client) => new(client,
        request => request
            .AddNoul("completed", "Did the assistant complete the user's task?")
            .AddNoul("followed", "Did the assistant follow its instructions?")
            .AddScore("quality", "How good is the answer?", score => score.Level("poor").Level("fair").Level("good"))
            .AddChoice("failure", "What went wrong, if anything?", choice => choice
                .Option("none", "Nothing went wrong")
                .Option("off_topic", "The answer is off topic")),
        metrics => metrics
            .Noul("completed", "TaskCompletion", value => value >= 0.95
                ? new EvaluationMetricInterpretation(EvaluationRating.Good)
                : new EvaluationMetricInterpretation(EvaluationRating.Unacceptable, failed: true))
            .Noul("followed", "InstructionAdherence")
            .Score("quality", "Quality")
            .Choice("failure", "FailureMode"));
}
