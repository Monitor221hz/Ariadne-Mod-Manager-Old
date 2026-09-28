using Daedalus.VFS;

namespace Daedalus.Contracts.ModManager;

public interface IArchiveReader
{
    IReadOnlyCollection<string> SupportedExtensions { get; }
    VirtualNode<ModFileEntry> Read(IModInfo modInfo, FileInfo archiveFile);
}
