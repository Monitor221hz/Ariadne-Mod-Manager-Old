using FileInfo = Fsp.Interop.FileInfo;
using SysFileInfo = System.IO.FileInfo;

namespace Daedalus.VFS.WinFsp;

// adapted from https://github.com/winfsp/winfsp/blob/master/tst/passthrough-dotnet/Program.cs
public static class FileSystemInfoExtensions
{
    public static FileInfo GetFileInfo(
        this FileSystemInfo fileSystemInfo,
        int allocationUnit = 4096
    )
    {
        var fileInfo = new FileInfo();
        fileSystemInfo.Refresh();
        if (fileSystemInfo.Exists)
        {
            fileInfo.FileAttributes = (uint)fileSystemInfo.Attributes;
            fileInfo.FileSize = fileSystemInfo is SysFileInfo sysFileInfo
                ? (ulong)sysFileInfo.Length
                : 0;
            fileInfo.CreationTime = (ulong)fileSystemInfo.CreationTimeUtc.ToFileTimeUtc();
            fileInfo.LastAccessTime = (ulong)fileSystemInfo.LastAccessTimeUtc.ToFileTimeUtc();
            fileInfo.LastWriteTime = (ulong)fileSystemInfo.LastWriteTimeUtc.ToFileTimeUtc();
        }
        fileInfo.AllocationSize =
            (fileInfo.FileSize + (ulong)allocationUnit - 1)
            / (ulong)allocationUnit
            * (ulong)allocationUnit;
        fileInfo.ChangeTime = fileInfo.LastWriteTime;
        return fileInfo;
    }
}
