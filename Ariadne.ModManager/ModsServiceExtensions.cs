using Ariadne.Contracts.ModManager;
using Microsoft.Extensions.DependencyInjection;

namespace Ariadne.ModManager;

public static class ModsServiceExtensions
{
    public static IServiceCollection AddMods(this IServiceCollection services)
    {
        services.AddSingleton<IArchiveReader, StandardArchiveReader>();
        services.AddSingleton<ILibraryModSerializer>(sp => new LibraryModSerializer(
            sp.GetServices<IArchiveReader>().ToList()
        ));
        return services;
    }
}
