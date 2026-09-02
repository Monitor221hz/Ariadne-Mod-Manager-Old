using Daedalus.Contracts.ModManager;
using Daedalus.ModManager.Serialization;
using Daedalus.Mods;
using Microsoft.Extensions.DependencyInjection;

namespace Daedalus.ModManager;

public static class ModManagerServiceExtensions
{
    public static IServiceCollection AddModManager(this IServiceCollection services)
    {
        services.AddMods();
        services.AddSingleton<IModManagerPaths>(_ => new ModManagerPaths());
        services.AddSingleton<IDeploymentPathsFactory, DeploymentPathsFactory>();
        services.AddSingleton<IModProfileSerializer, ModProfileSerializer>();
        return services;
    }
}
