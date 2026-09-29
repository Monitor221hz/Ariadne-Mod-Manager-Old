using Avalonia;
using Ariadne.Contracts.Games;
using Ariadne.Downloads;
using Ariadne.ModManager.Bethesda;
using Ariadne.Security;
using Ariadne.Security.Dpapi;
using Ariadne.Security.Libsecret;
using Ariadne.WebProtocol;
using Ariadne.WebProtocol.Modl;
using Ariadne.WebProtocol.Nexus;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI.Avalonia;

namespace Ariadne.ModManager.GUI;

sealed class Program
{
    private const string InstanceKey = "ariadne";

    private static readonly IReadOnlyDictionary<string, string> SchemesByOption = new Dictionary<
        string,
        string
    >
    {
        ["--nxm"] = ProtocolSchemes.Nxm,
        ["--modl"] = ProtocolSchemes.Modl,
    };

    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        var intent = ProtocolCommandLine.Parse(args, SchemesByOption);
        if (!InstanceForwarder.TryCreatePrimary(InstanceKey, out var forwarder))
        {
            InstanceForwarder.Forward(InstanceKey, intent);
            return;
        }

        using (forwarder)
        {
            var links = new WebLinkBuffer();
            if (intent.Link is not null && intent.Scheme is not null)
            {
                EnqueueLink(links, intent.Scheme, intent.Link);
            }
            forwarder.PayloadReceived += (_, payload) =>
                EnqueueLink(links, payload.Scheme, payload.Payload);

            var services = new ServiceCollection();
            services.AddModManager(typeof(SkyrimSELoadOrderBuilder).Assembly);
            services.AddBethesdaModManager();
            services.AddDownloads();
            services.AddNexusWebProtocol(
                new NxmRegistrationOptions
                {
                    ApplicationName = "Ariadne",
                    ApplicationDescription = "Ariadne mod manager",
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
                    ApplicationDescription = "Ariadne mod manager",
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
            services.AddSingleton(links);
            services.AddSingleton<NexusLinkProcessor>();
            services.AddSingleton<ModlLinkProcessor>();
            services.AddSingleton<ViewModels.MainViewModel>();
            App.Services = services.BuildServiceProvider();

            _ = App.Services.GetRequiredService<NexusLinkProcessor>();
            _ = App.Services.GetRequiredService<ModlLinkProcessor>();

            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
    }

    private static void EnqueueLink(WebLinkBuffer buffer, string scheme, string raw)
    {
        ISchemeLink? link = scheme switch
        {
            "nxm" => NxmLink.TryParse(raw, out var nxm) ? nxm : null,
            "modl" => ModlLink.TryParse(raw, out var modl) ? modl : null,
            _ => null,
        };
        if (link is not null)
        {
            buffer.Enqueue(link);
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
