namespace Daedalus.Contracts.Configs;

public interface ISupportedGame
{
    string Name { get; }
    IReadOnlyList<IPlatformConfiguration> Platforms { get; }
    IVendorInfo Vendors { get; }
    IGamePath Root { get; }
    IReadOnlyList<IGamePath> Targets { get; }
}
