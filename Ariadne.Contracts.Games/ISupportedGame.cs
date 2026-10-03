namespace Ariadne.Contracts.Games;

public interface ISupportedGame : IReadOnlyDictionary<string, IGamePath>
{
    string Name { get; }

    IVendorInfo Vendors { get; }
    IReadOnlyDictionary<string, string> ProtocolGameIds { get; }
    IGamePath Root { get; }
    IReadOnlyList<IGamePath> Deployments { get; }
    IReadOnlyList<IGamePath> InstallTargets { get; }
    IReadOnlyDictionary<string, int> LaunchTargets { get; }
    string? LoadOrderBuilder => null;
}
