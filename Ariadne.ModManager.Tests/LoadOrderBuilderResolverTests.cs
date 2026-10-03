using Ariadne.Contracts.Games;
using Ariadne.Contracts.ModManager;
using Ariadne.Games;
using Xunit;

namespace Ariadne.ModManager.Tests;

public class LoadOrderBuilderResolverTests
{
    private sealed class StubBuilder(string key) : ILoadOrderBuilder
    {
        public string Key => key;

        public IEnumerable<ILoadOrderInfo> Fetch(IInstalledGame game, IModList mods) => [];

        public void Deploy(
            IInstalledGame game,
            IModDeploymentMethod deploymentMethod,
            IReadOnlyList<ILoadOrderInfo> loadOrderInfos
        ) { }

        public void Save(
            IModProfile currentProfile,
            IReadOnlyList<ILoadOrderInfo> loadOrderInfos
        ) { }

        public IEnumerable<ILoadOrderInfo> Sort(
            IModProfile currentProfile,
            IEnumerable<ILoadOrderInfo> loadOrderInfos
        ) => loadOrderInfos;
    }

    private static SupportedGame GameWithKey(string? key) =>
        new(
            "Test Game",
            [],
            new VendorInfo(0, 0),
            new GamePath("Root", "", []),
            [],
            [],
            loadOrderBuilder: key
        );

    [Fact]
    public void GetFor_ResolvesByKey()
    {
        var builder = new StubBuilder("skyrimse");
        var resolver = new LoadOrderBuilderResolver([builder]);

        Assert.Same(builder, resolver.GetFor(GameWithKey("skyrimse")));
    }

    [Fact]
    public void GetFor_MatchesCaseInsensitively()
    {
        var builder = new StubBuilder("skyrimse");
        var resolver = new LoadOrderBuilderResolver([builder]);

        Assert.Same(builder, resolver.GetFor(GameWithKey("SkyrimSE")));
    }

    [Fact]
    public void GetFor_UnknownKey_ReturnsNull()
    {
        var resolver = new LoadOrderBuilderResolver([new StubBuilder("skyrimse")]);

        Assert.Null(resolver.GetFor(GameWithKey("morrowind")));
    }

    [Fact]
    public void GetFor_GameWithoutKey_ReturnsNull()
    {
        var resolver = new LoadOrderBuilderResolver([new StubBuilder("skyrimse")]);

        Assert.Null(resolver.GetFor(GameWithKey(null)));
    }

    [Fact]
    public void Constructor_DuplicateKeys_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new LoadOrderBuilderResolver([new StubBuilder("a"), new StubBuilder("A")])
        );
    }
}
