using System.Diagnostics.CodeAnalysis;
using Daedalus.Contracts.ModManager;
using Daedalus.VFS;

namespace Daedalus.ModManager;

public sealed class LibraryMod : ILibraryMod
{
    private readonly IReadOnlyList<IArchiveReader> _archiveReaders;

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
        _content = NewContentLazy();
    }

    private Lazy<VirtualNode<ModFileEntry>> _content;
    public VirtualNode<ModFileEntry> Content => _content.Value;

    public void RefreshContent() => _content = NewContentLazy();

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
        _content = NewContentLazy();
    }

    private Lazy<VirtualNode<ModFileEntry>> NewContentLazy() =>
        new(BuildContentTree, LazyThreadSafetyMode.ExecutionAndPublication);

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
            AddChildren(Info, root, Directory);
        }
        return root;
    }

    private void AddChildren(
        IModInfo modInfo,
        VirtualNode<ModFileEntry> node,
        DirectoryInfo directory
    )
    {
        foreach (
            var entry in directory.EnumerateFileSystemInfos("*", SearchOption.TopDirectoryOnly)
        )
        {
            if (entry is DirectoryInfo subDirectory)
            {
                var childDirectory = node.AddDirectory(subDirectory.Name);
                AddChildren(modInfo, childDirectory, subDirectory);
                continue;
            }
            var file = (FileInfo)entry;
            if (file.Name.Equals(LibraryModSerializer.FileName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            var reader = FindArchiveReader(file.Extension);
            var data = new ModFileEntry(
                file.Name,
                reader is not null ? ModEntryKind.Archive : ModEntryKind.File,
                modInfo,
                file.FullName,
                file.Length,
                new DateTimeOffset(file.LastWriteTimeUtc)
            );
            var child = node.AddFile(file.Name, data, NodeFlags.None);
            if (reader is not null)
            {
                foreach (var archiveEntry in reader.Read(modInfo, file).Children)
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

    public bool Equals(ILibraryMod? x, ILibraryMod? y)
    {
        return x is not null && y is not null && x.Directory.FullName == y.Directory.FullName;
    }

    public int GetHashCode([DisallowNull] ILibraryMod obj)
    {
        return obj.Directory.FullName.GetHashCode(StringComparison.OrdinalIgnoreCase);
    }

    public override bool Equals(object? obj)
    {
        return this.Equals(obj as ILibraryMod);
    }

    public override int GetHashCode()
    {
        return GetHashCode(this);
    }

    public bool Equals(ILibraryMod? other)
    {
        return other is not null && Directory.FullName == other.Directory.FullName;
    }
}
