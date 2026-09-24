using Microsoft.Extensions.DependencyInjection;

namespace Daedalus.Downloads;

public static class DownloadServiceExtensions
{
    public static IServiceCollection AddDownloads(this IServiceCollection services)
    {
        services.AddSingleton<IDownloadManager, DownloadManager>();
        services.AddSingleton<IDownloadQueue, DownloadQueue>();
        return services;
    }
}
