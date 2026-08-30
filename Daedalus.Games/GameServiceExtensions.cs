using System.Reflection;
using Daedalus.Contracts.Games;
using Microsoft.Extensions.DependencyInjection;

namespace Daedalus.Games;

public static class GameServiceExtensions
{
    public static IServiceCollection AddGames(
        this IServiceCollection services,
        params Assembly[] gameModules
    )
    {
        services.AddSingleton<IGameCatalog>(new GameCatalog(gameModules));
        services.AddSingleton<IGameLocator, GameLocator>();
        return services;
    }
}
