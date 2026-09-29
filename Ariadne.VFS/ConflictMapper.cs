namespace Ariadne.VFS;

public static class ConflictMapper<T>
{
    public enum ConflictType
    {
        Overwritten,
        FileFolderType,
    }

    public readonly record struct Conflict(
        ConflictType ConflictType,
        IReadOnlyList<IndexedNode> Providers,
        int WinnerIndex
    );

    public readonly record struct IndexedNode(int Index, VirtualNode<T> Node);

    private static IEnumerable<Conflict> Scan(IReadOnlyList<IndexedNode> trees, int focusIndex = -1)
    {
        Dictionary<string, List<IndexedNode>> localNodes = new(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < trees.Count; i++)
        {
            VirtualNode<T>? tree = trees[i].Node;
            foreach (var child in tree.Children)
            {
                if (!localNodes.TryGetValue(child.Name, out var list))
                {
                    list = new List<IndexedNode>();
                    localNodes[child.Name] = list;
                }
                list.Add(new IndexedNode(trees[i].Index, child));
            }
        }
        foreach (var kvp in localNodes)
        {
            var group = kvp.Value;
            if (group.Count < 2 || (focusIndex >= 0 && !group.Exists(c => c.Index == focusIndex)))
            {
                continue;
            }
            bool anyLeaf = false;
            bool anyParent = false;
            foreach (var conflict in group)
            {
                if (conflict.Node.IsDirectory)
                {
                    anyParent = true;
                }
                else
                {
                    anyLeaf = true;
                }
            }
            if (anyLeaf && anyParent)
            {
                yield return new Conflict(
                    ConflictType.FileFolderType,
                    group,
                    group.FindLast(c => c.Node.IsDirectory).Index
                );
                continue;
            }
            if (anyParent)
            {
                foreach (var childConflict in Scan(group, focusIndex))
                {
                    yield return childConflict;
                }
                continue;
            }
            if (anyLeaf)
            {
                yield return new Conflict(ConflictType.Overwritten, group, group[^1].Index);
                continue;
            }
        }
    }

    private static IEnumerable<Conflict> EnumerateConflicts(
        IReadOnlyList<VirtualNode<T>> trees,
        int focusIndex = -1
    )
    {
        if (trees.Count < 2)
        {
            yield break;
        }
        var indexedTrees = trees.Select((n, i) => new IndexedNode(i, n)).ToList();
        foreach (var conflict in Scan(indexedTrees, focusIndex))
        {
            yield return conflict;
        }
    }

    public static IEnumerable<Conflict> MapConflicts(IReadOnlyList<VirtualNode<T>> trees)
    {
        ArgumentNullException.ThrowIfNull(trees);
        return EnumerateConflicts(trees, -1);
    }

    public static IEnumerable<Conflict> MapConflictsFor(
        IReadOnlyList<VirtualNode<T>> trees,
        int focusIndex
    )
    {
        ArgumentNullException.ThrowIfNull(trees);
        ArgumentOutOfRangeException.ThrowIfNegative(focusIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(focusIndex, trees.Count);
        return EnumerateConflicts(trees, focusIndex);
    }
}
