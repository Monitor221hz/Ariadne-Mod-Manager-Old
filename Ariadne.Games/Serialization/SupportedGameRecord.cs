namespace Ariadne.Games.Serialization;

public sealed record class SupportedGameRecord(
    string Name,
    List<PlatformConfigurationRecord> Platforms,
    VendorInfoRecord Vendors,
    GamePathRecord Root,
    List<GamePathRecord> Deployments,
    List<GamePathRecord> InstallTargets,
    Dictionary<string, string>? ProtocolGameIds = null,
    Dictionary<string, int>? LaunchTargets = null,
    string? LoadOrderBuilder = null
);
