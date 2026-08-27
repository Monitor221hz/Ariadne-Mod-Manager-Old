namespace Daedalus.VFS;

public static class FileSystemLinker
{
    public static VirtualNode<BackedEntry> LinkFile(
        this VirtualNode<BackedEntry> root,
        string physicalPath,
        ReadOnlySpan<char> virtualPath,
        LinkFlags flags = LinkFlags.None
    )
    {
        if (flags.HasFlag(LinkFlags.FailIfExists) && IsLinked(root.FindNode(virtualPath)))
        {
            throw new IOException($"virtual path already linked: '{virtualPath}'");
        }
        return root.AddFile(virtualPath, new BackedEntry(physicalPath));
    }

    // preorder
    public static VirtualNode<BackedEntry> LinkDirectory(
        this VirtualNode<BackedEntry> root,
        string physicalDirectory,
        ReadOnlySpan<char> virtualDestination,
        LinkFlags flags = LinkFlags.Recursive
    )
    {
        physicalDirectory = Path.GetFullPath(physicalDirectory);

        if (flags.HasFlag(LinkFlags.FailIfExists) && IsLinked(root.FindNode(virtualDestination)))
        {
            throw new IOException($"virtual path already linked: '{virtualDestination}'");
        }

        var node = root.AddDirectory(virtualDestination);
        node.Data = new BackedEntry(physicalDirectory);
        if (flags.HasFlag(LinkFlags.CreateTarget))
        {
            node.SetFlag(NodeFlags.CreateTarget);
        }

        var searchOption = flags.HasFlag(LinkFlags.Recursive)
            ? SearchOption.AllDirectories
            : SearchOption.TopDirectoryOnly;

        foreach (
            var physical in Directory.EnumerateFileSystemEntries(
                physicalDirectory,
                "*",
                searchOption
            )
        )
        {
            string relative = Path.GetRelativePath(physicalDirectory, physical);
            string virtualPath =
                virtualDestination.Length == 0 ? relative : $"{virtualDestination}\\{relative}";

            if (
                flags.HasFlag(LinkFlags.Whiteouts)
                && relative.EndsWith(".daehidden", StringComparison.OrdinalIgnoreCase)
            )
            {
                root.FindNode(virtualPath.AsSpan()[..^".daehidden".Length])?.RemoveFromParent();
                continue;
            }

            if (flags.HasFlag(LinkFlags.FailIfExists) && IsLinked(root.FindNode(virtualPath)))
            {
                throw new IOException($"virtual path already linked: '{virtualPath}'");
            }

            root.AddFile(
                virtualPath,
                new BackedEntry(physical),
                File.GetAttributes(physical).HasFlag(FileAttributes.Directory)
                    ? NodeFlags.Directory
                    : NodeFlags.None
            );
        }

        return node;
    }

    private static bool IsLinked(VirtualNode<BackedEntry>? node) =>
        node?.Data.PhysicalPath is not null;
}
