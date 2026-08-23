using Daedalus.Contracts.Configs;

namespace Daedalus.Configs;

public class SupportedGame : ISupportedGame
{
    public string Name { get; }
    public IReadOnlyList<IPlatformConfiguration> Platforms { get; }
    public IVendorInfo Vendors { get; }
    public IGamePath Root { get; }
    public IReadOnlyList<IGamePath> Targets { get; }

    public SupportedGame(
        string name,
        IReadOnlyList<IPlatformConfiguration> platforms,
        IVendorInfo vendors,
        IGamePath root,
        IReadOnlyList<IGamePath> targets
    )
    {
        Name = name;
        Platforms = platforms;
        Vendors = vendors;
        Root = root;
        Targets = targets;
    }
}
