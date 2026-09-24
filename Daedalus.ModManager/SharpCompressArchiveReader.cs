using Daedalus.Contracts.ModManager;
using Daedalus.VFS;
using SharpCompress.Archives;

namespace Daedalus.ModManager;

public sealed class SharpCompressArchiveReader : IArchiveReader
{
    private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".zip",
        ".7z",
        ".rar",
        ".tar",
        ".gz",
        ".bz2",
    };

    public IReadOnlyCollection<string> SupportedExtensions => Extensions;

    public IReadOnlyList<VirtualNode<ModFileEntry>> Read(IModInfo modInfo, FileInfo archiveFile)
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
        return root.Children.ToList();
    }
}
