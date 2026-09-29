using Ariadne.Contracts.ModManager;

namespace Ariadne.ModManager;

public sealed class DeploymentPathsFactory(IModManagerPaths paths) : IDeploymentPathsFactory
{
    public IDeploymentPaths Create(IModProfile profile) =>
        new DeploymentPaths(profile.OverwriteFolder, paths.StagingFolder);
}
