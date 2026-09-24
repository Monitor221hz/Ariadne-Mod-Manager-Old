using Daedalus.Contracts.ModManager;
using Daedalus.VFS;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Archives;

namespace Daedalus.ModManager.Bethesda;

using IArchiveReader = Daedalus.Contracts.ModManager.IArchiveReader;

public class BethesdaArchiveReader : IArchiveReader
{
    public IReadOnlyCollection<string> SupportedExtensions { get; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".bsa", ".ba2" };

    public IReadOnlyList<VirtualNode<ModFileEntry>> Read(IModInfo modInfo, FileInfo archiveFile)
    {
        var root = new VirtualNode<ModFileEntry>("", NodeFlags.Directory, null, default);
        var reader = Archive.CreateReader(GameRelease.SkyrimSE, archiveFile.FullName);
        foreach (var file in reader.Files)
        {
            var path = file.Path.Replace('\\', '/');
            root.AddFile(
                path,
                new ModFileEntry(
                    path[(path.LastIndexOf('/') + 1)..],
                    ModEntryKind.File,
                    modInfo,
                    path,
                    file.Size,
                    DateTimeOffset.MinValue
                )
            );
        }
        return root.Children.ToList();
    }
}
