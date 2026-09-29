using Microsoft.Extensions.DependencyInjection;

namespace Ariadne.WebProtocol.Modl;

public static class ModlWebProtocolServiceExtensions
{
    public static IServiceCollection AddModlWebProtocol(
        this IServiceCollection services,
        ModlRegistrationOptions registrationOptions
    )
    {
        services.AddSingleton(registrationOptions);
        services.AddSingleton<IModlProtocolRegistration>(provider =>
            CreateRegistration(provider.GetRequiredService<ModlRegistrationOptions>())
        );
        return services;
    }

    private static IModlProtocolRegistration CreateRegistration(ModlRegistrationOptions options)
    {
        if (OperatingSystem.IsWindows())
        {
            return new WindowsModlProtocolRegistration(options);
        }

        return new UnsupportedModlProtocolRegistration(options.ApplicationName);
    }
}
