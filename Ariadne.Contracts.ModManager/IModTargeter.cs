using Ariadne.Contracts.Games;

namespace Ariadne.Contracts.ModManager;

public interface IModTargeter
{
    IGamePath GetTarget(ISupportedGame game, ILibraryMod mod);
}
