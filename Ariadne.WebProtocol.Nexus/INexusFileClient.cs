namespace Ariadne.WebProtocol.Nexus;

public interface INexusFileClient
{
    Task<NexusFileMetadata?> GetFileAsync(
        NxmModLink link,
        string apiKey,
        CancellationToken cancellationToken = default
    );
}
