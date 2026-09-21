using Daedalus.Contracts.Games;
using Daedalus.Contracts.Mods;

namespace Daedalus.Contracts.ModManager;

public interface ILoadOrderBuilder
{
    IEnumerable<ILoadOrderInfo> Fetch(IInstalledGame game, IModList mods);
    void Deploy(
        IInstalledGame game,
        IModDeploymentMethod deploymentMethod,
        IReadOnlyList<ILoadOrderInfo> loadOrderInfos
    );

    void Save(IModProfile currentProfile, IReadOnlyList<ILoadOrderInfo> loadOrderInfos);
    IEnumerable<ILoadOrderInfo> Sort(
        IModProfile currentProfile,
        IEnumerable<ILoadOrderInfo> loadOrderInfos
    );
}
