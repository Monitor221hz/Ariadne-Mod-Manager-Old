using System.Diagnostics.CodeAnalysis;
using Ariadne.Contracts.Games;
using Ariadne.VFS;

namespace Ariadne.Contracts.ModManager;

public enum InstallType
{
    New,
    Merge,
    Replace,
}

public interface IModInstaller
{
    bool CanInstall(ISupportedGame game, DirectoryInfo content);
    bool TryInstall(
        string name,
        IModInfo modInfo,
        DirectoryInfo content,
        [NotNullWhen(true)] out ILibraryMod? mod,
        InstallType type = InstallType.New
    );
}
