using Daedalus.Contracts.ModManager;

namespace Daedalus.ModManager;

public sealed class DeploymentPathsFactory(IModManagerPaths paths) : IDeploymentPathsFactory
{
    public IDeploymentPaths Create(IModProfile profile) =>
        new DeploymentPaths(profile.OverwriteFolder, paths.StagingFolder);
}
