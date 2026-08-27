using Daedalus.Contracts.Games;
using Daedalus.Contracts.Mods;

namespace Daedalus.ModManager;

public interface IModDeploymentMethod : IDisposable
{
    public ModDeploymentFlags Flags { get; }
    void Deploy(IInstalledGame game, IReadOnlyList<IModInfo> mods);
    void Revert(IInstalledGame game);
}

public interface ILoadOrderDeploymentMethod : IDisposable
{
    public ModDeploymentFlags Flags { get; }
    void Deploy(IInstalledGame game, IReadOnlyList<IModInfo> mods);
    void Revert(IInstalledGame game);
}
