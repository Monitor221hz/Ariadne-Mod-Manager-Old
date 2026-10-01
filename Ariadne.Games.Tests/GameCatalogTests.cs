using Ariadne.Contracts.Games;
using Ariadne.ModManager.Bethesda;
using Xunit;

namespace Ariadne.Games.Tests;

public class GameCatalogTests : IDisposable
{
    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } =
            System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "AriadneTests-" + Guid.NewGuid().ToString("N")
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

    private static string MinimalGameJson(string name, int steam) =>
        $$"""
            {
              "Name": "{{name}}",
              "Platforms": [],
              "Vendors": { "Steam": {{steam}}, "GOG": 0 },
              "Root": { "Key": "Root", "DirectoryPath": "", "Patterns": [] },
              "Deployments": [],
              "InstallTargets": []
            }
            """;

    [Fact]
    public void Catalog_LoadsAllValidGameConfigsFromDirectory()
    {
        File.WriteAllText(
            System.IO.Path.Combine(_temp.Path, "GameA.json"),
            MinimalGameJson("GameA", 1)
        );
        File.WriteAllText(
            System.IO.Path.Combine(_temp.Path, "GameB.json"),
            MinimalGameJson("GameB", 2)
        );

        var catalog = new GameCatalog(new DirectoryInfo(_temp.Path));

        Assert.Equal(2, catalog.Games.Count);
        Assert.Contains(catalog.Games, g => g.Name == "GameA" && g.Vendors.Steam == 1);
        Assert.Contains(catalog.Games, g => g.Name == "GameB" && g.Vendors.Steam == 2);
    }

    [Fact]
    public void Catalog_MissingDirectory_YieldsEmpty()
    {
        var missing = new DirectoryInfo(System.IO.Path.Combine(_temp.Path, "nope"));

        var catalog = new GameCatalog(missing);

        Assert.Empty(catalog.Games);
    }

    [Fact]
    public void Catalog_SkipsInvalidConfigs()
    {
        File.WriteAllText(
            System.IO.Path.Combine(_temp.Path, "Valid.json"),
            MinimalGameJson("Valid", 7)
        );
        File.WriteAllText(System.IO.Path.Combine(_temp.Path, "Broken.json"), "{ this is not json");
        File.WriteAllText(
            System.IO.Path.Combine(_temp.Path, "MissingFields.json"),
            """{ "Name": "Incomplete" }"""
        );

        var catalog = new GameCatalog(new DirectoryInfo(_temp.Path));

        var game = Assert.Single(catalog.Games);
        Assert.Equal("Valid", game.Name);
    }

    [SkippableFact]
    public void Catalog_LoadsEmbeddedSkyrimSEConfig()
    {
        Skip.IfNot(OperatingSystem.IsWindows(), "Windows only");

        var catalog = new GameCatalog([typeof(SkyrimSELoadOrderBuilder).Assembly]);

        var game = Assert.Single(catalog.Games);

        Assert.Equal("Skyrim Special Edition", game.Name);
        Assert.Equal(489830u, game.Vendors.Steam);
        Assert.Equal("skyrimspecialedition", game.ProtocolGameIds[ProtocolSchemes.Nxm]);
        Assert.Equal("skyrimse", game.ProtocolGameIds[ProtocolSchemes.Modl]);

        var concrete = Assert.IsType<SupportedGame>(game);
        var platform = Assert.Single(concrete.Platforms);
        Assert.Equal(PlatformType.Windows, platform.Platform);
        Assert.Equal(2, platform.Executables.Count);
        Assert.Equal("SkyrimSELauncher.exe", platform.Executables[0].File.Name);

        Assert.True(game.ContainsKey("_appData"));
        var appData = game["_appData"];
        Assert.True(Path.IsPathFullyQualified(appData.DirectoryPath));
        Assert.Equal(0, game.LaunchTargets["skse64_loader.exe"]);

        var data = Assert.Single(game.InstallTargets, target => target.Key == "Data");
        Assert.Equal("_root", data.BasedOn);
        var installed = new InstalledGame(new DirectoryInfo(@"C:\Games\SkyrimSE"), game);
        Assert.Equal(
            System.IO.Path.Combine(@"C:\Games\SkyrimSE", "Data"),
            installed.LookupAbsolutePath(data)
        );
    }
}
