using Daedalus.VFS;
using Daedalus.VFS.Services;
using Daedalus.VFS.WinFsp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Spectre.Console;

namespace Daedalus.ModManager.CLI;

internal class Program
{
    static async Task Main(string[] args)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

        ConfigureServices(builder.Services);

        await builder.Build().RunAsync();
    }

    static void ConfigureServices(IServiceCollection services)
    {
        services.ConfigureVFS();
        services.AddHostedService<Application>();
    }
}

public class Application(IVirtualFileSystemFactory vfsFactory, IHostApplicationLifetime lifetime)
    : IHostedService
{
    private readonly IVirtualFileSystemFactory _vfsFactory = vfsFactory;
    private readonly IHostApplicationLifetime _lifetime = lifetime;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        AnsiConsole.MarkupLine("[green]✓ adfasodfasdf[/]");
        await AnsiConsole.ConfirmAsync("Meep?", cancellationToken: cancellationToken);
        AnsiConsole.Clear();
        _lifetime.StopApplication();
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
