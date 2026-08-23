using Daedalus.VFS.WinFsp;
using Microsoft.Extensions.DependencyInjection;

namespace Daedalus.VFS.Services;

public static class VFSServiceExtensions
{
    public static IServiceCollection ConfigureVFS(this IServiceCollection services)
    {
        if (OperatingSystem.IsWindows())
        {
            services.AddSingleton<IVirtualFileSystem, WinFspVirtualFileSystem>();
        }
        else if (OperatingSystem.IsLinux())
        {
            throw new NotImplementedException();
        }
        services.AddSingleton<Func<IVirtualFileSystem>>(sp =>
            () => sp.GetRequiredService<IVirtualFileSystem>()
        );
        services.AddSingleton<
            IVirtualFileSystemFactory,
            ConstVirtualFileSystemFactory<IVirtualFileSystem>
        >();
        return services;
    }
}
