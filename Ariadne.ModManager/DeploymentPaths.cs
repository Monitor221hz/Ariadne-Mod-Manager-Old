using Ariadne.Contracts.ModManager;

namespace Ariadne.ModManager;

public sealed record DeploymentPaths(
    DirectoryInfo OverwriteDirectory,
    DirectoryInfo StagingDirectory
) : IDeploymentPaths;
