using System.Runtime.Versioning;
using Ariadne.WebProtocol.Nexus;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Ariadne.WebProtocol.Nexus.Tests;

public class NexusWebProtocolServiceExtensionsTests
{
    private static readonly NxmRegistrationOptions Options = new()
    {
        ApplicationName = "AriadneNxmTests",
        ApplicationDescription = "Test host",
        ExecutablePath = @"C:\Ariadne\Ariadne.exe",
    };

    private static ServiceProvider CreateProvider()
    {
        return new ServiceCollection()
            .AddNexusWebProtocol(Options, "Ariadne")
            .BuildServiceProvider();
    }

    [Fact]
    public void AddNexusWebProtocol_ResolvesSuppliedOptions()
    {
        using var provider = CreateProvider();

        var options = provider.GetRequiredService<NxmRegistrationOptions>();

        Assert.Same(Options, options);
    }

    [Fact]
    public void AddNexusWebProtocol_RegistersProtocolRegistrationAsSingleton()
    {
        using var provider = CreateProvider();

        var first = provider.GetRequiredService<INxmProtocolRegistration>();
        var second = provider.GetRequiredService<INxmProtocolRegistration>();

        Assert.Same(first, second);
    }

    [SkippableFact]
    [SupportedOSPlatform("windows")]
    public void AddNexusWebProtocol_OnWindows_UsesWindowsImplementation()
    {
        Skip.IfNot(OperatingSystem.IsWindows(), "Windows only");

        using var provider = CreateProvider();

        Assert.IsType<WindowsNxmProtocolRegistration>(
            provider.GetRequiredService<INxmProtocolRegistration>()
        );
    }

    [Fact]
    public void AddNexusWebProtocol_RegistersSocketFactory()
    {
        using var provider = CreateProvider();

        var factory = provider.GetRequiredService<IWebSocketConnectionFactory>();

        Assert.IsType<ClientWebSocketConnection>(factory.Create());
    }

    [Fact]
    public void AddNexusWebProtocol_SessionFactoryCreatesTransientSessionsWithDistinctIds()
    {
        using var provider = CreateProvider();

        var factory = provider.GetRequiredService<INexusSsoSessionFactory>();
        var first = factory.Create();
        var second = factory.Create();

        Assert.NotSame(first, second);
        Assert.NotEqual(first.AuthorizationUri, second.AuthorizationUri);
    }

    [Fact]
    public void AddNexusWebProtocol_RegistersAccountClient()
    {
        using var provider = CreateProvider();

        var client = provider.GetRequiredService<INexusAccountClient>();

        Assert.NotNull(client);
    }

    [Fact]
    public void AddNexusWebProtocol_RegistersDownloadResolver()
    {
        using var provider = CreateProvider();

        var resolver = provider.GetRequiredService<INexusDownloadResolver>();

        Assert.NotNull(resolver);
    }
}
