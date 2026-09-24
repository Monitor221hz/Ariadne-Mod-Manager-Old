using System.Reflection;
using Daedalus.Contracts.ModManager;
using Daedalus.Games;
using Daedalus.ModManager.Serialization;
using Daedalus.VFS;
using Daedalus.VFS.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Daedalus.ModManager;

public static class ModManagerServiceExtensions
{
    public static IServiceCollection AddModManager(
        this IServiceCollection services,
        params Assembly[] gameModules
    )
    {
        services.AddMods();
        services.AddGames(gameModules);
        services.ConfigureVFS();
        services.AddSingleton<IInstanceStore>(_ => new InstanceStore(
            new FileInfo(Path.Join(AppContext.BaseDirectory, "instances.json"))
        ));
        services.AddSingleton<IInstanceService, InstanceService>();
        services.AddSingleton<IModManagerPaths>(sp => new ModManagerPaths(
            new DirectoryInfo(AppContext.BaseDirectory),
            sp.GetRequiredService<IInstanceService>()
        ));
        services.AddSingleton<IDeploymentPathsFactory, DeploymentPathsFactory>();
        services.AddSingleton<IModProfileSerializer, ModProfileSerializer>();
        services.AddSingleton<IModProfileEditor, ModProfileEditor>();
        services.AddSingleton<Func<IModProfile, IModDeploymentMethod>>(sp =>
            profile => new VirtualDeploymentMethod(
                sp.GetRequiredService<IVirtualFileSystemFactory>(),
                sp.GetRequiredService<IDeploymentPathsFactory>().Create(profile)
            )
        );
        services.AddSingleton<IModDeploymentMethodFactory, ModDeploymentMethodFactory>();
        return services;
    }
}
