using Daedalus.Contracts.Mods;

namespace Daedalus.Contracts.ModManager;

public interface IModProfile
{
    string Name { get; set; }
    IModList ModList { get; set; }
    Version Version { get; set; }
    DirectoryInfo ProfileFolder { get; }
    DirectoryInfo OverwriteFolder { get; }
}
