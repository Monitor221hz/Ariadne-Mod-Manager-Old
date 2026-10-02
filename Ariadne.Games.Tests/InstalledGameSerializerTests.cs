using Ariadne.Contracts.Games;
using Ariadne.Games.Serialization;
using Xunit;

namespace Ariadne.Games.Tests;

public class InstalledGameSerializerTests : IDisposable
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

    private static ISupportedGame CreateConfiguration(uint steam = 489830, int gog = 0) =>
        new SupportedGame(
            "Test Game",
            [],
            new VendorInfo(steam, gog),
            new GamePath("Root", "", []),
            [],
            []
        );

    private static InstalledGame CreateGame(ISupportedGame configuration) =>
        new(new DirectoryInfo(System.IO.Path.Combine("C:", "Games", "Test Game")), configuration);

    [Fact]
    public void GetFileName_UsesVendorAndId()
    {
        var sut = new InstalledGameSerializer();

        Assert.Equal("steam_489830.json", sut.GetFileName(new VendorInfo(489830, 0)));
        Assert.Equal("gog_1207664643.json", sut.GetFileName(new VendorInfo(0, 1207664643)));
    }

    [Fact]
    public void Save_WritesVendorIdJson_ContainingInstallPathOnly()
    {
        var sut = new InstalledGameSerializer();
        var game = CreateGame(CreateConfiguration());
        var folder = new DirectoryInfo(System.IO.Path.Combine(_temp.Path, "games"));

        sut.Save(game, folder);

        var file = new FileInfo(System.IO.Path.Combine(folder.FullName, "steam_489830.json"));
        Assert.True(file.Exists);

        var json = File.ReadAllText(file.FullName);
        Assert.Contains("InstallPath", json);
        Assert.DoesNotContain("Configuration", json);
    }

    [Fact]
    public void Load_RoundTripsInstallPath_AndReattachesConfiguration()
    {
        var sut = new InstalledGameSerializer();
        var configuration = CreateConfiguration();
        var game = CreateGame(configuration);
        var folder = new DirectoryInfo(System.IO.Path.Combine(_temp.Path, "games"));
        sut.Save(game, folder);

        var loaded = sut.Load(
            new FileInfo(System.IO.Path.Combine(folder.FullName, "steam_489830.json")),
            configuration
        );

        Assert.Equal(game.InstallPath.FullName, loaded.InstallPath.FullName);
        Assert.Same(configuration, loaded.Configuration);
    }
}
