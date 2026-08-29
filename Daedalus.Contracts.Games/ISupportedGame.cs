namespace Daedalus.Contracts.Games;

public interface ISupportedGame : IReadOnlyDictionary<string, IGamePath>
{
    string Name { get; }
    IReadOnlyList<IPlatformConfiguration> Platforms { get; }
    IVendorInfo Vendors { get; }
    IGamePath Root { get; }
    IReadOnlyList<IGamePath> Deployments { get; }
    IReadOnlyList<IGamePath> InstallTargets { get; }
}
