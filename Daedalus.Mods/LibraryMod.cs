using Daedalus.Contracts.Mods;
using Daedalus.VFS;

namespace Daedalus.Mods;

public sealed class LibraryMod : ILibraryMod
{
    private readonly IReadOnlyList<IArchiveReader> _archiveReaders;
    private VirtualNode<ModFileEntry>? _content;

    public IModInfo Info { get; }
    public DirectoryInfo Directory { get; }

    public LibraryMod(
        IModInfo info,
        DirectoryInfo directory,
        IReadOnlyList<IArchiveReader> archiveReaders
    )
    {
        Info = info;
        Directory = directory;
        _archiveReaders = archiveReaders;
    }

    public VirtualNode<ModFileEntry> Content => _content ??= BuildContentTree();

    public void RefreshContent() => _content = BuildContentTree();

    private VirtualNode<ModFileEntry> BuildContentTree()
    {
        Directory.Refresh();
        var root = new VirtualNode<ModFileEntry>(
            Directory.Name,
            NodeFlags.Directory,
            null,
            default
        );
        if (Directory.Exists)
        {
            AddChildren(root, Directory);
        }
        return root;
    }

    private void AddChildren(VirtualNode<ModFileEntry> node, DirectoryInfo directory)
    {
        foreach (
            var entry in directory.EnumerateFileSystemInfos("*", SearchOption.TopDirectoryOnly)
        )
        {
            if (entry is DirectoryInfo subDirectory)
            {
                var childDirectory = node.AddDirectory(subDirectory.Name);
                AddChildren(childDirectory, subDirectory);
                continue;
            }
            var file = (FileInfo)entry;
            var data = new ModFileEntry(
                file.Name,
                ModEntryKind.File,
                file.FullName,
                file.Length,
                new DateTimeOffset(file.LastWriteTimeUtc)
            );
            var child = node.AddFile(file.Name, data, NodeFlags.None);
            var reader = FindArchiveReader(file.Extension);
            if (reader is not null)
            {
                foreach (var archiveEntry in reader.Read(file))
                {
                    child.SetChild(archiveEntry);
                }
                child.Data = new ModFileEntry(
                    file.Name,
                    ModEntryKind.Archive,
                    file.FullName,
                    file.Length,
                    new DateTimeOffset(file.LastWriteTimeUtc)
                );
            }
        }
    }

    private IArchiveReader? FindArchiveReader(string extension)
    {
        foreach (var reader in _archiveReaders)
        {
            if (reader.SupportedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
            {
                return reader;
            }
        }
        return null;
    }
}
