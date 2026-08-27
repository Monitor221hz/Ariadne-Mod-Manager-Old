namespace Daedalus.Games.Serialization;

public sealed record class SupportedGameRecord(
    string Name,
    List<PlatformConfigurationRecord> Platforms,
    VendorInfoRecord Vendors,
    GamePathRecord Root,
    List<GamePathRecord> Deployments,
    List<GamePathRecord> InstallTargets
);
