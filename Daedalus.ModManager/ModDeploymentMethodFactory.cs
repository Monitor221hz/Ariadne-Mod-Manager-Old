using Daedalus.Contracts.ModManager;

namespace Daedalus.ModManager;

public sealed class ModDeploymentMethodFactory(Func<IModProfile, IModDeploymentMethod> producer)
    : IModDeploymentMethodFactory
{
    public IModDeploymentMethod Create(IModProfile profile) => producer(profile);
}
