using Ariadne.Contracts.Games;
using Ariadne.Contracts.ModManager;
using Ariadne.VFS;

namespace Ariadne.ModManager;

public sealed class DeploymentPreviewService : IDeploymentPreviewService
{
    public IReadOnlyList<DeploymentTargetGroup> GroupByTarget(
        IReadOnlyList<ILibraryMod> mods,
        ISupportedGame? configuration
    )
    {
        return mods.GroupBy(
                mod => (mod.Info.Target ?? "").Trim('\\', '/'),
                StringComparer.OrdinalIgnoreCase
            )
            .Select(group => new DeploymentTargetGroup(
                ResolveDisplayKey(group.Key, configuration),
                ResolveGamePath(group.Key, configuration),
                group.ToList()
            ))
            .ToList();
    }

    public VirtualNode<ModFileEntry> MergeContent(IReadOnlyList<ILibraryMod> mods)
    {
        var merged = new VirtualNode<ModFileEntry>("", NodeFlags.Directory, null, null);
        foreach (var mod in mods)
        {
            var stack = new Stack<(VirtualNode<ModFileEntry> Node, string Prefix)>();
            stack.Push((mod.Content, ""));
            while (stack.Count > 0)
            {
                var (node, prefix) = stack.Pop();
                foreach (var child in node.Children)
                {
                    var relative = prefix.Length == 0 ? child.Name : prefix + "\\" + child.Name;
                    if (child.Data is not null)
                    {
                        merged.AddFile(
                            relative,
                            child.Data,
                            child.IsDirectory ? NodeFlags.Directory : NodeFlags.None
                        );
                    }
                    stack.Push((child, relative));
                }
            }
        }
        foreach (var node in merged.SelfAndDescendants())
        {
            if (node.Data is null && !ReferenceEquals(node, merged))
            {
                node.Data = CreateDirectoryEntry(node.Name);
            }
        }
        return merged;
    }

    private static string ResolveDisplayKey(string rawTarget, ISupportedGame? config)
    {
        if (string.IsNullOrEmpty(rawTarget))
        {
            return config?.InstallTargets.FirstOrDefault(t => t.DirectoryPath.Length == 0)?.Key
                ?? "Root";
        }
        return
            config?.InstallTargets.FirstOrDefault(t =>
                t.Key.Equals(rawTarget, StringComparison.OrdinalIgnoreCase)
                || t.DirectoryPath.Equals(rawTarget, StringComparison.OrdinalIgnoreCase)
            )
                is { } target
            ? target.Key
            : $"<unresolved: {rawTarget}>";
    }

    private static IGamePath? ResolveGamePath(string rawTarget, ISupportedGame? config)
    {
        if (string.IsNullOrEmpty(rawTarget))
        {
            return config?.InstallTargets.FirstOrDefault(t => t.DirectoryPath.Length == 0)
                ?? config?.Root;
        }
        return config?.InstallTargets.FirstOrDefault(t =>
            t.Key.Equals(rawTarget, StringComparison.OrdinalIgnoreCase)
            || t.DirectoryPath.Equals(rawTarget, StringComparison.OrdinalIgnoreCase)
        );
    }

    private static ModFileEntry CreateDirectoryEntry(string name) =>
        new(name, ModEntryKind.Directory, DirectoryOrigin.Value, "", 0, DateTimeOffset.MinValue);

    private static class DirectoryOrigin
    {
        public static readonly IModInfo Value = new PlaceholderModInfo();

        private sealed class PlaceholderModInfo : IModInfo
        {
            public ModID ID => new(0, SourceType.Local);
            public string Version => "";
            public List<string> Categories => [];
            public string Target { get; set; } = "";
            public uint Priority { get; set; }
            public bool Active { get; set; }
        }
    }
}
