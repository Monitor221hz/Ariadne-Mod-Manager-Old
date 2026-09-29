namespace Ariadne.Contracts.ModManager;

public interface IDeploymentPathsFactory
{
    IDeploymentPaths Create(IModProfile profile);
}
