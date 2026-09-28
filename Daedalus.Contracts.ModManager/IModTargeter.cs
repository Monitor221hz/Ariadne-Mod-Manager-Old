using Daedalus.Contracts.Games;

namespace Daedalus.Contracts.ModManager;

public interface IModTargeter
{
    void ApplyAliases(ISupportedGame game, ILibraryMod mod);

    IGamePath GetTarget(ISupportedGame game, ILibraryMod mod);
}
