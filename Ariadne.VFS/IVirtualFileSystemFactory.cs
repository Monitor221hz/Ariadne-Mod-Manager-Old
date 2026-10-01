namespace Ariadne.VFS;

public interface IVirtualFileSystemFactory
{
    VirtualFileSystemCapabilities Capabilities { get; }

    public IVirtualFileSystem Create();
}
