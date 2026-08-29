using Daedalus.Contracts.Games;
using Daedalus.Contracts.Mods;

namespace Daedalus.Contracts.ModManager;

public interface IModDeploymentMethod : IDisposable
{
    public ModDeploymentFlags Flags { get; }
    void Deploy(IInstalledGame game, IReadOnlyList<IModInfo> mods);
    void Revert(IInstalledGame game);
    bool TryGetDeployedPath(IInstalledGame game, IGamePath path, out DirectoryInfo? directoryInfo)
    {
        directoryInfo = new(game.LookupAbsolutePath(path));
        return true;
    }
}
