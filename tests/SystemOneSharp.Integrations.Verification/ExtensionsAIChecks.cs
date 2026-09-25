using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.AI;
using SystemOneSharp.Extensions.AI;
using SystemOneSharp.Testing;
using static SystemOneSharp.Testing.Verify;

namespace SystemOneSharp.Integrations.Verification;

internal static class ExtensionsAIChecks
{
    public static async Task RunAsync()
    {
        using var cityArgument = JsonDocument.Parse("\"Paris\"");
        List<ChatMessage> conversation =
        [
            new(ChatRole.System, "You plan trips."),
            new(ChatRole.User, "Weather in Paris?"),
            new(ChatRole.Assistant, [new FunctionCallContent("call-1", "get_weather",
                new Dictionary<string, object?> { ["city"] = cityArgument.RootElement.Clone(), ["days"] = 2 })]),
            new(ChatRole.Tool, [new FunctionResultContent("call-1", new { summary = "sunny", high = 21 })]),
            new(ChatRole.Assistant, "It will be sunny.")
        ];

        const string expected =
            """{"messages":[""" +
            """{"role":"system","contents":[{"type":"text","text":"You plan trips."}]},""" +
            """{"role":"user","contents":[{"type":"text","text":"Weather in Paris?"}]},""" +
            """{"role":"assistant","contents":[{"type":"function_call","call_id":"call-1","name":"get_weather","arguments":{"city":"Paris","days":2}}]},""" +
            """{"role":"tool","contents":[{"type":"function_result","call_id":"call-1","result":{"summary":"sunny","high":21}}]},""" +
            """{"role":"assistant","contents":[{"type":"text","text":"It will be sunny."}]}]}""";
        var state = SystemOneAiState.Create(conversation);
        Check(state.ToJsonString() == expected, "ChatMessage conversation → canonical System One state");
        Check(state["messages"]![2]!["contents"]![0]!["arguments"]!["city"]!.GetValue<string>() == "Paris",
            "FunctionCallContent → canonical function-call state");
        Check(state["messages"]![3]!["contents"]![0]!["result"]!["high"]!.GetValue<int>() == 21,
            "FunctionResultContent → canonical function-result state");
        Check(conversation.ToSystemOneState().ToJsonString() == state.ToJsonString() &&
            SystemOneAiState.Create(conversation).ToJsonString() == expected, "projection is stable for identical input");

        var sparse = SystemOneAiState.Create(
        [
            new(ChatRole.Assistant, [new FunctionCallContent("call-2", "list_files")]),
            new(ChatRole.Tool, [new FunctionResultContent("call-2", null)])
        ]);
        Check(sparse["messages"]![0]!["contents"]![0]!["arguments"] is JsonObject { Count: 0 } &&
            sparse["messages"]![1]!["contents"]![0]!["result"] is null, "missing arguments and null result project as {} and null");

        var decorated = new ChatMessage(ChatRole.User,
        [
            new TextContent("visible") { AdditionalProperties = new() { ["secret_text_prop"] = "leak-1" }, RawRepresentation = "leak-2" },
            new TextReasoningContent("leak-3 hidden reasoning"),
            new DataContent(new byte[] { 1, 2, 3 }, "image/png")
        ])
        {
            AuthorName = "leak-4",
            MessageId = "leak-5",
            AdditionalProperties = new() { ["secret_message_prop"] = "leak-6" },
            RawRepresentation = "leak-7"
        };
        var reasoningOnly = new ChatMessage(ChatRole.Assistant, [new TextReasoningContent("leak-8")]);
        var decoratedJson = SystemOneAiState.Create([decorated, reasoningOnly]).ToJsonString();
        Check(decoratedJson == """{"messages":[{"role":"user","contents":[{"type":"text","text":"visible"}]},{"role":"assistant","contents":[]}]}""",
            "metadata, raw representations, reasoning and unsupported content excluded");
        Check(!decoratedJson.Contains("leak", StringComparison.Ordinal), "no excluded value reaches the state");
        Throws<NotSupportedException>(() => SystemOneAiState.Create([reasoningOnly], new SystemOneAiStateOptions { ThrowOnUnsupportedContent = true }),
            "unsupported content throws when configured");
        Throws<ArgumentException>(() => SystemOneAiState.Create([null!]), "null message rejected");

        var client = new FakeSystemOneClient(SystemOneResponses.AllNoul(0.9));
        var request = new SystemOneRequestBuilder()
            .WithConversation(conversation)
            .AddNoul("answered", "Did the assistant answer the question?")
            .Build();
        await client.DecideAsync(request);
        Check(client.Requests.Single().State.ToJsonString() == expected, "WithConversation sets the projected state");
    }
}
