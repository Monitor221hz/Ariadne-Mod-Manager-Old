using Daedalus.Contracts.ModManager;
using Microsoft.Extensions.DependencyInjection;

namespace Daedalus.ModManager;

public static class ModsServiceExtensions
{
    public static IServiceCollection AddMods(this IServiceCollection services)
    {
        services.AddSingleton<IArchiveReader, SharpCompressArchiveReader>();
        services.AddSingleton<ILibraryModSerializer>(sp => new LibraryModSerializer(
            sp.GetServices<IArchiveReader>().ToList()
        ));
        return services;
    }
}
