using Daedalus.Contracts.Games;
using Daedalus.Contracts.Mods;

namespace Daedalus.Contracts.ModManager;

public interface ILoadOrderBuilder
{
    IEnumerable<ILoadOrderInfo> Fetch(IInstalledGame game, IReadOnlyList<IModInfo> mods);
    void Deploy(
        IInstalledGame game,
        IModDeploymentMethod deploymentMethod,
        IReadOnlyList<ILoadOrderInfo> loadOrderInfos
    );
}
