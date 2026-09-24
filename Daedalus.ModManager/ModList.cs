using System.Collections;
using System.Runtime.CompilerServices;
using Daedalus.Contracts.ModManager;

namespace Daedalus.ModManager;

public partial class ModList : IList<ILibraryMod>, IModList
{
    private readonly List<ILibraryMod> _looseMods;
    private readonly List<IModGroup> _modGroups;
    public IList<ILibraryMod> LooseMods => _looseMods;
    public IList<IModGroup> ModGroups => _modGroups;

    public int Count
    {
        get
        {
            int count = _looseMods.Count;
            for (int i = 0; i < _modGroups.Count; i++)
            {
                count += _modGroups[i].Count;
            }
            return count;
        }
    }

    public bool IsReadOnly => false;

    public ModList(List<ILibraryMod> looseMods, List<IModGroup> groupedMods)
    {
        _looseMods = looseMods;
        _modGroups = groupedMods;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool IsLoose(int index) => index < _looseMods.Count;

    private IModGroup GroupOf(int localIndex, out int indexInGroup)
    {
        for (int groupIndex = 0; groupIndex < _modGroups.Count; groupIndex++)
        {
            var group = _modGroups[groupIndex];
            if (localIndex < group.Count)
            {
                indexInGroup = localIndex;
                return group;
            }
            localIndex -= group.Count;
        }
        throw new ArgumentOutOfRangeException("index");
    }

    private ILibraryMod GetAt(int index)
    {
        if (IsLoose(index))
        {
            return _looseMods[index];
        }
        return GroupOf(index - _looseMods.Count, out int localIndex)[localIndex];
    }

    private void SetAt(int index, ILibraryMod value)
    {
        if (IsLoose(index))
        {
            _looseMods[index] = value;
            return;
        }
        GroupOf(index - _looseMods.Count, out int localIndex)[localIndex] = value;
    }

    public ILibraryMod this[int index]
    {
        get => GetAt(index);
        set => SetAt(index, value);
    }

    public int IndexOf(ILibraryMod item)
    {
        int index = _looseMods.IndexOf(item);
        if (index != -1)
        {
            return index;
        }
        int groupIndex = 0;
        foreach (var group in _modGroups)
        {
            index = group.IndexOf(item);
            if (index != -1)
            {
                return _looseMods.Count + groupIndex + index;
            }
            groupIndex += group.Count;
        }
        return -1;
    }

    public void Insert(int index, ILibraryMod item)
    {
        if (index < 0 || index > Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        if (index <= _looseMods.Count)
        {
            _looseMods.Insert(index, item);
            return;
        }

        int localIndex = index - _looseMods.Count;
        for (int groupIndex = 0; groupIndex < _modGroups.Count; groupIndex++)
        {
            var group = _modGroups[groupIndex];
            if (localIndex < group.Count || groupIndex == _modGroups.Count - 1)
            {
                group.Insert(localIndex, item);
                return;
            }
            localIndex -= group.Count;
        }
    }

    public void RemoveAt(int index)
    {
        if (IsLoose(index))
        {
            _looseMods.RemoveAt(index);
            return;
        }
        GroupOf(index - _looseMods.Count, out int localIndex).RemoveAt(localIndex);
    }

    public void Add(ILibraryMod item)
    {
        _looseMods.Add(item);
    }

    public void Clear()
    {
        _looseMods.Clear();
        _modGroups.Clear();
    }

    public bool Contains(ILibraryMod item)
    {
        if (_looseMods.Contains(item))
        {
            return true;
        }
        foreach (var group in _modGroups)
        {
            if (group.Contains(item))
            {
                return true;
            }
        }
        return false;
    }

    public void CopyTo(ILibraryMod[] array, int arrayIndex)
    {
        _looseMods.CopyTo(array, arrayIndex);
        arrayIndex += _looseMods.Count;
        foreach (var group in _modGroups)
        {
            group.CopyTo(array, arrayIndex);
            arrayIndex += group.Count;
        }
    }

    public bool Remove(ILibraryMod item)
    {
        if (_looseMods.Remove(item))
        {
            return true;
        }

        foreach (var group in _modGroups)
        {
            if (group.Remove(item))
            {
                return true;
            }
        }
        return false;
    }

    public ModListEnumerator GetEnumerator() => new(this);

    IEnumerator<ILibraryMod> IEnumerable<ILibraryMod>.GetEnumerator() => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
