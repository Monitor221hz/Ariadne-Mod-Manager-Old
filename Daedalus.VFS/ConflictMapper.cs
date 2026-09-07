namespace Daedalus.VFS;

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

    private static IEnumerable<Conflict> Scan(IReadOnlyList<IndexedNode> trees)
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
            if (group.Count < 2)
            {
                continue;
            }
            bool anyLeaf = false;
            bool anyParent = false;
            foreach (var conflict in group)
            {
                if (conflict.Node.IsDirectory || conflict.Node.Count > 0)
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
                foreach (var childConflict in Scan(group))
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

    private static IEnumerable<Conflict> EnumerateConflicts(IReadOnlyList<VirtualNode<T>> trees)
    {
        if (trees.Count < 2)
        {
            yield break;
        }
        var indexedTrees = trees.Select((n, i) => new IndexedNode(i, n)).ToList();
        foreach (var conflict in Scan(indexedTrees))
        {
            yield return conflict;
        }
    }

    public static IEnumerable<Conflict> MapConflicts(IReadOnlyList<VirtualNode<T>> trees)
    {
        ArgumentNullException.ThrowIfNull(trees);
        return EnumerateConflicts(trees);
    }
}
