using Fsp;

namespace Daedalus.VFS.WinFsp;

public sealed class WinFspVirtualFileSystem : IVirtualFileSystem
{
    private FileSystemHost? _host;

    public void Mount(VirtualNode<BackedEntry> root, VirtualFileSystemSettings settings)
    {
        _host = new FileSystemHost(
            new OverlayFileSystem(
                root,
                new OverlayFileSystemOptions
                {
                    CopyUpEnabled = settings.CopyUp,
                    OutputRules = settings.OutputRules,
                    ProcessTracker = new WMProcessObserver(),
                    PhysicalMountRoot = settings.MountPoint.FullName,
                }
            )
        );
    }

    public void Unmount()
    {
        _host?.Unmount();
    }

    public void Dispose()
    {
        _host?.Dispose();
    }
}
