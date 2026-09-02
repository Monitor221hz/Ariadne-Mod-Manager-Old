using Daedalus.Contracts.ModManager;
using Daedalus.Contracts.Mods;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Daedalus.ModManager.Tests;

public class ModManagerServiceExtensionsTests
{
    [Fact]
    public void AddModManager_RegistersSingletons()
    {
        using var provider = new ServiceCollection().AddModManager().BuildServiceProvider();

        var paths = provider.GetRequiredService<IModManagerPaths>();
        Assert.Equal(
            new DirectoryInfo(AppContext.BaseDirectory).FullName,
            paths.AssemblyFolder.FullName
        );
        Assert.Same(paths, provider.GetRequiredService<IModManagerPaths>());

        var factory = provider.GetRequiredService<IDeploymentPathsFactory>();
        Assert.Same(factory, provider.GetRequiredService<IDeploymentPathsFactory>());

        Assert.Same(
            provider.GetRequiredService<IModProfileSerializer>(),
            provider.GetRequiredService<IModProfileSerializer>()
        );
        Assert.Same(
            provider.GetRequiredService<IModInfoSerializer>(),
            provider.GetRequiredService<IModInfoSerializer>()
        );
    }

    [Fact]
    public void AddModManager_DoesNotRegisterGlobalDeploymentPaths()
    {
        // Overwrite is profile-scoped: IDeploymentPaths must be created per profile
        // through IDeploymentPathsFactory, never resolved globally.
        using var provider = new ServiceCollection().AddModManager().BuildServiceProvider();

        Assert.Null(provider.GetService<IDeploymentPaths>());
        Assert.Null(provider.GetService<IModDeploymentMethod>());
    }
}
