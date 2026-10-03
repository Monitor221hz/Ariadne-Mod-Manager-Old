using Ariadne.Contracts.Games;

namespace Ariadne.Contracts.ModManager;

public interface ILoadOrderBuilderResolver
{
    ILoadOrderBuilder? GetFor(ISupportedGame game);
}
