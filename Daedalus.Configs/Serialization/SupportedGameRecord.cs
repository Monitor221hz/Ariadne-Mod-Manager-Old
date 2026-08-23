namespace Daedalus.Configs.Serialization;

public sealed record class SupportedGameRecord(
    string Name,
    List<PlatformConfigurationRecord> Platforms,
    VendorInfoRecord Vendors,
    GamePathRecord Root,
    List<GamePathRecord> Targets
);
