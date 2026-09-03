namespace Daedalus.Contracts.ModManager;

public interface IInstanceStore
{
    IReadOnlyDictionary<string, DirectoryInfo> Instances { get; }
    string? LastActive { get; }

    void Add(string name, DirectoryInfo folder);
    void Remove(string name);
    void SetLastActive(string? name);
}
