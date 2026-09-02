using System.Collections;
using Daedalus.Contracts.Mods;

namespace Daedalus.Mods;

public partial class ModList
{
    public struct ModListEnumerator : IEnumerator<IModInfo>
    {
        private readonly ModList _list;
        private int _looseIndex;
        private int _groupIndex;
        private int _modIndex;
        private IModInfo? _current;

        internal ModListEnumerator(ModList list)
        {
            _list = list;
            _looseIndex = -1;
            _groupIndex = -1;
            _modIndex = -1;
            _current = null;
        }

        public readonly IModInfo Current => _current!;

        readonly object IEnumerator.Current => Current;

        public bool MoveNext()
        {
            if (_groupIndex < 0)
            {
                var loose = _list._looseMods;
                if (++_looseIndex < loose.Count)
                {
                    _current = loose[_looseIndex];
                    return true;
                }
                _groupIndex = 0;
                _modIndex = -1;
            }

            var groups = _list._modGroups;
            while (_groupIndex < groups.Count)
            {
                var group = groups[_groupIndex];
                if (++_modIndex < group.Count)
                {
                    _current = group[_modIndex];
                    return true;
                }
                _groupIndex++;
                _modIndex = -1;
            }

            _current = null;
            return false;
        }

        public void Reset()
        {
            _looseIndex = -1;
            _groupIndex = -1;
            _modIndex = -1;
            _current = null;
        }

        public readonly void Dispose() { }
    }
}
