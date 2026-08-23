using Daedalus.Contracts.Configs;

namespace Daedalus.Configs;

public class GamePath : IGamePath
{
    public string Key { get; }
    public DirectoryInfo Path { get; }
    public IReadOnlyList<string> Patterns { get; }

    public GamePath(string key, DirectoryInfo path, IReadOnlyList<string> patterns)
    {
        Key = key;
        Path = path;
        Patterns = patterns;
    }
}
