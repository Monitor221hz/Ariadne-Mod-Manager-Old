using Daedalus.Contracts.Games;
using GameFinder.Common;
using GameFinder.RegistryUtils;
using GameFinder.StoreHandlers.Steam;
using GameFinder.StoreHandlers.Steam.Models.ValueTypes;
using NexusMods.Paths;

namespace Daedalus.Games;

public sealed class GameLocator : IGameLocator
{
    private readonly AHandler<SteamGame, AppId> _steamHandler;

    public GameLocator()
        : this(
            new SteamHandler(
                FileSystem.Shared,
                OperatingSystem.IsWindows() ? WindowsRegistry.Shared : null
            )
        ) { }

    public GameLocator(AHandler<SteamGame, AppId> steamHandler)
    {
        _steamHandler = steamHandler;
    }

    public IEnumerable<IInstalledGame> FindInstalledGames(ISupportedGame game)
    {
        return FindInstalledGames([game]);
    }

    public IEnumerable<IInstalledGame> FindInstalledGames(IEnumerable<ISupportedGame> games)
    {
        var wanted = games
            .Where(g => g.Vendors.Steam != 0)
            .Select(g => (Id: AppId.From(g.Vendors.Steam), Game: g))
            .ToList();
        if (wanted.Count == 0)
        {
            yield break;
        }
        var found = _steamHandler.FindAllGamesById(out _);
        foreach (var (id, game) in wanted)
        {
            if (found.TryGetValue(id, out var steamGame))
            {
                yield return new InstalledGame(
                    new DirectoryInfo(steamGame.Path.GetFullPath()),
                    game
                );
            }
        }
    }
}
