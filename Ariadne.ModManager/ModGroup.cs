using System.Collections;
using System.Drawing;
using Ariadne.Contracts.ModManager;

namespace Ariadne.ModManager;

public class ModGroup : IModGroup
{
    public string Name { get; set; }
    private readonly List<IModListEntry> _mods;
    public Color HeaderColor { get; set; }

    public int Count => _mods.Count;

    public bool IsReadOnly => false;

    public IModListEntry this[int index]
    {
        get => _mods[index];
        set => _mods[index] = value;
    }

    public ModGroup(string name, List<IModListEntry> mods, Color headerColor)
    {
        Name = name;
        _mods = mods;
        HeaderColor = headerColor;
    }

    public ModGroup(string name, List<IModListEntry> mods)
    {
        Name = name;
        _mods = mods;
        HeaderColor = Color.FromArgb(255, 0, 0, 0);
    }

    public int IndexOf(IModListEntry item)
    {
        return _mods.IndexOf(item);
    }

    public void Insert(int index, IModListEntry item)
    {
        _mods.Insert(index, item);
    }

    public void RemoveAt(int index)
    {
        _mods.RemoveAt(index);
    }

    public void Add(IModListEntry item)
    {
        _mods.Add(item);
    }

    public void Clear()
    {
        _mods.Clear();
    }

    public bool Contains(IModListEntry item)
    {
        return _mods.Contains(item);
    }

    public void CopyTo(IModListEntry[] array, int arrayIndex)
    {
        _mods.CopyTo(array, arrayIndex);
    }

    public bool Remove(IModListEntry item)
    {
        return _mods.Remove(item);
    }

    public IEnumerator<IModListEntry> GetEnumerator()
    {
        return _mods.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
