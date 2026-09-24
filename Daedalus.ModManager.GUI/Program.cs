using Avalonia;
using Daedalus.Downloads;
using Daedalus.ModManager.Bethesda;
using Daedalus.Security;
using Daedalus.Security.Dpapi;
using Daedalus.Security.Libsecret;
using Daedalus.WebProtocol.Modl;
using Daedalus.WebProtocol.Nexus;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI.Avalonia;

namespace Daedalus.ModManager.GUI;

sealed class Program
{
    private const string InstanceKey = "ariadne";

    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        var intent = NexusCommandLine.Parse(args);
        if (!NexusInstanceForwarder.TryCreatePrimary(InstanceKey, out var forwarder))
        {
            NexusInstanceForwarder.Forward(InstanceKey, intent);
            return;
        }

        using (forwarder)
        {
            var nxmLinks = new NexusLinkBuffer();
            if (intent.Link is not null)
            {
                nxmLinks.Enqueue(intent.Link);
            }
            forwarder.LinkReceived += (_, link) => nxmLinks.Enqueue(link);

            var services = new ServiceCollection();
            services.AddModManager(typeof(SkyrimSELoadOrderBuilder).Assembly);
            services.AddBethesdaModManager();
            services.AddDownloads();
            services.AddNexusWebProtocol(
                new NxmRegistrationOptions
                {
                    ApplicationName = "Ariadne",
                    ApplicationDescription = "Daedalus mod manager",
                    ExecutablePath =
                        Environment.ProcessPath
                        ?? Path.Combine(AppContext.BaseDirectory, "Ariadne.exe"),
                },
                InstanceKey
            );
            services.AddModlWebProtocol(
                new ModlRegistrationOptions
                {
                    ApplicationName = "Ariadne",
                    ApplicationDescription = "Daedalus mod manager",
                    ExecutablePath =
                        Environment.ProcessPath
                        ?? Path.Combine(AppContext.BaseDirectory, "Ariadne.exe"),
                }
            );
            services.AddSingleton<ISecretStore>(_ =>
                OperatingSystem.IsWindows()
                    ? new DpapiSecretStore("Ariadne")
                    : new LibsecretSecretStore("Ariadne")
            );
            services.AddSingleton<IUrlLauncher, UrlLauncher>();
            services.AddSingleton<INexusAccountCache>(_ => new NexusAccountCache("Ariadne"));
            services.AddSingleton<ViewModels.SourcesMenuViewModel>();
            services.AddSingleton(nxmLinks);
            services.AddSingleton<NexusLinkProcessor>();
            services.AddSingleton<ViewModels.MainViewModel>();
            App.Services = services.BuildServiceProvider();

            _ = App.Services.GetRequiredService<NexusLinkProcessor>();

            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder
            .Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace()
            .UseReactiveUI(_ => { });
}
