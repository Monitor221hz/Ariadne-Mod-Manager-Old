using Daedalus.Contracts.Games;
using Daedalus.Contracts.ModManager;
using Daedalus.Contracts.Mods;
using Daedalus.Mods;
using Daedalus.VFS;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Daedalus.ModManager.Tests;

public class ModManagerServiceExtensionsTests
{
    private static ModProfile CreateProfile(string name) =>
        new(
            name,
            new ModList([], []),
            new Version(1, 0),
            new DirectoryInfo(Path.Combine("C:", "Daedalus", "Profiles", name))
        );

    [Fact]
    public void AddModManager_RegistersFullStack()
    {
        using var provider = new ServiceCollection().AddModManager().BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<IModManagerPaths>());
        Assert.NotNull(provider.GetRequiredService<IDeploymentPathsFactory>());
        Assert.NotNull(provider.GetRequiredService<IModProfileSerializer>());
        Assert.NotNull(provider.GetRequiredService<IModInfoSerializer>());
        Assert.NotNull(provider.GetRequiredService<IGameCatalog>());
        Assert.NotNull(provider.GetRequiredService<IGameLocator>());
        Assert.NotNull(provider.GetRequiredService<IInstalledGameSerializer>());
        Assert.NotNull(provider.GetRequiredService<IVirtualFileSystemFactory>());
    }

    [Fact]
    public void AddModManager_StatelessServicesAreSingletons()
    {
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

    [Fact]
    public void AddModManager_DoesNotRegisterGlobalDeploymentPathsOrMethod()
    {
        using var provider = new ServiceCollection().AddModManager().BuildServiceProvider();

        Assert.Null(provider.GetService<IDeploymentPaths>());
        Assert.Null(provider.GetService<IModDeploymentMethod>());
    }

    [Fact]
    public void AddModManager_DeploymentMethodFactoryYieldsFreshInstancePerProfile()
    {
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

    [Fact]
    public void AddModManager_VirtualFileSystemsAreTransient()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        using var provider = new ServiceCollection().AddModManager().BuildServiceProvider();
        var factory = provider.GetRequiredService<IVirtualFileSystemFactory>();

        using var a = factory.Create();
        using var b = factory.Create();

        Assert.NotSame(a, b);
    }
}
