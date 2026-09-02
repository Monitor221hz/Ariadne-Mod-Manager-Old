using Daedalus.Contracts.ModManager;
using Daedalus.Contracts.Mods;

namespace Daedalus.ModManager;

public class ModProfile : IDiskInitializable, IModProfile
{
    public string Name { get; set; }
    public IModList ModList { get; set; }
    public Version Version { get; set; }
    public DirectoryInfo ProfileFolder { get; }
    public DirectoryInfo OverwriteFolder { get; }

    public ModProfile(string name, IModList modList, Version version, DirectoryInfo profileFolder)
    {
        Name = name;
        ModList = modList;
        Version = version;
        ProfileFolder = profileFolder;
        OverwriteFolder = new DirectoryInfo(Path.Join(profileFolder.FullName, "Overwrite"));
    }

    public void InitializeDisk()
    {
        if (!OverwriteFolder.Exists)
        {
            OverwriteFolder.Create();
        }
    }
}
