using Ariadne.Contracts.ModManager;
using Microsoft.Extensions.DependencyInjection;

namespace Ariadne.ModManager.Bethesda;

public static class BethesdaServiceExtensions
{
    public static IServiceCollection AddBethesdaModManager(this IServiceCollection services)
    {
        services.AddSingleton<ILoadOrderBuilder, SkyrimSELoadOrderBuilder>();
        services.AddSingleton<IArchiveReader, BethesdaArchiveReader>();
        return services;
    }
}
