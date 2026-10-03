using Ariadne.Contracts.Games;
using Ariadne.Contracts.ModManager;

namespace Ariadne.ModManager;

public sealed class LoadOrderBuilderResolver : ILoadOrderBuilderResolver
{
    private readonly IReadOnlyDictionary<string, ILoadOrderBuilder> _byKey;

    public LoadOrderBuilderResolver(IEnumerable<ILoadOrderBuilder> builders)
    {
        _byKey = builders.ToDictionary(builder => builder.Key, StringComparer.OrdinalIgnoreCase);
    }

    public ILoadOrderBuilder? GetFor(ISupportedGame game) =>
        game.LoadOrderBuilder is { Length: > 0 } key ? _byKey.GetValueOrDefault(key) : null;
}
