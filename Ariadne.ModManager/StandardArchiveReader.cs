using Ariadne.Contracts.ModManager;
using Ariadne.VFS;
using SharpCompress.Archives;

namespace Ariadne.ModManager;

public sealed class StandardArchiveReader : IArchiveReader
{
    private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".zip",
        ".7z",
        ".rar",
        ".tar",
    };

    public IReadOnlyCollection<string> SupportedExtensions => Extensions;

    public VirtualNode<ModFileEntry> Read(IModInfo modInfo, FileInfo archiveFile)
    {
        var root = new VirtualNode<ModFileEntry>("", NodeFlags.Directory, null, default);
        using var archive = ArchiveFactory.OpenArchive(archiveFile.FullName);
        foreach (var entry in archive.Entries.Where(entry => !entry.IsDirectory))
        {
            if (entry is null || entry.Key == null)
            {
                continue;
            }
            var path = entry.Key.Replace('\\', '/');
            root.AddFile(
                path,
                new ModFileEntry(
                    path[(path.LastIndexOf('/') + 1)..],
                    ModEntryKind.File,
                    modInfo,
                    path,
                    entry.Size,
                    entry.LastModifiedTime ?? DateTimeOffset.MinValue
                )
            );
        }
        return root;
    }
}
