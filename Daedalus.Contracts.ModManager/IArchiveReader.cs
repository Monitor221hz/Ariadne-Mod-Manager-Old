using Daedalus.VFS;

namespace Daedalus.Contracts.ModManager;

public interface IArchiveReader
{
    IReadOnlyCollection<string> SupportedExtensions { get; }
    IReadOnlyList<VirtualNode<ModFileEntry>> Read(IModInfo modInfo, FileInfo archiveFile);
}
