using Ariadne.Contracts.ModManager;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Ariadne.ModManager.Tests;

public class ModsServiceExtensionsTests
{
    [Fact]
    public void AddMods_RegistersModInfoSerializerAsSingleton()
    {
        using var provider = new ServiceCollection().AddMods().BuildServiceProvider();

        var first = provider.GetRequiredService<ILibraryModSerializer>();
        var second = provider.GetRequiredService<ILibraryModSerializer>();

        Assert.NotNull(first);
        Assert.Same(first, second);
    }
}
