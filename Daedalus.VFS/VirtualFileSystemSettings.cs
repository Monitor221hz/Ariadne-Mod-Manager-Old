namespace Daedalus.VFS;

public sealed record class VirtualFileSystemSettings(
    bool CopyUp,
    DirectoryInfo MountPoint,
    IReadOnlyList<OutputRule> OutputRules
);
