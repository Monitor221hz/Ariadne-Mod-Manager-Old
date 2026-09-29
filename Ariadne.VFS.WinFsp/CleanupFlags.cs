using Fsp;

namespace Ariadne.VFS.WinFsp;

[Flags]
public enum CleanupFlags : uint
{
    Delete = FileSystemBase.CleanupDelete,
    SetAllocationSize = FileSystemBase.CleanupSetAllocationSize,
    SetArchiveBit = FileSystemBase.CleanupSetArchiveBit,
    SetLastAccessTime = FileSystemBase.CleanupSetLastAccessTime,
    SetLastWriteTime = FileSystemBase.CleanupSetLastWriteTime,
    SetChangeTime = FileSystemBase.CleanupSetChangeTime,
}
