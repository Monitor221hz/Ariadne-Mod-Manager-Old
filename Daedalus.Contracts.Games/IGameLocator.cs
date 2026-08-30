namespace Daedalus.Contracts.Games;

public interface IGameLocator
{
    IEnumerable<IInstalledGame> FindInstalledGames(ISupportedGame game);

    IEnumerable<IInstalledGame> FindInstalledGames(IEnumerable<ISupportedGame> games);
}
