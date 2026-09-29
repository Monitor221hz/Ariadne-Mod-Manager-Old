namespace Ariadne.VFS;

public interface IVirtualFileSystem : IDisposable
{
    public void Mount(VirtualNode<BackedEntry> root, VirtualFileSystemSettings settings);
    public void Unmount();
}
