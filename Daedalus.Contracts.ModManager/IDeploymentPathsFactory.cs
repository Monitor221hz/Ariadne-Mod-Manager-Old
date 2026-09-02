namespace Daedalus.Contracts.ModManager;

public interface IDeploymentPathsFactory
{
    IDeploymentPaths Create(IModProfile profile);
}
