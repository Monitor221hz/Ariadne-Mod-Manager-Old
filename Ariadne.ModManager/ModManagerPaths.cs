using Ariadne.Contracts.ModManager;

namespace Ariadne.ModManager;

public class ModManagerPaths : IModManagerPaths
{
    private readonly IInstanceService _instances;

    public ModManagerPaths(DirectoryInfo assemblyFolder, IInstanceService instances)
    {
        AssemblyFolder = assemblyFolder;
        _instances = instances;
    }

    public DirectoryInfo AssemblyFolder { get; }

    public DirectoryInfo InstanceFolder =>
        _instances.Current?.Folder
        ?? throw new InvalidOperationException(
            "No active instance. Create or select an instance first."
        );

    public DirectoryInfo StagingFolder => Child("Staging");
    public DirectoryInfo ModsFolder => Child("Mods");
    public DirectoryInfo ProfilesFolder => Child("Profiles");
    public DirectoryInfo DownloadsFolder => Child("Downloads");
    public DirectoryInfo TemporaryFolder => Child("Temp");

    private DirectoryInfo Child(string name) => new(Path.Join(InstanceFolder.FullName, name));
}
