using System.Collections;
using System.Runtime.CompilerServices;
using Daedalus.Contracts.Mods;

namespace Daedalus.Mods;

public partial class ModList : IList<IModInfo>, IModList
{
    private readonly List<IModInfo> _looseMods;
    private readonly List<IModGroup> _modGroups;
    public IList<IModInfo> LooseMods => _looseMods;
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

    public ModList(List<IModInfo> looseMods, List<IModGroup> groupedMods)
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

    private IModInfo GetAt(int index)
    {
        if (IsLoose(index))
        {
            return _looseMods[index];
        }
        return GroupOf(index - _looseMods.Count, out int localIndex)[localIndex];
    }

    private void SetAt(int index, IModInfo value)
    {
        if (IsLoose(index))
        {
            _looseMods[index] = value;
            return;
        }
        GroupOf(index - _looseMods.Count, out int localIndex)[localIndex] = value;
    }

    public IModInfo this[int index]
    {
        get => GetAt(index);
        set => SetAt(index, value);
    }

    public int IndexOf(IModInfo item)
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

    public void Insert(int index, IModInfo item)
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

    public void Add(IModInfo item)
    {
        _looseMods.Add(item);
    }

    public void Clear()
    {
        _looseMods.Clear();
        _modGroups.Clear();
    }

    public bool Contains(IModInfo item)
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

    public void CopyTo(IModInfo[] array, int arrayIndex)
    {
        _looseMods.CopyTo(array, arrayIndex);
        arrayIndex += _looseMods.Count;
        foreach (var group in _modGroups)
        {
            group.CopyTo(array, arrayIndex);
            arrayIndex += group.Count;
        }
    }

    public bool Remove(IModInfo item)
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

    IEnumerator<IModInfo> IEnumerable<IModInfo>.GetEnumerator() => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
