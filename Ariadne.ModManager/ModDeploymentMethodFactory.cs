using Ariadne.Contracts.ModManager;

namespace Ariadne.ModManager;

public sealed class ModDeploymentMethodFactory(Func<IModProfile, IModDeploymentMethod> producer)
    : IModDeploymentMethodFactory
{
    public IModDeploymentMethod Create(IModProfile profile) => producer(profile);
}
