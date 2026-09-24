namespace Daedalus.WebProtocol.Nexus;

public interface INexusDownloadResolver
{
    int? DailyRequestsLimit { get; }
    int? DailyRequestsRemaining { get; }

    Task<NexusDownloadLink?> ResolveDownloadAsync(
        NxmModLink link,
        string apiKey,
        bool isPremium,
        string? serverName = null,
        CancellationToken cancellationToken = default
    );
}
