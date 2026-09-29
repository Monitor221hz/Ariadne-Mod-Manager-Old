namespace Ariadne.WebProtocol.Nexus;

public interface INexusSsoSession
{
    Uri AuthorizationUri { get; }

    Task<NexusSsoResult> ConnectAsync(CancellationToken cancellationToken = default);
}
