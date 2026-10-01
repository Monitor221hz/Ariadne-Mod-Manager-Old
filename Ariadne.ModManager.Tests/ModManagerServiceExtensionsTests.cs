using Ariadne.Contracts.Games;
using Ariadne.Contracts.ModManager;
using Ariadne.ModManager;
using Ariadne.VFS;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Ariadne.ModManager.Tests;

public class ModManagerServiceExtensionsTests
{
    private static void SkipNonWindows() =>
        Skip.If(
            !OperatingSystem.IsWindows(),
            "Requires VFS support not implemented on this platform"
        );

    private static ModProfile CreateProfile(string name) =>
        new(
            name,
            new ModList([], []),
            new Version(1, 0),
            new DirectoryInfo(Path.Combine("C:", "Ariadne", "Profiles", name))
        );

    [SkippableFact]
    public void AddModManager_RegistersFullStack()
    {
        SkipNonWindows();

        using var provider = new ServiceCollection().AddModManager().BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<IModManagerPaths>());
        Assert.NotNull(provider.GetRequiredService<IDeploymentPathsFactory>());
        Assert.NotNull(provider.GetRequiredService<IModProfileSerializer>());
        Assert.NotNull(provider.GetRequiredService<IProfileService>());
        Assert.NotNull(provider.GetRequiredService<ILibraryModSerializer>());
        Assert.NotNull(provider.GetRequiredService<IGameCatalog>());
        Assert.NotNull(provider.GetRequiredService<IGameLocator>());
        Assert.NotNull(provider.GetRequiredService<IInstalledGameSerializer>());
        Assert.NotNull(provider.GetRequiredService<IVirtualFileSystemFactory>());
    }

    [SkippableFact]
    public void AddModManager_StatelessServicesAreSingletons()
    {
        SkipNonWindows();

        using var provider = new ServiceCollection().AddModManager().BuildServiceProvider();

        Assert.Same(
            provider.GetRequiredService<IModManagerPaths>(),
            provider.GetRequiredService<IModManagerPaths>()
        );
        Assert.Equal(
            new DirectoryInfo(AppContext.BaseDirectory).FullName,
            provider.GetRequiredService<IModManagerPaths>().AssemblyFolder.FullName
        );
        Assert.Same(
            provider.GetRequiredService<IDeploymentPathsFactory>(),
            provider.GetRequiredService<IDeploymentPathsFactory>()
        );
        Assert.Same(
            provider.GetRequiredService<IModProfileSerializer>(),
            provider.GetRequiredService<IModProfileSerializer>()
        );
    }

    [SkippableFact]
    public void AddModManager_DoesNotRegisterGlobalDeploymentPathsOrMethod()
    {
        SkipNonWindows();

        using var provider = new ServiceCollection().AddModManager().BuildServiceProvider();

        Assert.Null(provider.GetService<IDeploymentPaths>());
        Assert.Null(provider.GetService<IModDeploymentMethod>());
    }

    [SkippableFact]
    public void AddModManager_DeploymentMethodFactoryYieldsFreshInstancePerProfile()
    {
        SkipNonWindows();

        using var temp = new TempDirectory();
        var services = new ServiceCollection().AddModManager();
        services.AddSingleton<IInstanceStore>(_ => new InstanceStore(
            new FileInfo(Path.Combine(temp.Path, "instances.json"))
        ));
        using var provider = services.BuildServiceProvider();
        provider
            .GetRequiredService<IInstanceService>()
            .Create(
                "test",
                new DirectoryInfo(Path.Combine(temp.Path, "instance")),
                TestAssets.GameAt(new DirectoryInfo(Path.Combine(temp.Path, "game")))
            );

        var factory = provider.GetRequiredService<IModDeploymentMethodFactory>();
        Assert.Same(factory, provider.GetRequiredService<IModDeploymentMethodFactory>());

        using var first = factory.Create(CreateProfile("Main"));
        using var second = factory.Create(CreateProfile("Main"));
        using var otherProfile = factory.Create(CreateProfile("Second"));

        Assert.NotSame(first, second);
        Assert.NotSame(first, otherProfile);
    }

    [SkippableFact]
    public void AddModManager_VirtualFileSystemsAreTransient()
    {
        SkipNonWindows();

        using var provider = new ServiceCollection().AddModManager().BuildServiceProvider();
        var factory = provider.GetRequiredService<IVirtualFileSystemFactory>();

        using var a = factory.Create();
        using var b = factory.Create();

        Assert.NotSame(a, b);
    }
}
