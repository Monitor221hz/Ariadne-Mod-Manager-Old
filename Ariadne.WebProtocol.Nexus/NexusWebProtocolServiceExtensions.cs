using Microsoft.Extensions.DependencyInjection;

namespace Ariadne.WebProtocol.Nexus;

public static class NexusWebProtocolServiceExtensions
{
    public static IServiceCollection AddNexusWebProtocol(
        this IServiceCollection services,
        NxmRegistrationOptions registrationOptions,
        string applicationSlug
    )
    {
        services.AddSingleton(registrationOptions);
        services.AddSingleton<INxmProtocolRegistration>(provider =>
            CreateRegistration(provider.GetRequiredService<NxmRegistrationOptions>())
        );
        services.AddSingleton<IWebSocketConnectionFactory, ClientWebSocketConnectionFactory>();
        services.AddSingleton<INexusSsoSessionFactory>(provider => new NexusSsoSessionFactory(
            applicationSlug,
            provider.GetRequiredService<IWebSocketConnectionFactory>()
        ));
        services.AddSingleton<INexusAccountClient>(_ => new NexusAccountClient());
        services.AddSingleton<INexusFileClient>(_ => new NexusFileClient());
        services.AddSingleton<INexusDownloadResolver>(_ => new NexusDownloadResolver());
        return services;
    }

    private static INxmProtocolRegistration CreateRegistration(NxmRegistrationOptions options)
    {
        if (OperatingSystem.IsWindows())
        {
            return new WindowsNxmProtocolRegistration(options);
        }

        return new UnsupportedNxmProtocolRegistration(options.ApplicationName);
    }
}
