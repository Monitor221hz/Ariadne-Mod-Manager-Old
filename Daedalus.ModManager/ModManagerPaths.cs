using Daedalus.Contracts.ModManager;

namespace Daedalus.ModManager;

public class ModManagerPaths : IModManagerPaths, IDiskInitializable
{
    public DirectoryInfo AssemblyFolder { get; }
    public DirectoryInfo StagingFolder { get; }

    public DirectoryInfo ModsFolder { get; }

    public ModManagerPaths(
        DirectoryInfo assemblyFolder,
        DirectoryInfo stagingFolder,
        DirectoryInfo modsFolder
    )
    {
        AssemblyFolder = assemblyFolder;
        StagingFolder = stagingFolder;
        ModsFolder = modsFolder;
    }

    public ModManagerPaths(DirectoryInfo assemblyFolder)
    {
        AssemblyFolder = assemblyFolder;
        StagingFolder = new DirectoryInfo(Path.Join(assemblyFolder.FullName, "Staging"));
        ModsFolder = new DirectoryInfo(Path.Join(assemblyFolder.FullName, "Mods"));
    }

    public void InitializeDisk()
    {
        if (StagingFolder.Exists)
        {
            StagingFolder.Delete(true);
        }
        StagingFolder.Create();
        if (!ModsFolder.Exists)
        {
            ModsFolder.Create();
        }
    }
}
