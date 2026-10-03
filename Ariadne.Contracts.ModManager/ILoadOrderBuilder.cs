using Ariadne.Contracts.Games;

namespace Ariadne.Contracts.ModManager;

public interface ILoadOrderBuilder
{
    string Key { get; }

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
