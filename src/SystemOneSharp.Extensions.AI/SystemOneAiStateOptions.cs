using System.Text.Json;
using Microsoft.Extensions.AI;

namespace SystemOneSharp.Extensions.AI;

/// <summary>Configures how <see cref="SystemOneAiState"/> projects a conversation.</summary>
public sealed class SystemOneAiStateOptions
{
    /// <summary>
    /// Gets the options used to serialize function-call arguments and function results.
    /// Defaults to <see cref="AIJsonUtilities.DefaultOptions"/>.
    /// </summary>
    public JsonSerializerOptions? SerializerOptions { get; init; }

    /// <summary>
    /// Gets whether content other than text, function calls and function results throws
    /// <see cref="NotSupportedException"/>. By default it is left out of the state.
    /// </summary>
    public bool ThrowOnUnsupportedContent { get; init; }
}
