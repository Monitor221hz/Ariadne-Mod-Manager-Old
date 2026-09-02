using System.Reflection;
using Daedalus.Contracts.Games;
using Daedalus.Games.Serialization;
using Microsoft.Extensions.DependencyInjection;

namespace Daedalus.Games;

public static class GameServiceExtensions
{
    public static IServiceCollection AddGames(
        this IServiceCollection services,
        IEnumerable<Assembly> gameModules,
        DirectoryInfo? looseConfigDirectory = null
    )
    {
        services.AddSingleton<IGameCatalog>(_ => new GameCatalog(
            gameModules,
            looseConfigDirectory
        ));
        services.AddSingleton<IGameLocator, GameLocator>();
        services.AddSingleton<IInstalledGameSerializer, InstalledGameSerializer>();
        return services;
    }

    public static IServiceCollection AddGames(
        this IServiceCollection services,
        params Assembly[] gameModules
    ) => services.AddGames((IEnumerable<Assembly>)gameModules);
}
