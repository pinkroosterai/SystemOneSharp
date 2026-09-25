using System.Diagnostics;

namespace SystemOneSharp;

/// <summary>Names the <see cref="ActivitySource"/> that traces every <see cref="SystemOneClient.DecideAsync"/> call.</summary>
/// <remarks>
/// Spans record only operational data: model, question counts, retries, token usage and outcome.
/// State, instructions, criteria, answers and the API key are never recorded.
/// </remarks>
public static class SystemOneDiagnostics
{
    /// <summary>The source name to subscribe to, for example with OpenTelemetry's <c>AddSource</c>.</summary>
    public const string ActivitySourceName = "SystemOneSharp";

    internal static readonly ActivitySource Source = new(ActivitySourceName, typeof(SystemOneDiagnostics).Assembly.GetName().Version?.ToString());

    // OpenTelemetry GenAI semantic conventions, still Development status upstream; names may change.
    // See docs/microsoft-ai-integration/research.md § Core diagnostics.
    internal const string OperationName = "decide";
    internal const string ProviderName = "systemone";
    internal const string OperationNameTag = "gen_ai.operation.name";
    internal const string ProviderNameTag = "gen_ai.provider.name";
    internal const string RequestModelTag = "gen_ai.request.model";
    internal const string ResponseModelTag = "gen_ai.response.model";
    internal const string InputTokensTag = "gen_ai.usage.input_tokens";
    internal const string OutputTokensTag = "gen_ai.usage.output_tokens";
    internal const string ErrorTypeTag = "error.type";

    internal const string QuestionCountTag = "systemone.question.count";
    internal const string ChoiceCountTag = "systemone.question.choice.count";
    internal const string ScoreCountTag = "systemone.question.score.count";
    internal const string NoulCountTag = "systemone.question.noul.count";
    internal const string RetryCountTag = "systemone.retry.count";
}
