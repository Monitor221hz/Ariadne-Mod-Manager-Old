using Ariadne.VFS;

namespace Ariadne.Contracts.ModManager;

public interface IArchiveReader
{
    IReadOnlyCollection<string> SupportedExtensions { get; }
    VirtualNode<ModFileEntry> Read(IModInfo modInfo, FileInfo archiveFile);
}
