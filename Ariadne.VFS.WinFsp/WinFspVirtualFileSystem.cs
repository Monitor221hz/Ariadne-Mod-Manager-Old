using Fsp;

namespace Ariadne.VFS.WinFsp;

public sealed class WinFspVirtualFileSystem : IVirtualFileSystem
{
    private FileSystemHost? _host;
    private WMProcessObserver? _processTracker;
    private string? _junctionPath;
    private string? _backingPath;

    public void Mount(VirtualNode<BackedEntry> root, VirtualFileSystemSettings settings)
    {
        var mountPoint = settings.MountPoint.FullName;
        if (settings.InPlaceTarget is { } inPlaceTarget)
        {
            mountPoint = PrepareInPlace(root, mountPoint, inPlaceTarget.FullName);
        }
        else
        {
            PrepareMountPoint(settings.MountPoint);
        }
        if (settings.OutputRules.Count > 0)
        {
            _processTracker = new WMProcessObserver();
        }
        var host = new FileSystemHost(
            new OverlayFileSystem(
                root,
                new OverlayFileSystemOptions
                {
                    CopyUpEnabled = settings.CopyUp,
                    OutputRules = settings.OutputRules,
                    ProcessTracker = _processTracker,
                    PhysicalMountRoot = mountPoint,
                }
            )
        );
        var status = host.Mount(mountPoint, null, false, 0);
        if (status < 0)
        {
            host.Dispose();
            Win32.ThrowIoExceptionWithNtStatus(status);
        }
        _host = host;
        if (_junctionPath is not null)
        {
            try
            {
                Junction.Create(_junctionPath, mountPoint);
            }
            catch
            {
                Unmount();
                host.Dispose();
                throw;
            }
        }
    }

    public void Unmount()
    {
        var host = _host;
        _host = null;
        host?.Unmount();
        host?.Dispose();
        var tracker = _processTracker;
        _processTracker = null;
        tracker?.Dispose();
        try
        {
            if (
                _junctionPath is not null
                && new DirectoryInfo(_junctionPath).LinkTarget is not null
            )
            {
                Junction.Delete(_junctionPath);
            }
            if (_backingPath is not null && Directory.Exists(_backingPath))
            {
                RestoreDirectory(_backingPath, _junctionPath!);
            }
        }
        finally
        {
            _junctionPath = null;
            _backingPath = null;
        }
    }

    public void Dispose()
    {
        Unmount();
    }

    private static void RestoreDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var entry in new DirectoryInfo(source).EnumerateFileSystemInfos())
        {
            var target = Path.Join(destination, entry.Name);
            if (entry is DirectoryInfo directory)
            {
                RestoreDirectory(directory.FullName, target);
            }
            else
            {
                File.Move(entry.FullName, target);
            }
        }
        Directory.Delete(source);
    }

    private string PrepareInPlace(VirtualNode<BackedEntry> root, string mountPoint, string realPath)
    {
        var backingPath = BackingPathFor(realPath);
        if (Junction.IsJunctionTo(realPath, mountPoint))
        {
            Junction.Delete(realPath);
        }
        if (Directory.Exists(backingPath))
        {
            RecoverBacking(realPath, backingPath);
        }
        if (!Directory.Exists(realPath))
        {
            return realPath;
        }
        if (!Directory.EnumerateFileSystemEntries(realPath).Any())
        {
            Directory.Delete(realPath);
            return realPath;
        }
        Directory.Move(realPath, backingPath);
        root.LinkDirectory(backingPath, "");
        _junctionPath = realPath;
        _backingPath = backingPath;
        return mountPoint;
    }

    private static string BackingPathFor(string realPath)
    {
        var trimmed = realPath.TrimEnd(Path.DirectorySeparatorChar);
        var parent = Path.GetDirectoryName(trimmed)!;
        return Path.Join(parent, "." + Path.GetFileName(trimmed) + ".ariadne-backing");
    }

    private static void RecoverBacking(string realPath, string backingPath)
    {
        if (!Directory.Exists(realPath))
        {
            Directory.Move(backingPath, realPath);
            return;
        }
        MergeDirectory(new DirectoryInfo(backingPath), new DirectoryInfo(realPath));
        Directory.Delete(backingPath, true);
    }

    private static void MergeDirectory(DirectoryInfo source, DirectoryInfo destination)
    {
        foreach (var child in source.EnumerateFileSystemInfos())
        {
            var target = Path.Join(destination.FullName, child.Name);
            if (child is DirectoryInfo directory)
            {
                if (Directory.Exists(target))
                {
                    MergeDirectory(directory, new DirectoryInfo(target));
                }
                else
                {
                    directory.MoveTo(target);
                }
            }
            else if (!File.Exists(target))
            {
                ((FileInfo)child).MoveTo(target);
            }
        }
    }

    private static void PrepareMountPoint(DirectoryInfo mountPoint)
    {
        mountPoint.Refresh();
        if (mountPoint.Exists && !mountPoint.Attributes.HasFlag(FileAttributes.ReparsePoint))
        {
            mountPoint.Delete(true);
        }
    }
}
