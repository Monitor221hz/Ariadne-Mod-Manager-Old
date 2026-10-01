using System.Collections;
using System.Runtime.CompilerServices;
using Ariadne.Contracts.ModManager;

namespace Ariadne.ModManager;

public partial class ModList : IList<IModListEntry>, IModList
{
    private readonly List<IModListEntry> _looseMods;
    private readonly List<IModGroup> _modGroups;
    private readonly HashSet<ILibraryMod> _modSet;
    public IList<IModListEntry> LooseMods => _looseMods;
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

    public ModList(List<IModListEntry> looseMods, List<IModGroup> modGroups)
    {
        _looseMods = looseMods;
        _modGroups = modGroups;
        _modSet = new(looseMods.Concat(modGroups.SelectMany(g => g)).Select(e => e.Mod));
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

    private IModListEntry GetAt(int index)
    {
        if (IsLoose(index))
        {
            return _looseMods[index];
        }
        return GroupOf(index - _looseMods.Count, out int localIndex)[localIndex];
    }

    private void SetAt(int index, IModListEntry value)
    {
        if (IsLoose(index))
        {
            _looseMods[index] = value;
            return;
        }
        GroupOf(index - _looseMods.Count, out int localIndex)[localIndex] = value;
    }

    public IModListEntry this[int index]
    {
        get => GetAt(index);
        set => SetAt(index, value);
    }

    public int IndexOf(IModListEntry item)
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

    public void Insert(int index, IModListEntry item)
    {
        if (index < 0 || index > Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        if (index <= _looseMods.Count)
        {
            if (_modSet.Add(item.Mod))
            {
                _looseMods.Insert(index, item);
            }
            return;
        }

        int localIndex = index - _looseMods.Count;
        for (int groupIndex = 0; groupIndex < _modGroups.Count; groupIndex++)
        {
            var group = _modGroups[groupIndex];
            if (localIndex < group.Count || groupIndex == _modGroups.Count - 1)
            {
                if (_modSet.Add(item.Mod))
                {
                    group.Insert(localIndex, item);
                }

                return;
            }
            localIndex -= group.Count;
        }
    }

    public void RemoveAt(int index)
    {
        if (IsLoose(index))
        {
            _modSet.Remove(_looseMods[index].Mod);
            _looseMods.RemoveAt(index);
            return;
        }
        var group = GroupOf(index - _looseMods.Count, out int localIndex);
        _modSet.Remove(group[localIndex].Mod);
        group.RemoveAt(localIndex);
    }

    public void Add(IModListEntry item)
    {
        if (_modSet.Add(item.Mod))
        {
            _looseMods.Add(item);
        }
    }

    public void Clear()
    {
        _looseMods.Clear();
        _modGroups.Clear();
        _modSet.Clear();
    }

    public bool Contains(IModListEntry item)
    {
        return _modSet.Contains(item.Mod);
    }

    public bool Contains(ILibraryMod mod)
    {
        return _modSet.Contains(mod);
    }

    public void CopyTo(IModListEntry[] array, int arrayIndex)
    {
        _looseMods.CopyTo(array, arrayIndex);
        arrayIndex += _looseMods.Count;
        foreach (var group in _modGroups)
        {
            group.CopyTo(array, arrayIndex);
            arrayIndex += group.Count;
        }
    }

    public bool Remove(IModListEntry item)
    {
        if (!_modSet.Contains(item.Mod))
        {
            return false;
        }

        if (_looseMods.Remove(item))
        {
            _modSet.Remove(item.Mod);
            return true;
        }

        foreach (var group in _modGroups)
        {
            if (group.Remove(item))
            {
                _modSet.Remove(item.Mod);
                return true;
            }
        }
        return false;
    }

    internal void RebuildSet()
    {
        _modSet.Clear();
        _modSet.UnionWith(_looseMods.Select(entry => entry.Mod));
        foreach (var group in _modGroups)
        {
            _modSet.UnionWith(group.Select(entry => entry.Mod));
        }
    }

    public ModListEnumerator GetEnumerator() => new(this);

    IEnumerator<IModListEntry> IEnumerable<IModListEntry>.GetEnumerator() => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
