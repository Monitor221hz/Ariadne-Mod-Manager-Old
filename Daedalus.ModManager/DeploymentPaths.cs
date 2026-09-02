using Daedalus.Contracts.ModManager;

namespace Daedalus.ModManager;

public sealed record DeploymentPaths(
    DirectoryInfo OverwriteDirectory,
    DirectoryInfo StagingDirectory
) : IDeploymentPaths;
