using Daedalus.VFS;

namespace Daedalus.Contracts.Mods;

public interface ILibraryMod
{
    IModInfo Info { get; }
    DirectoryInfo Directory { get; }
    VirtualNode<ModFileEntry> Content { get; }
    void RefreshContent();
}
