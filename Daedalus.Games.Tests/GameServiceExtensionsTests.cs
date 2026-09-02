using Daedalus.Contracts.Games;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Daedalus.Games.Tests;

public class GameServiceExtensionsTests : IDisposable
{
    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } =
            System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "DaedalusTests-" + Guid.NewGuid().ToString("N")
            );

        public TempDirectory() => Directory.CreateDirectory(Path);

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, true);
            }
        }
    }

    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void AddGames_RegistersInstalledGameSerializerAsSingleton()
    {
        using var provider = new ServiceCollection().AddGames().BuildServiceProvider();

        var first = provider.GetRequiredService<IInstalledGameSerializer>();
        var second = provider.GetRequiredService<IInstalledGameSerializer>();

        Assert.NotNull(first);
        Assert.Same(first, second);
    }

    [Fact]
    public void AddGames_BuildsCatalogLazily_OnFirstResolve()
    {
        var looseDir = new DirectoryInfo(_temp.Path);
        using var provider = new ServiceCollection().AddGames([], looseDir).BuildServiceProvider();
        // Config appears only after registration: a factory-registered catalog must pick it up.
        File.WriteAllText(
            System.IO.Path.Combine(_temp.Path, "LooseGame.json"),
            """
            {
              "Name": "LooseGame",
              "Platforms": [],
              "Vendors": { "Steam": 42, "GOG": 0 },
              "Root": { "Key": "Root", "DirectoryPath": "", "Patterns": [] },
              "Deployments": [],
              "InstallTargets": []
            }
            """
        );

        var catalog = provider.GetRequiredService<IGameCatalog>();

        var game = Assert.Single(catalog.Games);
        Assert.Equal("LooseGame", game.Name);
        Assert.Equal(42u, game.Vendors.Steam);
    }
}
