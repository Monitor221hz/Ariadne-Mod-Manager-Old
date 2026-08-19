using System.Runtime.InteropServices;

namespace Daedalus.FileSystem;

public readonly struct Entry
{
    public enum Flags
    {
        None = 0 << 0,
        Whiteout = 1 << 0,
    }

    private readonly byte[] _key;
    public readonly FileInfo File;

    public Entry(FileInfo file)
    {
        File = file;
        
        _key = MemoryMarshal.AsBytes(file.FullName.AsSpan()).ToArray();
    }

    public override int GetHashCode()
    {
        return File.FullName.GetHashCode();
    }

    public override bool Equals(object? obj)
    {
        return obj is Entry other && File.FullName == other.File.FullName;
    }

    public static bool operator ==(Entry left, Entry right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(Entry left, Entry right)
    {
        return !(left == right);
    }
}
