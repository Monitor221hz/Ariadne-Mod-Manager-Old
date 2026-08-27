using Daedalus.Contracts.Games;

namespace Daedalus.Games;

public class InstalledGame : IInstalledGame
{
    public DirectoryInfo InstallPath { get; }
    public ISupportedGame Configuration { get; }
    private Dictionary<string, string> _absolutePathKeyMap = new Dictionary<string, string>();

    public InstalledGame(DirectoryInfo installPath, ISupportedGame configuration)
    {
        InstallPath = installPath;
        Configuration = configuration;
    }

    public string LookupAbsolutePath(IGamePath path)
    {
        if (_absolutePathKeyMap.TryGetValue(path.Key, out var cachedPath))
        {
            return cachedPath;
        }
        _absolutePathKeyMap[path.Key] = path.GetAbsolutePath(this);
        return _absolutePathKeyMap[path.Key];
    }

    public string UpdateAbsolutePath(IGamePath path)
    {
        _absolutePathKeyMap[path.Key] = path.GetAbsolutePath(this);
        return _absolutePathKeyMap[path.Key];
    }
}
