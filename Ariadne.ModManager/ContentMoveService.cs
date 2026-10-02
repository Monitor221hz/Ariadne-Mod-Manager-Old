using Ariadne.Contracts.ModManager;
using Ariadne.Extensions.IO;

namespace Ariadne.ModManager;

public sealed class ContentMoveService : IContentMoveService
{
    public bool CanMoveInto(
        string sourcePath,
        bool sourceIsDirectory,
        string displayName,
        string destinationDirectory
    )
    {
        if (sourceIsDirectory && IsSameOrDescendant(destinationDirectory, sourcePath))
        {
            return false;
        }
        if (
            string.Equals(
                Path.GetDirectoryName(sourcePath),
                destinationDirectory,
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            return false;
        }
        return !Path.Exists(Path.Join(destinationDirectory, displayName));
    }

    public bool CanFlattenIntoModRoot(string sourceDirectory, string modRootDirectory)
    {
        var source = new DirectoryInfo(sourceDirectory);
        if (!source.Exists)
        {
            return false;
        }
        return source
            .EnumerateFileSystemInfos()
            .All(child => !Path.Exists(Path.Join(modRootDirectory, child.Name)));
    }

    public void MoveInto(
        string sourcePath,
        bool sourceIsDirectory,
        string displayName,
        string destinationDirectory
    )
    {
        var destination = Path.Join(destinationDirectory, displayName);
        if (sourceIsDirectory)
        {
            Directory.Move(sourcePath, destination);
        }
        else
        {
            File.Move(sourcePath, destination);
        }
    }

    public void FlattenIntoModRoot(string sourceDirectory, string modRootDirectory)
    {
        var source = new DirectoryInfo(sourceDirectory);
        foreach (var child in source.EnumerateFileSystemInfos())
        {
            var destination = Path.Join(modRootDirectory, child.Name);
            if (child is DirectoryInfo directory)
            {
                directory.MergeTo(destination);
            }
            else
            {
                ((FileInfo)child).MoveTo(destination);
            }
        }
        source.Refresh();
        if (source.Exists && !source.EnumerateFileSystemInfos().Any())
        {
            source.Delete();
        }
    }

    private static bool IsSameOrDescendant(string candidate, string directory)
    {
        var trimmed = directory.TrimEnd(Path.DirectorySeparatorChar);
        return string.Equals(candidate, trimmed, StringComparison.OrdinalIgnoreCase)
            || candidate.StartsWith(
                trimmed + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase
            );
    }
}
