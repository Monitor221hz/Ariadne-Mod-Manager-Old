using System.Diagnostics.CodeAnalysis;
using Daedalus.Contracts.Games;
using Daedalus.Contracts.Mods;
using Daedalus.VFS;

namespace Daedalus.Contracts.ModManager;

public interface IModDeploymentMethod : IDisposable
{
    void SetOutputRules(List<OutputRule> outputRules);
    void Deploy(IInstalledGame game, IReadOnlyList<ILibraryMod> mods);
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
