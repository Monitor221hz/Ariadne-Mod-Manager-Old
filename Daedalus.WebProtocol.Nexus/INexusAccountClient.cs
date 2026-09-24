namespace Daedalus.WebProtocol.Nexus;

public interface INexusAccountClient
{
    Task<NexusAccount?> ValidateAsync(string apiKey, CancellationToken cancellationToken = default);
}
