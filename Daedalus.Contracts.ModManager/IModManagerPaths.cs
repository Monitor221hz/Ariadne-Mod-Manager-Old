namespace Daedalus.Contracts.ModManager;

public interface IModManagerPaths
{
    DirectoryInfo AssemblyFolder { get; }
    DirectoryInfo InstanceFolder { get; }
    DirectoryInfo StagingFolder { get; }
    DirectoryInfo ModsFolder { get; }
    DirectoryInfo ProfilesFolder { get; }
}
