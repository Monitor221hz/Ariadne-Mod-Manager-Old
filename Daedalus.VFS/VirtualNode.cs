using System.Text;

namespace Daedalus.VFS;

public partial class VirtualNode<TNodeData>
{
    private static ReadOnlySpan<char> Separators => "/\\";

    private readonly Dictionary<string, VirtualNode<TNodeData>> _children = new(
        StringComparer.OrdinalIgnoreCase
    );

    private Dictionary<string, VirtualNode<TNodeData>>.AlternateLookup<ReadOnlySpan<char>>? _alt;

    // snapshot for enumeration, invalidate on mutate
    private List<VirtualNode<TNodeData>>? _ordered;

    public VirtualNode(
        string name,
        NodeFlags flags,
        VirtualNode<TNodeData>? parent,
        TNodeData? data
    )
    {
        Name = name;
        Flags = flags;
        Parent = parent;
        Data = data;
    }

    private Dictionary<string, VirtualNode<TNodeData>>.AlternateLookup<ReadOnlySpan<char>> Alt =>
        _alt ??= _children.GetAlternateLookup<ReadOnlySpan<char>>();

    public string Name { get; }

    public VirtualNode<TNodeData>? Parent { get; private set; }

    public VirtualNode<TNodeData> Root => Parent?.Root ?? this;

    public TNodeData? Data { get; set; }

    public IReadOnlyList<VirtualNode<TNodeData>> Children => _ordered ??= BuildOrderedChildren();

    private List<VirtualNode<TNodeData>> BuildOrderedChildren()
    {
        var list = new List<VirtualNode<TNodeData>>(_children.Values);
        list.Sort(static (a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.Name, b.Name));
        return list;
    }

    public int Count => _children.Count;

    public int CountRecursive
    {
        get
        {
            int result = 1;
            foreach (var child in _children.Values)
            {
                result += child.CountRecursive;
            }
            return result;
        }
    }

    public NodeFlags Flags { get; private set; }

    public VirtualNode<TNodeData> SetFlag(NodeFlags flag, bool enabled = true)
    {
        Flags = enabled ? Flags | flag : Flags & ~flag;
        return this;
    }

    public bool HasFlag(NodeFlags flag) => (Flags & flag) != 0;

    public bool IsDirectory => HasFlag(NodeFlags.Directory);

    public string GetPath()
    {
        if (Parent == null)
        {
            return Name.Length == 0 ? "" : Name + "\\";
        }
        string parentPath = Parent.GetPath();
        return parentPath.Length == 0 ? Name : parentPath + "\\" + Name;
    }

    public void SetChild(VirtualNode<TNodeData> child)
    {
        child.Parent = this;
        _children[child.Name] = child;
        _ordered = null;
    }

    /// <summary>Adds or replaces a node; missing intermediate components become dummy directories.</summary>
    public VirtualNode<TNodeData> AddFile(
        ReadOnlySpan<char> path,
        TNodeData data,
        NodeFlags flags = NodeFlags.None
    )
    {
        var current = this;
        ReadOnlySpan<char> final = default;
        var components = new ComponentEnumerator(path);
        while (components.MoveNext())
        {
            if (!final.IsEmpty)
            {
                current = current.GetOrCreateDirectory(final);
            }
            final = components.Current;
        }

        if (final.IsEmpty)
        {
            return current;
        }
        VirtualNode<TNodeData>? next;
        if (
            !current.Alt.TryGetValue(final, out next)
            || (next.Flags & NodeFlags.Directory) != (flags & NodeFlags.Directory)
        )
        {
            next = new VirtualNode<TNodeData>(final.ToString(), flags, current, data);
            current.SetChild(next);
        }
        else
        {
            next.Data = data;
            next.Flags = flags;
        }
        return next;
    }

    public VirtualNode<TNodeData> AddDirectory(ReadOnlySpan<char> path)
    {
        var current = this;
        var components = new ComponentEnumerator(path);
        while (components.MoveNext())
        {
            current = current.GetOrCreateDirectory(components.Current);
        }
        return current;
    }

    private VirtualNode<TNodeData> GetOrCreateDirectory(ReadOnlySpan<char> name)
    {
        if (!Alt.TryGetValue(name, out var child))
        {
            child = new VirtualNode<TNodeData>(name.ToString(), NodeFlags.Directory, this, default);
            SetChild(child);
        }
        else
        {
            child.SetFlag(NodeFlags.Directory);
        }
        return child;
    }

    public VirtualNode<TNodeData>? FindNode(ReadOnlySpan<char> path)
    {
        var current = this;
        var components = new ComponentEnumerator(path);
        while (components.MoveNext())
        {
            if (!current.Alt.TryGetValue(components.Current, out var next))
            {
                return null;
            }
            current = next;
        }
        return current;
    }

    public void VisitPath(ReadOnlySpan<char> path, Action<VirtualNode<TNodeData>> visitor)
    {
        var current = this;
        var components = new ComponentEnumerator(path);
        while (components.MoveNext())
        {
            if (!current.Alt.TryGetValue(components.Current, out var next))
            {
                return;
            }
            visitor(next);
            current = next;
        }
    }

    public VirtualNode<TNodeData> GetNode(string name) =>
        TryGetNode(name, out var node)
            ? node
            : throw new KeyNotFoundException($"no node named '{name}' below '{GetPath()}'");

    public bool TryGetNode(string name, out VirtualNode<TNodeData> node) =>
        Alt.TryGetValue(name.AsSpan(), out node!);

    public bool ContainsChild(string name) => Alt.ContainsKey(name.AsSpan());

    public bool Remove(string name)
    {
        bool removed = _children.Remove(name);
        if (removed)
        {
            _ordered = null;
        }
        return removed;
    }

    public void RemoveFromParent() => Parent?.Remove(Name);

    public void Clear()
    {
        _children.Clear();
        _ordered = null;
    }

    public List<VirtualNode<TNodeData>> Find(ReadOnlySpan<char> pattern)
    {
        var result = new List<VirtualNode<TNodeData>>();

        int firstWildcard = pattern.IndexOfAny('*', '?');
        if (firstWildcard == 0)
        {
            firstWildcard = -1;
        }
        if (firstWildcard != -1)
        {
            firstWildcard = pattern[..(firstWildcard + 1)].LastIndexOfAny('\\', '/');
        }

        if (firstWildcard != -1)
        {
            var node = FindNode(pattern[..firstWildcard]);
            node?.FindLocal(result, pattern[(firstWildcard + 1)..]);
        }
        else
        {
            FindLocal(result, pattern);
        }

        return result;
    }

    private void FindLocal(ICollection<VirtualNode<TNodeData>> output, ReadOnlySpan<char> pattern)
    {
        foreach (var child in _children.Values)
        {
            if (
                pattern.Length > 1
                && pattern[0] == '*'
                && pattern[1] is '/' or '\\'
                && child.IsDirectory
            )
            {
                child.FindLocal(output, pattern[2..]);
            }
            else if (Wildcard.PartialMatch(child.Name, pattern, out var remainder))
            {
                if (remainder.IsEmpty || remainder is "*")
                {
                    output.Add(child);
                }

                if (child.IsDirectory)
                {
                    child.FindLocal(output, remainder.TrimStart(Separators));
                }
            }
        }
    }

    public void Dump(TextWriter writer, int level = 0)
    {
        writer.Write(new string(' ', level));
        writer.Write(Name);
        writer.Write(" -> ");
        writer.WriteLine(Data?.ToString() ?? "");
        foreach (var child in Children)
        {
            child.Dump(writer, level + 1);
        }
    }

    private ref struct ComponentEnumerator
    {
        private readonly ReadOnlySpan<char> _path;
        private int _pos;

        public ComponentEnumerator(ReadOnlySpan<char> path)
        {
            _path = path;
            _pos = 0;
            Current = default;
        }

        public ReadOnlySpan<char> Current { get; private set; }

        public bool MoveNext()
        {
            while (_pos < _path.Length)
            {
                int start = _pos;
                while (_pos < _path.Length && _path[_pos] is not ('/' or '\\'))
                {
                    _pos++;
                }
                var part = _path[start.._pos];
                _pos++; // step over the separator
                if (part.IsEmpty || part is ".")
                {
                    continue;
                }
                Current = part;
                return true;
            }
            Current = default;
            return false;
        }
    }
}
