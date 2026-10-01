namespace Ariadne.ModManager.Serialization;

public sealed record class ModListRecord(
    List<string> LooseMods,
    List<ModGroupRecord> ModGroups,
    Dictionary<string, bool>? ActiveStates = null
);
