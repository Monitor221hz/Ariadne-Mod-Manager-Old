namespace Daedalus.VFS;

// Derived from ModOrganizer2/usvfs/src/shared/directory_tree.h (GPLv3, Sebastian Herbord)
public interface IVirtualFileSystem : IDisposable
{
    public void Mount(VirtualNode<BackedEntry> root, VirtualFileSystemSettings settings);
    public void Unmount();
}
