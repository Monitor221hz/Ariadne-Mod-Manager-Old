using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace Daedalus.FileSystem;

public class PathTree : IDictionary<ulong, Entry>
{
    static ulong Hash(ReadOnlySpan<byte> path)
    {
        if (path.Length > 0 && path[^1] == 0)
            path = path[..^1];

        ulong h = 14695981039346656037UL; // FNV-1a 64
        foreach (byte b in path)
        {
            byte c = (byte)(b >= 'A' && b <= 'Z' ? b + 32 : b);
            h = (h ^ c) * 1099511628211UL;
        }
        return h;
    }

    static ulong Hash(ReadOnlySpan<char> path)
    {
        return Hash(MemoryMarshal.AsBytes(path));
    }

    private readonly Dictionary<ulong, Entry> _entry = new Dictionary<ulong, Entry>();

    public ICollection<ulong> Keys => _entry.Keys;

    public ICollection<Entry> Values => _entry.Values;

    public int Count => _entry.Count;

    public bool IsReadOnly => false;

    public Entry this[ulong key]
    {
        get => _entry[key];
        set => _entry[key] = value;
    }

    public void Add(Entry entry)
    {
        _entry.Add(Hash(entry.File.FullName), entry);
    }

    public void Add(ulong key, Entry value)
    {
        _entry.Add(key, value);
    }

    public bool ContainsKey(ulong key)
    {
        return _entry.ContainsKey(key);
    }

    public bool Remove(ulong key)
    {
        return _entry.Remove(key);
    }

    public bool TryGetValue(ulong key, [MaybeNullWhen(false)] out Entry value)
    {
        return _entry.TryGetValue(key, out value);
    }

    public void Add(KeyValuePair<ulong, Entry> item)
    {
        _entry.Add(item.Key, item.Value);
    }

    public void Clear()
    {
        _entry.Clear();
    }

    public bool Contains(KeyValuePair<ulong, Entry> item)
    {
        return _entry.Contains(item);
    }

    public void CopyTo(KeyValuePair<ulong, Entry>[] array, int arrayIndex)
    {
        throw new NotImplementedException();
    }

    public bool Remove(KeyValuePair<ulong, Entry> item)
    {
        return _entry.Remove(item.Key);
    }

    public IEnumerator<KeyValuePair<ulong, Entry>> GetEnumerator()
    {
        return _entry.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
