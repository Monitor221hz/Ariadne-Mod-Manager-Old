namespace Daedalus.ModManager.Serialization;

public sealed record class ModProfileRecord(string Name, ModListRecord ModList, Version Version);
