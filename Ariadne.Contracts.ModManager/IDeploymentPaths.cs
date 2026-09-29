namespace Ariadne.Contracts.ModManager;

public interface IDeploymentPaths
{
    public DirectoryInfo OverwriteDirectory { get; }
    public DirectoryInfo StagingDirectory { get; }
}
