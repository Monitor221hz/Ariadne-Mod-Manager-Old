using System.Runtime.Versioning;
using Ariadne.WebProtocol.Modl;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Ariadne.WebProtocol.Modl.Tests;

public class ModlWebProtocolServiceExtensionsTests
{
    private static readonly ModlRegistrationOptions Options = new()
    {
        ApplicationName = "AriadneModlTests",
        ApplicationDescription = "Test host",
        ExecutablePath = @"C:\Ariadne\Ariadne.exe",
    };

    private static ServiceProvider CreateProvider()
    {
        return new ServiceCollection().AddModlWebProtocol(Options).BuildServiceProvider();
    }

    [Fact]
    public void AddModlWebProtocol_ResolvesSuppliedOptions()
    {
        using var provider = CreateProvider();

        var options = provider.GetRequiredService<ModlRegistrationOptions>();

        Assert.Same(Options, options);
    }

    [Fact]
    public void AddModlWebProtocol_RegistersProtocolRegistrationAsSingleton()
    {
        using var provider = CreateProvider();

        var first = provider.GetRequiredService<IModlProtocolRegistration>();
        var second = provider.GetRequiredService<IModlProtocolRegistration>();

        Assert.Same(first, second);
    }

    [SkippableFact]
    [SupportedOSPlatform("windows")]
    public void AddModlWebProtocol_OnWindows_UsesWindowsImplementation()
    {
        Skip.IfNot(OperatingSystem.IsWindows(), "Windows only");

        using var provider = CreateProvider();

        Assert.IsType<WindowsModlProtocolRegistration>(
            provider.GetRequiredService<IModlProtocolRegistration>()
        );
    }
}
