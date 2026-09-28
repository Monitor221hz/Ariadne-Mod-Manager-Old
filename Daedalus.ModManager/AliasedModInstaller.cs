using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Daedalus.Contracts.Games;
using Daedalus.Contracts.ModManager;

namespace Daedalus.ModManager;

public sealed class AliasedModInstaller(
    ILibraryModFactory modFactory,
    ILibraryModSerializer serializer
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
        [NotNullWhen(true)] out ILibraryMod? mod,
        InstallType type = InstallType.New
    )
    {
        if (type is InstallType.Replace)
        {
            mod = _modFactory.Create(name, modInfo);
            if (mod is null)
            {
                return false;
            }
            mod.ForceCreateDirectory();
        }
        else if (!_modFactory.TryCreate(name, modInfo, out mod))
        {
            if (type is not InstallType.Merge)
            {
                return false;
            }
            mod = _modFactory.Create(name, modInfo);
            if (mod is null)
            {
                return false;
            }
        }

        try
        {
            CopyDirectory(content, mod.Directory);
        }
        catch
        {
            Debug.WriteLine($"Failed to copy {content.FullName} to {mod.Directory.FullName}");
            mod = null;
            return false;
        }
        _serializer.Save(mod);
        return true;
    }

    private static void CopyDirectory(DirectoryInfo source, DirectoryInfo destination)
    {
        foreach (var entry in source.EnumerateFileSystemInfos())
        {
            var destinationPath = Path.Join(destination.FullName, entry.Name);
            if (entry is DirectoryInfo subDirectory)
            {
                var nested = new DirectoryInfo(destinationPath);
                nested.Create();
                CopyDirectory(subDirectory, nested);
            }
            else if (entry is FileInfo file)
            {
                file.CopyTo(destinationPath, overwrite: true);
            }
        }
    }
}
