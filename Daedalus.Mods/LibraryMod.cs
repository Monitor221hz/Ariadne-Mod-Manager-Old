using Daedalus.Contracts.Mods;
using Daedalus.VFS;

namespace Daedalus.Mods;

public sealed class LibraryMod : ILibraryMod
{
    private readonly IReadOnlyList<IArchiveReader> _archiveReaders;
    private VirtualNode<ModFileEntry>? _content;

    public IModInfo Info { get; }
    public DirectoryInfo Directory { get; private set; }
    public string Name => Directory.Name;

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

    public void RenameTo(string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
        {
            throw new ArgumentException("Mod name must not be empty.", nameof(newName));
        }
        newName = newName.Trim();
        if (newName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new ArgumentException(
                $"\"{newName}\" contains invalid path characters.",
                nameof(newName)
            );
        }
        if (newName == Directory.Name)
        {
            return;
        }

        var parent =
            Directory.Parent
            ?? throw new InvalidOperationException($"Mod \"{Name}\" has no parent folder.");
        var destinationPath = Path.Join(parent.FullName, newName);
        if (System.IO.Directory.Exists(destinationPath) || File.Exists(destinationPath))
        {
            throw new InvalidOperationException($"A mod named \"{newName}\" already exists.");
        }

        Directory.MoveTo(destinationPath);
        Directory = new DirectoryInfo(destinationPath);
        _content = null;
    }

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
                child.Data = data;
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
