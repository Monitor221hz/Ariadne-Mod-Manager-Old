namespace Ariadne.ModManager.Serialization;

public sealed record class InstancesConfigRecord(
    Dictionary<string, string> Instances,
    string? LastActive
);
