namespace Daedalus.WebProtocol.Nexus;

public sealed record NexusLaunchIntent
{
    public required NxmLink? Link { get; init; }
}
