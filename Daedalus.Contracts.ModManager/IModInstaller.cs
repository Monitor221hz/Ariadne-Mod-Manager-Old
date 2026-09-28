using System.Diagnostics.CodeAnalysis;
using Daedalus.Contracts.Games;
using Daedalus.VFS;

namespace Daedalus.Contracts.ModManager;

public interface IModInstaller
{
    bool CanInstall(ISupportedGame game, DirectoryInfo content);
    bool TryInstall(
        string name,
        IModInfo modInfo,
        DirectoryInfo content,
        [NotNullWhen(true)] out ILibraryMod? mod
    );
}
