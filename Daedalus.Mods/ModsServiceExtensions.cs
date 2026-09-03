using Daedalus.Contracts.Mods;
using Daedalus.Mods.Serialization;
using Microsoft.Extensions.DependencyInjection;

namespace Daedalus.Mods;

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
