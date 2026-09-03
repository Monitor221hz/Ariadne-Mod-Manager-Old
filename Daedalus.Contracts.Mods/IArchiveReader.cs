using Daedalus.VFS;

namespace Daedalus.Contracts.Mods;

public interface IArchiveReader
{
    IReadOnlyCollection<string> SupportedExtensions { get; }
    IReadOnlyList<VirtualNode<ModFileEntry>> Read(FileInfo archiveFile);
}
