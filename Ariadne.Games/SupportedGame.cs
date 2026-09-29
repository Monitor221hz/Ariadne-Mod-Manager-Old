using System.Collections;
using System.Diagnostics.CodeAnalysis;
using Ariadne.Contracts.Games;

namespace Ariadne.Games;

public class SupportedGame : ISupportedGame
{
    public string Name { get; }
    public IReadOnlyList<IPlatformConfiguration> Platforms { get; }
    public IVendorInfo Vendors { get; }
    public IGamePath Root { get; }
    public IReadOnlyList<IGamePath> Deployments { get; }
    public IReadOnlyList<IGamePath> InstallTargets { get; }
    public IEnumerable<string> Keys => _pathNameMap.Keys;
    public IReadOnlyDictionary<string, string> ProtocolGameIds => _protocolGameIds;

    public IEnumerable<IGamePath> Values => _pathNameMap.Values;

    public int Count => _pathNameMap.Count;

    public IGamePath this[string key] => _pathNameMap[key];

    private readonly Dictionary<string, IGamePath> _pathNameMap;

    private readonly Dictionary<string, string> _protocolGameIds;

    public SupportedGame(
        string name,
        IReadOnlyList<IPlatformConfiguration> platforms,
        IVendorInfo vendors,
        IGamePath root,
        IReadOnlyList<IGamePath> deployments,
        IReadOnlyList<IGamePath> installTargets,
        IReadOnlyDictionary<string, string>? protocolGameIds = null
    )
    {
        Name = name;
        Platforms = platforms;
        Vendors = vendors;
        Root = root;
        Deployments = deployments;
        InstallTargets = installTargets;
        var paths = Deployments.Concat(InstallTargets).Prepend(root);
        _pathNameMap = paths.ToDictionary(t => t.Key, t => t);
        _protocolGameIds = (
            protocolGameIds ?? (IReadOnlyDictionary<string, string>)new Dictionary<string, string>()
        ).ToDictionary(
            pair => pair.Key.ToLowerInvariant(),
            pair => pair.Value.ToLowerInvariant(),
            StringComparer.Ordinal
        );
    }

    public bool ContainsKey(string key)
    {
        return _pathNameMap.ContainsKey(key);
    }

    public bool TryGetValue(string key, [MaybeNullWhen(false)] out IGamePath value)
    {
        return _pathNameMap.TryGetValue(key, out value);
    }

    public IEnumerator<KeyValuePair<string, IGamePath>> GetEnumerator()
    {
        return _pathNameMap.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
