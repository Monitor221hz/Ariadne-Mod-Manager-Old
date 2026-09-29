using Ariadne.Contracts.Games;
using Ariadne.Contracts.ModManager;

namespace Ariadne.ModManager;

public class QuickPatternModTargeter : IModTargeter
{
    public void ApplyAliases(ISupportedGame game, ILibraryMod mod)
    {
        var flattened = false;
        foreach (var target in game.InstallTargets)
        {
            foreach (var alias in target.Aliases)
            {
                flattened |= FlattenAliasDirectory(mod.Directory, alias);
            }
        }
        if (flattened)
        {
            mod.RefreshContent();
        }
    }

    public IGamePath GetTarget(ISupportedGame game, ILibraryMod mod)
    {
        foreach (var target in game.InstallTargets)
        {
            if (target.Patterns.Any(pattern => mod.Content.Find(pattern).Count > 0))
            {
                return target;
            }
        }
        return game.InstallTargets[0];
    }

    private static bool FlattenAliasDirectory(DirectoryInfo modDirectory, string alias)
    {
        var aliasPath = Path.Join(modDirectory.FullName, alias);
        if (!System.IO.Directory.Exists(aliasPath))
        {
            return false;
        }

        foreach (var child in new DirectoryInfo(aliasPath).EnumerateFileSystemInfos())
        {
            var destination = Path.Join(modDirectory.FullName, child.Name);
            if (child is DirectoryInfo childDirectory)
            {
                if (System.IO.Directory.Exists(destination))
                {
                    MergeDirectory(childDirectory, new DirectoryInfo(destination));
                }
                else
                {
                    childDirectory.MoveTo(destination);
                }
            }
            else
            {
                File.Move(child.FullName, destination, overwrite: true);
            }
        }

        System.IO.Directory.Delete(aliasPath, recursive: true);
        return true;
    }

    private static void MergeDirectory(DirectoryInfo source, DirectoryInfo destination)
    {
        foreach (var child in source.EnumerateFileSystemInfos())
        {
            var destinationPath = Path.Join(destination.FullName, child.Name);
            if (child is DirectoryInfo childDirectory)
            {
                if (System.IO.Directory.Exists(destinationPath))
                {
                    MergeDirectory(childDirectory, new DirectoryInfo(destinationPath));
                }
                else
                {
                    childDirectory.MoveTo(destinationPath);
                }
            }
            else
            {
                File.Move(child.FullName, destinationPath, overwrite: true);
            }
        }

        if (!source.EnumerateFileSystemInfos().Any())
        {
            source.Delete();
        }
    }
}
