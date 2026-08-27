using Daedalus.Contracts.Games;

namespace Daedalus.Games;

public class SupportedGame : ISupportedGame
{
    public string Name { get; }
    public IReadOnlyList<IPlatformConfiguration> Platforms { get; }
    public IVendorInfo Vendors { get; }
    public IGamePath Root { get; }
    public IReadOnlyList<IGamePath> Deployments { get; }
    public IReadOnlyList<IGamePath> InstallTargets { get; }

    public IReadOnlyDictionary<string, IGamePath> PathNameMap { get; }

    public SupportedGame(
        string name,
        IReadOnlyList<IPlatformConfiguration> platforms,
        IVendorInfo vendors,
        IGamePath root,
        IReadOnlyList<IGamePath> deployments,
        IReadOnlyList<IGamePath> installTargets
    )
    {
        Name = name;
        Platforms = platforms;
        Vendors = vendors;
        Root = root;
        Deployments = deployments;
        InstallTargets = installTargets;
        PathNameMap = Deployments.Concat(InstallTargets).ToDictionary(t => t.Key, t => t);
    }
}
