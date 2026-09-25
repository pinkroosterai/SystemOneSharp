namespace SystemOneSharp.Testing;

/// <summary>An <see cref="ISystemOneClient"/> that records every request and answers from a factory, without HTTP.</summary>
public sealed class FakeSystemOneClient(Func<SystemOneRequest, SystemOneResponse> respond) : ISystemOneClient
{
    private readonly List<SystemOneRequest> _requests = new();
    private readonly List<CancellationToken> _tokens = new();

    /// <summary>Creates a client that throws <paramref name="error"/> for every request.</summary>
    public FakeSystemOneClient(Exception error) : this(_ => throw error) { }

    /// <summary>Gets the requests received, in order.</summary>
    public IReadOnlyList<SystemOneRequest> Requests => _requests;

    /// <summary>Gets the cancellation token passed with each request, in order.</summary>
    public IReadOnlyList<CancellationToken> Tokens => _tokens;

    public Task<SystemOneResponse> DecideAsync(SystemOneRequest request, CancellationToken cancellationToken = default)
    {
        _requests.Add(request);
        _tokens.Add(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(respond(request));
    }
}
