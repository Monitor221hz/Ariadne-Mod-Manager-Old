using Ariadne.Contracts.ModManager;
using Ariadne.VFS;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Archives;

namespace Ariadne.ModManager.Bethesda;

using IArchiveReader = Ariadne.Contracts.ModManager.IArchiveReader;

public class BethesdaArchiveReader : IArchiveReader
{
    public IReadOnlyCollection<string> SupportedExtensions { get; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".bsa", ".ba2" };

    public VirtualNode<ModFileEntry> Read(IModInfo modInfo, FileInfo archiveFile)
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
        return root;
    }
}
