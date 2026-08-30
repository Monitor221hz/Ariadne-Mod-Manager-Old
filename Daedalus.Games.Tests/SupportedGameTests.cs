using Daedalus.Contracts.Games;
using Xunit;

namespace Daedalus.Games.Tests;

public class SupportedGameTests
{
    private static SupportedGame CreateGame()
    {
        var root = new GamePath("Root", "", []);
        var appData = new GamePath("AppData", @"D:\AppData\Test Game", []);
        var data = new GamePath("Data", "Data", [], basedOn: "Root");
        return new SupportedGame(
            "Test Game",
            [],
            new VendorInfo(123, 456),
            root,
            [appData],
            [data]
        );
    }

    [Fact]
    public void Constructor_ExposesCtorArguments()
    {
        var game = CreateGame();

        Assert.Equal("Test Game", game.Name);
        Assert.Equal(123u, game.Vendors.Steam);
        Assert.Equal(456, game.Vendors.GOG);
        Assert.Equal("Root", game.Root.Key);
        Assert.Single(game.Deployments);
        Assert.Single(game.InstallTargets);
    }

    [Fact]
    public void PathMap_ContainsRootDeploymentsAndInstallTargets()
    {
        var game = CreateGame();

        Assert.Equal(3, game.Count);
        Assert.True(game.ContainsKey("Root"));
        Assert.True(game.ContainsKey("AppData"));
        Assert.True(game.ContainsKey("Data"));
        Assert.False(game.ContainsKey("Missing"));
    }

    [Fact]
    public void PathMap_Indexer_ReturnsMatchingPath()
    {
        var game = CreateGame();

        Assert.Equal("Data", game["Data"].Key);
        Assert.Equal("AppData", game["AppData"].Key);
        Assert.Throws<KeyNotFoundException>(() => game["Missing"]);
    }

    [Fact]
    public void PathMap_TryGetValue()
    {
        var game = CreateGame();

        Assert.True(game.TryGetValue("Data", out var data));
        Assert.NotNull(data);
        Assert.Equal("Data", data.Key);

        Assert.False(game.TryGetValue("Missing", out var missing));
        Assert.Null(missing);
    }

    [Fact]
    public void PathMap_KeysAndValues()
    {
        var game = CreateGame();

        Assert.Equal(["AppData", "Data", "Root"], game.Keys.Order());
        Assert.Equal(3, game.Values.Count());
        Assert.All(game.Values, v => Assert.Contains(v.Key, game.Keys));
    }

    [Fact]
    public void PathMap_EnumeratesAsKeyValuePairs()
    {
        var game = CreateGame();

        var entries = game.ToList();

        Assert.Equal(3, entries.Count);
        Assert.All(entries, kvp => Assert.Same(kvp.Value, game[kvp.Key]));
        Assert.Same(game.Root, game["Root"]);
    }

    [Fact]
    public void Constructor_DuplicateKeysAcrossDeploymentsAndTargets_Throws()
    {
        var root = new GamePath("Root", "", []);
        var a = new GamePath("Data", "Data", []);
        var b = new GamePath("Data", "Other", []);

        Assert.Throws<ArgumentException>(() =>
            new SupportedGame("Test Game", [], new VendorInfo(0, 0), root, [a], [b])
        );
    }
}
