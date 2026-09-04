using Daedalus.VFS;

namespace Daedalus.Contracts.Mods;

public interface ILibraryMod : IEqualityComparer<ILibraryMod>, IEquatable<ILibraryMod>
{
    IModInfo Info { get; }
    DirectoryInfo Directory { get; }
    string Name { get; }
    VirtualNode<ModFileEntry> Content { get; }
    void RefreshContent();

    public void RenameTo(string newName);
}
