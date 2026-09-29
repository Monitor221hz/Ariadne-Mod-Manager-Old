namespace Ariadne.Contracts.ModManager;

public interface IModDeploymentMethodFactory
{
    IModDeploymentMethod Create(IModProfile profile);
}
