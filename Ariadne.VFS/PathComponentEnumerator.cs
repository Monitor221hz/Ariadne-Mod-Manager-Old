namespace Ariadne.VFS;

public ref struct PathComponentEnumerator
{
    private readonly ReadOnlySpan<char> _path;
    private int _pos;

    public PathComponentEnumerator(ReadOnlySpan<char> path)
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
