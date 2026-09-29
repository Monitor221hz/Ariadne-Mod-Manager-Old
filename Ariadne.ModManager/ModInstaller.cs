using System.Diagnostics.CodeAnalysis;
using Ariadne.Contracts.Games;
using Ariadne.Contracts.ModManager;

namespace Ariadne.ModManager;

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
