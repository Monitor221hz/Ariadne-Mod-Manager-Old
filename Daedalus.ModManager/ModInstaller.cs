using System.Diagnostics.CodeAnalysis;
using Daedalus.Contracts.Games;
using Daedalus.Contracts.ModManager;

namespace Daedalus.ModManager;

public abstract class ModInstaller(ILibraryModFactory modFactory, ILibraryModSerializer serializer)
    : IModInstaller
{
    protected readonly ILibraryModFactory _modFactory = modFactory;
    protected readonly ILibraryModSerializer _serializer = serializer;
    public abstract bool CanInstall(ISupportedGame game, DirectoryInfo content);
    public abstract bool TryInstall(
        string name,
        IModInfo modInfo,
        DirectoryInfo content,
        [NotNullWhen(true)] out ILibraryMod? mod,
        InstallType type = InstallType.New
    );
}
