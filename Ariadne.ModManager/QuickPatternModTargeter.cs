using Ariadne.Contracts.Games;
using Ariadne.Contracts.ModManager;

namespace Ariadne.ModManager;

public class QuickPatternModTargeter : IModTargeter
{
    public IGamePath GetTarget(ISupportedGame game, ILibraryMod mod)
    {
        foreach (var target in game.InstallTargets)
        {
            if (target.Patterns.Any(pattern => mod.Content.Find(pattern).Count > 0))
            {
                return target;
            }
        }
        return game.InstallTargets[0];
    }
}
