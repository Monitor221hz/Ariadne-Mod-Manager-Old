namespace Daedalus.Contracts.Configs;

public interface IGamePath
{
    string Key { get; }
    DirectoryInfo Path { get; }
    IReadOnlyList<string> Patterns { get; }
}
