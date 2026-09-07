namespace Daedalus.VFS;

public partial class VirtualNode<TNodeData>
{
    public enum DiffType : byte
    {
        LeftOnly,
        RightOnly,
        Same,
        DataConflict,
        TypeConflict,
    }

    public readonly record struct NodeDifference<T>(
        DiffType Kind,
        VirtualNode<T>? Left,
        VirtualNode<T>? Right
    )
    {
        public VirtualNode<T> Pick(bool preferRight) => preferRight ? Right! : Left!;
    }

    public static IEnumerable<NodeDifference<TNodeData>> MapDiff(
        VirtualNode<TNodeData>? left,
        VirtualNode<TNodeData>? right,
        IEqualityComparer<TNodeData>? comparer
    )
    { //Shared directories never appear as entries so intermediate dirs of the union only from AddFile's scaffolding, flag loss?
        if (left == null && right != null)
        {
            yield return new(DiffType.RightOnly, null, right);
            yield break;
        }
        if (right == null && left != null)
        {
            yield return new(DiffType.LeftOnly, left, null);
            yield break;
        }
        if (left == null || right == null)
        {
            yield break;
        }

        foreach (var leftChild in left.Children)
        {
            if (right.Alt.TryGetValue(leftChild.Name, out var rightMatch))
            {
                if (leftChild.IsDirectory != rightMatch.IsDirectory)
                {
                    yield return new(DiffType.TypeConflict, leftChild, rightMatch);
                    continue;
                }
                if (leftChild.IsDirectory && rightMatch.IsDirectory)
                {
                    foreach (var childDiff in MapDiff(leftChild, rightMatch, comparer))
                    {
                        yield return childDiff;
                    }
                    continue;
                }
                comparer ??= EqualityComparer<TNodeData>.Default;
                yield return comparer.Equals(leftChild.Data, rightMatch.Data)
                    ? new(DiffType.Same, leftChild, rightMatch)
                    : new(DiffType.DataConflict, leftChild, rightMatch);
                continue;
            }
            yield return new(DiffType.LeftOnly, leftChild, null);
        }
        foreach (var rightChild in right.Children)
        {
            if (!left.Alt.TryGetValue(rightChild.Name, out var _))
            {
                yield return new(DiffType.RightOnly, null, rightChild);
            }
        }
    }

    private void MergeCloneIn(VirtualNode<TNodeData> child)
    {
        var parentPath = child.Parent?.GetPath() ?? "";
        var directory = AddDirectory(parentPath);
        var clone = new VirtualNode<TNodeData>(child);
        directory.SetChild(clone);
    }

    public static VirtualNode<TNodeData> Union(
        IEnumerable<NodeDifference<TNodeData>> diffs,
        bool preferRight = true
    )
    {
        var root = new VirtualNode<TNodeData>(string.Empty, NodeFlags.Directory, null, default);
        foreach (var diff in diffs)
        {
            switch (diff.Kind)
            {
                case DiffType.Same:
                    root.MergeCloneIn(diff.Pick(preferRight));
                    break;
                case DiffType.LeftOnly:
                    root.MergeCloneIn(diff.Left);
                    break;
                case DiffType.RightOnly:
                    root.MergeCloneIn(diff.Right);
                    break;
                case DiffType.DataConflict:
                    root.MergeCloneIn(diff.Pick(preferRight));
                    break;
                case DiffType.TypeConflict:
                    root.MergeCloneIn(diff.Pick(preferRight));
                    break;
            }
        }
        return root;
    }

    public VirtualNode<TNodeData> Union(
        VirtualNode<TNodeData> other,
        bool preferRight = true,
        IEqualityComparer<TNodeData>? comparer = null
    ) => Union(MapDiff(this, other, comparer), preferRight);

    public static VirtualNode<TNodeData> Conflicts(
        IEnumerable<NodeDifference<TNodeData>> diffs,
        bool preferRight = true
    )
    {
        var root = new VirtualNode<TNodeData>(string.Empty, NodeFlags.Directory, null, default);
        foreach (var diff in diffs)
        {
            switch (diff.Kind)
            {
                case DiffType.DataConflict:
                case DiffType.TypeConflict:
                    root.MergeCloneIn(diff.Pick(preferRight));
                    break;
            }
        }
        return root;
    }

    public VirtualNode<TNodeData> Conflicts(
        VirtualNode<TNodeData> other,
        bool preferRight = true,
        IEqualityComparer<TNodeData>? comparer = null
    ) => Conflicts(MapDiff(this, other, comparer), preferRight);

    public static VirtualNode<TNodeData> Exclusives(IEnumerable<NodeDifference<TNodeData>> diffs)
    {
        var root = new VirtualNode<TNodeData>(string.Empty, NodeFlags.Directory, null, default);
        foreach (var diff in diffs)
        {
            switch (diff.Kind)
            {
                case DiffType.LeftOnly:
                    root.MergeCloneIn(diff.Left);
                    break;
                case DiffType.RightOnly:
                    root.MergeCloneIn(diff.Right);
                    break;
            }
        }
        return root;
    }

    public VirtualNode<TNodeData> Exclusives(
        VirtualNode<TNodeData> other,
        IEqualityComparer<TNodeData>? comparer = null
    ) => Exclusives(MapDiff(this, other, comparer));

    public static VirtualNode<TNodeData> Exclude(
        IEnumerable<NodeDifference<TNodeData>> diffs,
        bool excludeRight = true
    )
    {
        var root = new VirtualNode<TNodeData>(string.Empty, NodeFlags.Directory, null, default);
        DiffType includeKind = excludeRight ? DiffType.LeftOnly : DiffType.RightOnly;
        foreach (var diff in diffs)
        {
            if (diff.Kind == includeKind)
            {
                root.MergeCloneIn(diff.Pick(!excludeRight));
            }
        }
        return root;
    }

    public VirtualNode<TNodeData> Exclude(
        VirtualNode<TNodeData> other,
        bool excludeRight = true,
        IEqualityComparer<TNodeData>? comparer = null
    ) => Exclude(MapDiff(this, other, comparer), excludeRight);
}
