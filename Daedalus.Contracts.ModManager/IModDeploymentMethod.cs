using System.Diagnostics.CodeAnalysis;
using Daedalus.Contracts.Games;
using Daedalus.Contracts.Mods;

namespace Daedalus.Contracts.ModManager;

public interface IModDeploymentMethod : IDisposable
{
    void Deploy(IInstalledGame game, IReadOnlyList<IModInfo> mods);
    void Revert(IInstalledGame game);
    bool TryGetDeployedPath(
        IInstalledGame game,
        IGamePath path,
        [NotNullWhen(true)] out DirectoryInfo? directoryInfo
    )
    {
        directoryInfo = new(game.LookupAbsolutePath(path));
        return true;
    }
}
