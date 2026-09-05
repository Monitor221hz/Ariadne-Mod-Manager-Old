namespace Daedalus.VFS.WinFsp;

public sealed record class OverlayFileSystemOptions
{
    public bool CopyUpEnabled { get; init; } = true;
    public uint FileInfoTimeout { get; init; } = 60_000;
    public uint DirInfoTimeout { get; init; } = 60_000;
    public IReadOnlyList<OutputRule>? OutputRules { get; init; }
    public WMProcessObserver? ProcessTracker { get; init; }
    public string? PhysicalMountRoot { get; init; }
}
