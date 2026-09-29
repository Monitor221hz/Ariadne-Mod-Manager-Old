using Ariadne.VFS;

namespace Ariadne.Contracts.ModManager;

public interface ILibraryMod : IEqualityComparer<ILibraryMod>, IEquatable<ILibraryMod>
{
    IModInfo Info { get; }
    DirectoryInfo Directory { get; }
    string Name { get; }
    VirtualNode<ModFileEntry> Content { get; }
    void RefreshContent();
    public void RenameTo(string newName);

    public bool TryCreateDirectory()
    {
        if (Directory.Exists)
        {
            return false;
        }
        Directory.Create();
        return true;
    }

    public void ForceCreateDirectory()
    {
        if (Directory.Exists)
        {
            Directory.Delete(true);
        }
        Directory.Create();
    }

    public void ReplaceDirectory(DirectoryInfo content)
    {
        if (Directory.Exists)
        {
            Directory.Delete(true);
        }
        content.MoveTo(Directory.FullName);
    }
}
