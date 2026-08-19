using System.Collections;
using Microsoft.Win32.SafeHandles;

namespace Daedalus.FileSystem;

public class Mod
{
    public Mod(string name, DirectoryInfo folder, uint priority)
    {
        Name = name;
        Directory = folder;
        Priority = priority;
    }

    public string Name { get; set; }

    public uint Priority { get; set; }

    public DirectoryInfo Directory { get; }

    public override bool Equals(object? obj)
    {
        if (obj is not Mod other)
        {
            return false;
        }
        return Directory.FullName.Equals(other.Directory.FullName);
    }

    public override int GetHashCode()
    {
        return Directory.FullName.GetHashCode();
    }
}

public class ModList : IList<Mod>
{
    private List<Mod> _mods;

    public ModList()
    {
        _mods = new List<Mod>();
    }

    public ModList(IList<Mod> mods)
    {
        _mods = new List<Mod>(mods);
    }

    public Mod this[int index]
    {
        get => _mods[index];
        set => _mods[index] = value;
    }

    public int Count => _mods.Count;

    public bool IsReadOnly => false;

    public void Add(Mod item)
    {
        _mods.Add(item);
    }

    public void Clear()
    {
        _mods.Clear();
    }

    public bool Contains(Mod item)
    {
        return _mods.Contains(item);
    }

    public void CopyTo(Mod[] array, int arrayIndex)
    {
        _mods.CopyTo(array, arrayIndex);
    }

    public IEnumerator<Mod> GetEnumerator()
    {
        return _mods.GetEnumerator();
    }

    public int IndexOf(Mod item)
    {
        return _mods.IndexOf(item);
    }

    public void Insert(int index, Mod item)
    {
        _mods.Insert(index, item);
    }

    public bool Remove(Mod item)
    {
        return _mods.Remove(item);
    }

    public void RemoveAt(int index)
    {
        _mods.RemoveAt(index);
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public void Sort()
    {
        _mods.Sort((a, b) => a.Priority.CompareTo(b.Priority));
    }

    public PathTree Build()
    {
        var tree = new PathTree();
        foreach (var mod in _mods)
        {
            var files = mod.Directory.GetFiles();
            foreach (var file in files)
            {
                var entry = new Entry(file);
                tree.Add(entry);
            }
        }
        return tree;
    }
}
