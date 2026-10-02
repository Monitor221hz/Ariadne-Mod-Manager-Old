using System.Reflection;
using Ariadne.Contracts.ModManager;
using Ariadne.Games;
using Ariadne.ModManager.Serialization;
using Ariadne.VFS;
using Ariadne.VFS.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Ariadne.ModManager;

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
        services.AddSingleton<IProfileService, ProfileService>();
        services.AddSingleton<IModProfileEditor, ModProfileEditor>();
        services.AddSingleton<IConflictAnalysisService, ConflictAnalysisService>();
        services.AddSingleton<IDeploymentPreviewService, DeploymentPreviewService>();
        services.AddSingleton<IContentMoveService, ContentMoveService>();
        services.AddSingleton<ILibraryImportService, LibraryImportService>();
        services.AddSingleton<IArchiveExtractor, StandardArchiveExtractor>();
        services.AddSingleton<IModTargeter, QuickPatternModTargeter>();
        services.AddSingleton<IModInstaller, StandardModInstaller>();
        services.AddSingleton<IModInstallService, ModInstallService>();
        services.AddSingleton<ILibraryModFactory, InstancedModFactory>(
            sp => new InstancedModFactory(
                sp.GetServices<IArchiveReader>().ToList(),
                sp.GetRequiredService<IModManagerPaths>()
            )
        );
        services.AddSingleton<Func<IModProfile, IModDeploymentMethod>>(sp =>
            profile => new VirtualDeploymentMethod(
                sp.GetRequiredService<IVirtualFileSystemFactory>(),
                sp.GetRequiredService<IDeploymentPathsFactory>().Create(profile)
            )
        );
        services.AddSingleton<IModDeploymentMethodFactory, ModDeploymentMethodFactory>();
        services.AddSingleton<IDeploymentService, DeploymentService>();
        services.AddSingleton<ILaunchTargetService, LaunchTargetService>();
        return services;
    }
}
