namespace Daedalus.VFS;

[Flags]
public enum VirtualFileSystemFlags : byte
{
    None = 0,
    RequiresEmptyMountDir = 1 << 0,
}
