using Daedalus.ModManager;
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
        services.AddModManager();
        services.AddHostedService<Application>();
    }
}

public class Application(IHostApplicationLifetime lifetime) : IHostedService
{
    private readonly IHostApplicationLifetime _lifetime = lifetime;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        AnsiConsole.Clear();

        using Stream fontStream = typeof(Application).Assembly.GetManifestResourceStream(
            "Daedalus.ModManager.CLI.Fonts.Caligraphy.flf"
        )!;
        FigletFont font = FigletFont.Load(fontStream);
        AnsiConsole.Write(new FigletText(font, "Daedalus").Color(Color.CornflowerBlue));

        _lifetime.StopApplication();
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
