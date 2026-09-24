namespace SystemOneSharp;

public interface ISystemOneClient
{
    Task<SystemOneResponse> DecideAsync(SystemOneRequest request, CancellationToken cancellationToken = default);
}
