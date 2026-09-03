using System.Collections;
using System.Drawing;
using Daedalus.Contracts.Mods;

namespace Daedalus.Mods;

public class ModGroup : IModGroup
{
    public string Name { get; }
    private readonly List<ILibraryMod> _mods;
    public Color HeaderColor { get; set; }

    public int Count => _mods.Count;

    public bool IsReadOnly => false;

    public ILibraryMod this[int index]
    {
        get => _mods[index];
        set => _mods[index] = value;
    }

    public ModGroup(string name, List<ILibraryMod> mods, Color headerColor)
    {
        Name = name;
        _mods = mods;
        HeaderColor = headerColor;
    }

    public ModGroup(string name, List<ILibraryMod> mods)
    {
        Name = name;
        _mods = mods;
        HeaderColor = Color.FromArgb(255, 0, 0, 0);
    }

    public int IndexOf(ILibraryMod item)
    {
        return _mods.IndexOf(item);
    }

    public void Insert(int index, ILibraryMod item)
    {
        _mods.Insert(index, item);
    }

    public void RemoveAt(int index)
    {
        _mods.RemoveAt(index);
    }

    public void Add(ILibraryMod item)
    {
        _mods.Add(item);
    }

    public void Clear()
    {
        _mods.Clear();
    }

    public bool Contains(ILibraryMod item)
    {
        return _mods.Contains(item);
    }

    public void CopyTo(ILibraryMod[] array, int arrayIndex)
    {
        _mods.CopyTo(array, arrayIndex);
    }

    public bool Remove(ILibraryMod item)
    {
        return _mods.Remove(item);
    }

    public IEnumerator<ILibraryMod> GetEnumerator()
    {
        return _mods.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
