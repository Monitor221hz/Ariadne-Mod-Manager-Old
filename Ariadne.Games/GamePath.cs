using System.Diagnostics.CodeAnalysis;
using Ariadne.Contracts.Games;

namespace Ariadne.Games;

public class GamePath : IGamePath
{
    public string Key { get; }
    public string? BasedOn { get; set; }
    public string DirectoryPath { get; }
    public IReadOnlyCollection<string> Aliases { get; set; }
    public IReadOnlyList<string> Patterns { get; }
    private bool _isAbsolute;
    private string? _cachedAbsolutePath;

    public GamePath(
        string key,
        string directoryPath,
        IReadOnlyList<string>? aliases,
        IReadOnlyList<string> patterns,
        string? basedOn = null
    )
    {
        Key = key;
        DirectoryPath = Environment.ExpandEnvironmentVariables(directoryPath);
        Aliases = aliases?.ToHashSet(StringComparer.OrdinalIgnoreCase) ?? [];
        _isAbsolute = Path.IsPathFullyQualified(DirectoryPath);
        Patterns = patterns;
        BasedOn = basedOn;
    }

    public string GetAbsolutePath(IInstalledGame game)
    {
        if (_isAbsolute)
        {
            return DirectoryPath;
        }
        if (_cachedAbsolutePath != null)
        {
            return _cachedAbsolutePath;
        }
        var gameConfig = game.Configuration;
        if (BasedOn == null)
        {
            _cachedAbsolutePath = Path.Combine(game.InstallPath.FullName, DirectoryPath);
            return _cachedAbsolutePath;
        }
        if (BasedOn != null && gameConfig.TryGetValue(BasedOn, out var basedOnPath))
        {
            _cachedAbsolutePath = Path.Combine(basedOnPath.GetAbsolutePath(game), DirectoryPath);
            return _cachedAbsolutePath;
        }
        return DirectoryPath;
    }

    public bool Equals(IGamePath? x, IGamePath? y)
    {
        return x != null && y != null && x.Key.Equals(y.Key);
    }

    public int GetHashCode([DisallowNull] IGamePath obj)
    {
        return obj.Key.GetHashCode();
    }

    public bool Equals(IGamePath? other)
    {
        return Equals(this, other);
    }
}
