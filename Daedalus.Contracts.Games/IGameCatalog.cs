namespace Daedalus.Contracts.Games;

public interface IGameCatalog
{
    IReadOnlyList<ISupportedGame> Games { get; }
}
