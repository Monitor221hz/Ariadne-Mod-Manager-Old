namespace Ariadne.VFS;

public sealed record class VirtualFileSystemSettings(
    bool CopyUp,
    DirectoryInfo MountPoint,
    IReadOnlyList<OutputRule> OutputRules
);
