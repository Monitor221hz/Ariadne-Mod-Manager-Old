namespace Ariadne.WebProtocol;

public sealed record LaunchIntent
{
    public string? Scheme { get; init; }
    public string? Link { get; init; }
}
