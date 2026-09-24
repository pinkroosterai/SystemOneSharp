namespace SystemOneSharp;

/// <summary>Sends System One decision requests.</summary>
public interface ISystemOneClient
{
    /// <summary>Returns typed answers for every question in the request.</summary>
    Task<SystemOneResponse> DecideAsync(SystemOneRequest request, CancellationToken cancellationToken = default);
}
