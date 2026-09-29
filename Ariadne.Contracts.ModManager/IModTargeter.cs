using Ariadne.Contracts.Games;

namespace Ariadne.Contracts.ModManager;

public interface IModTargeter
{
    void ApplyAliases(ISupportedGame game, ILibraryMod mod);

    IGamePath GetTarget(ISupportedGame game, ILibraryMod mod);
}
