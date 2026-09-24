namespace SystemOneSharp;

/// <summary>Configures the endpoint, authentication, model, and retry behavior.</summary>
public sealed class SystemOneOptions
{
    /// <summary>Gets the HTTP(S) base URI. Defaults to the hosted Jev API.</summary>
    public Uri BaseUri { get; init; } = new("https://api.typesafe.ai/");
    /// <summary>Gets the optional bearer key. Supply it through a secure configuration source.</summary>
    public string? ApiKey { get; init; }
    /// <summary>Gets the model ID sent with each request.</summary>
    public string Model { get; init; } = "jev-latest";
    /// <summary>Gets the maximum number of retries after the first attempt for HTTP 429 or 529.</summary>
    public int MaxRetries { get; init; } = 2;
    /// <summary>Gets the first exponential retry delay when the server does not send Retry-After.</summary>
    public TimeSpan InitialRetryDelay { get; init; } = TimeSpan.FromMilliseconds(250);
}
