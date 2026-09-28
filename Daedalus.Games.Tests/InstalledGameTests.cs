using Daedalus.Contracts.Games;
using Xunit;

namespace Daedalus.Games.Tests;

public class InstalledGameTests
{
    private static readonly DirectoryInfo InstallPath = new(@"C:\Games\Test Game");

    private sealed class FakeGamePath : IGamePath
    {
        public string Key => "Fake";
        public string? BasedOn => null;
        public string DirectoryPath => "FakeDir";
        public IReadOnlyList<string> Patterns => [];

        public IReadOnlyCollection<string> Aliases => [];

        public int ResolveCount;
        public string Result = "initial";

        public string GetAbsolutePath(IInstalledGame game)
        {
            ResolveCount++;
            return Result;
        }

        public bool Equals(IGamePath? x, IGamePath? y) =>
            x is not null && y is not null && x.Key == y.Key;

        public int GetHashCode(IGamePath obj) => obj.Key.GetHashCode();

        public bool Equals(IGamePath? other) => other is not null && other.Key == Key;
    }

    private static InstalledGame CreateGame()
    {
        var root = new GamePath("Root", "", [], []);
        var data = new GamePath("Data", "Data", [], [], basedOn: "Root");
        var config = new SupportedGame("Test Game", [], new VendorInfo(0, 0), root, [], [data]);
        return new InstalledGame(InstallPath, config);
    }

    [Fact]
    public void LookupAbsolutePath_ReturnsResolvedPath()
    {
        var game = CreateGame();

        var resolved = game.LookupAbsolutePath(game.Configuration["Data"]);

        Assert.Equal(Path.Combine(InstallPath.FullName, "Data"), resolved);
    }

    [Fact]
    public void LookupAbsolutePath_CachesResolutionPerKey()
    {
        var game = CreateGame();
        var fake = new FakeGamePath();

        var first = game.LookupAbsolutePath(fake);
        var second = game.LookupAbsolutePath(fake);

        Assert.Equal("initial", first);
        Assert.Equal(first, second);
        Assert.Equal(1, fake.ResolveCount);
    }

    [Fact]
    public void UpdateAbsolutePath_RecomputesAndUpdatesCache()
    {
        var game = CreateGame();
        var fake = new FakeGamePath();
        game.LookupAbsolutePath(fake);

        fake.Result = "updated";
        var updated = game.UpdateAbsolutePath(fake);

        Assert.Equal("updated", updated);
        Assert.Equal(2, fake.ResolveCount);
        Assert.Equal("updated", game.LookupAbsolutePath(fake));
        Assert.Equal(2, fake.ResolveCount);
    }
}
