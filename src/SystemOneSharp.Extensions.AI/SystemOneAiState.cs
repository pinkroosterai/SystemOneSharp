using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.AI;

namespace SystemOneSharp.Extensions.AI;

/// <summary>Projects a Microsoft.Extensions.AI conversation into System One state.</summary>
/// <remarks>
/// This is the single projection every SystemOneSharp integration uses. It produces a stable,
/// SystemOneSharp-owned schema rather than serializing <see cref="ChatMessage"/> itself:
/// <code>
/// {"messages":[{"role":"user","contents":[
///   {"type":"text","text":"..."},
///   {"type":"function_call","call_id":"...","name":"...","arguments":{}},
///   {"type":"function_result","call_id":"...","result":...}]}]}
/// </code>
/// Only <see cref="TextContent"/>, <see cref="FunctionCallContent"/> and <see cref="FunctionResultContent"/>
/// are projected. Other content, <c>RawRepresentation</c>, <c>AdditionalProperties</c>, author names and
/// message IDs are never included. A message whose contents are all unsupported keeps its turn with an
/// empty <c>contents</c> array.
/// </remarks>
public static class SystemOneAiState
{
    /// <summary>Projects <paramref name="messages"/> with default options.</summary>
    public static JsonObject Create(IEnumerable<ChatMessage> messages) => Create(messages, new SystemOneAiStateOptions());

    /// <summary>Projects <paramref name="messages"/> with the supplied options.</summary>
    /// <exception cref="NotSupportedException">
    /// A message holds unsupported content and <see cref="SystemOneAiStateOptions.ThrowOnUnsupportedContent"/> is set.
    /// </exception>
    public static JsonObject Create(IEnumerable<ChatMessage> messages, SystemOneAiStateOptions options)
    {
        ArgumentNullException.ThrowIfNull(messages);
        ArgumentNullException.ThrowIfNull(options);
        var serializerOptions = options.SerializerOptions ?? AIJsonUtilities.DefaultOptions;

        var projected = new JsonArray();
        foreach (var message in messages)
        {
            if (message is null)
                throw new ArgumentException("Messages must not contain null.", nameof(messages));

            var contents = new JsonArray();
            foreach (var content in message.Contents)
            {
                if (Project(content, serializerOptions) is { } node)
                    contents.Add(node);
                else if (options.ThrowOnUnsupportedContent)
                    throw new NotSupportedException($"{content.GetType().Name} has no System One projection.");
            }

            projected.Add(new JsonObject
            {
                ["role"] = message.Role.Value,
                ["contents"] = contents
            });
        }

        return new JsonObject { ["messages"] = projected };
    }

    private static JsonObject? Project(AIContent content, JsonSerializerOptions options) => content switch
    {
        TextContent text => new JsonObject
        {
            ["type"] = "text",
            ["text"] = text.Text
        },
        FunctionCallContent call => new JsonObject
        {
            ["type"] = "function_call",
            ["call_id"] = call.CallId,
            ["name"] = call.Name,
            ["arguments"] = Serialize(call.Arguments ?? new Dictionary<string, object?>(), typeof(IDictionary<string, object?>), options)
        },
        FunctionResultContent result => new JsonObject
        {
            ["type"] = "function_result",
            ["call_id"] = result.CallId,
            ["result"] = Serialize(result.Result, typeof(object), options)
        },
        _ => null
    };

    private static JsonNode? Serialize(object? value, Type type, JsonSerializerOptions options) =>
        JsonSerializer.SerializeToNode(value, options.GetTypeInfo(type));
}

/// <summary>Extension methods that apply <see cref="SystemOneAiState"/> to conversations and request builders.</summary>
public static class SystemOneAiStateExtensions
{
    /// <summary>Projects the conversation into System One state.</summary>
    public static JsonObject ToSystemOneState(this IEnumerable<ChatMessage> messages, SystemOneAiStateOptions? options = null) =>
        SystemOneAiState.Create(messages, options ?? new SystemOneAiStateOptions());

    /// <summary>Sets the request state to the projected conversation.</summary>
    /// <remarks>
    /// Named differently from <see cref="SystemOneRequestBuilder.WithState{T}(T)"/> on purpose: that instance
    /// overload would otherwise bind first and reflection-serialize every <see cref="ChatMessage"/> property.
    /// </remarks>
    public static SystemOneRequestBuilder WithConversation(this SystemOneRequestBuilder builder, IEnumerable<ChatMessage> messages,
        SystemOneAiStateOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.WithState(SystemOneAiState.Create(messages, options ?? new SystemOneAiStateOptions()));
    }
}
