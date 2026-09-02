namespace Daedalus.Contracts.ModManager;

public interface IModManagerPaths
{
    DirectoryInfo AssemblyFolder { get; }
    DirectoryInfo StagingFolder { get; }
    DirectoryInfo ModsFolder { get; }
}
