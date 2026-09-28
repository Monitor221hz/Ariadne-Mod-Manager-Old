using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Daedalus.Contracts.Games;
using Daedalus.Contracts.ModManager;

namespace Daedalus.ModManager;

public sealed class AliasedModInstaller(
    ILibraryModFactory modFactory,
    ILibraryModSerializer serializer,
    IInstanceService instanceService
) : ModInstaller(modFactory, serializer)
{
    public override bool CanInstall(ISupportedGame game, DirectoryInfo content)
    {
        return true;
    }

    public override bool TryInstall(
        string name,
        IModInfo modInfo,
        DirectoryInfo content,
        [NotNullWhen(true)] out ILibraryMod? mod
    )
    {
        if (!_modFactory.TryCreate(name, modInfo, out mod))
        {
            return false;
        }

        try
        {
            mod.ReplaceDirectory(content);
        }
        catch
        {
            Debug.WriteLine($"Failed to move {content.FullName} to {mod.Directory.FullName}");
            mod = null;
            return false;
        }
        _serializer.Save(mod);
        return true;
    }
}
