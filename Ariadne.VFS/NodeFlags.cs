namespace Ariadne.VFS;

[Flags]
public enum NodeFlags : byte
{
    None = 0x00,

    Directory = 0x01,

    /// <summary>The node exists only to hold children; it maps to no actual entry.</summary>
    Placeholder = 0x02,

    /// <summary>File creation within this subtree redirects to this node's physical target.</summary>
    CreateTarget = 0x04,
}
