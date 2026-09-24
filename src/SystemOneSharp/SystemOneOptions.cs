namespace SystemOneSharp;

public sealed class SystemOneOptions
{
    public Uri BaseUri { get; init; } = new("https://api.typesafe.ai/");
    public string? ApiKey { get; init; }
    public string Model { get; init; } = "jev-latest";
    public int MaxRetries { get; init; } = 2;
    public TimeSpan InitialRetryDelay { get; init; } = TimeSpan.FromMilliseconds(250);
}
