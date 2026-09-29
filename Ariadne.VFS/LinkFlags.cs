namespace Ariadne.VFS;

[Flags]
public enum LinkFlags : uint
{
    None = 0,

    FailIfExists = 0x01,

    //MonitorChanges = 0x02,

    /// <summary>File creation within the destination subtree redirects here.</summary>
    CreateTarget = 0x04,

    Recursive = 0x08,

    Whiteouts = 0x10,
}
