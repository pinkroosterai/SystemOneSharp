# SystemOneSharp.Extensions.AI

Turns a [Microsoft.Extensions.AI](https://learn.microsoft.com/dotnet/ai/microsoft-extensions-ai) conversation into System One state. It is the one projection the evaluation and Agent Framework packages also use, so a conversation looks the same to System One wherever it comes from.

```text
dotnet add package SystemOneSharp.Extensions.AI
```

It depends on `SystemOneSharp` and `Microsoft.Extensions.AI.Abstractions` only.

## Use

```csharp
using SystemOneSharp;
using SystemOneSharp.Extensions.AI;

var request = new SystemOneRequestBuilder()
    .WithConversation(messages)                      // IEnumerable<ChatMessage>
    .AddNoul("answered", "Did the assistant answer the user's question?")
    .Build();

var response = await client.DecideAsync(request);
```

`messages.ToSystemOneState()` and `SystemOneAiState.Create(messages, options)` return the same state as a `JsonObject`.

The builder method is `WithConversation`, not `WithState`: `SystemOneRequestBuilder.WithState<T>(T)` would otherwise bind first and serialize every property of every `ChatMessage`.

## The state schema

The schema belongs to SystemOneSharp, and it stays the same when `ChatMessage` gains properties:

```json
{
  "messages": [
    { "role": "user", "contents": [ { "type": "text", "text": "Weather in Paris?" } ] },
    { "role": "assistant", "contents": [
      { "type": "function_call", "call_id": "call-1", "name": "get_weather", "arguments": { "city": "Paris" } } ] },
    { "role": "tool", "contents": [
      { "type": "function_result", "call_id": "call-1", "result": "Sunny, 21 °C" } ] }
  ]
}
```

Only `TextContent`, `FunctionCallContent` and `FunctionResultContent` are projected. Other types will be added once it is clear what they mean to System One. Reasoning, images, data, usage and tool-approval content are left out by default. A message whose contents are all left out keeps its turn with an empty `contents` array. Set `SystemOneAiStateOptions.ThrowOnUnsupportedContent` to get a `NotSupportedException` instead.

`RawRepresentation`, `AdditionalProperties`, author names and message IDs are never included.

Function arguments and results are serialized with `AIJsonUtilities.DefaultOptions` unless you set `SystemOneAiStateOptions.SerializerOptions`.

## What this package does not do

It has no `IChatClient` and no decision-client abstraction. Microsoft's proposed provider-neutral decision API ([dotnet/extensions#7764](https://github.com/dotnet/extensions/issues/7764)) has not shipped. When it does, an adapter from it to `ISystemOneClient` belongs in this package.

See also: [evaluation](https://github.com/pinkroosterai/SystemOneSharp/blob/master/docs/evaluation.md), [Agent Framework](https://github.com/pinkroosterai/SystemOneSharp/blob/master/docs/agent-framework.md), [core client](https://github.com/pinkroosterai/SystemOneSharp/blob/master/README.md).
