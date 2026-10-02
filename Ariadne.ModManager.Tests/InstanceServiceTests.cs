using Ariadne.Contracts.Games;
using Ariadne.Contracts.ModManager;
using Ariadne.Games;
using Ariadne.Games.Serialization;
using Xunit;

namespace Ariadne.ModManager.Tests;

public class InstanceServiceTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    private ISupportedGame Configuration { get; } =
        new SupportedGame(
            "Test Game",
            [],
            new VendorInfo(489830, 0),
            new GamePath("Root", "", []),
            [],
            []
        );

    private InstalledGame CreateGame()
    {
        var installDir = new DirectoryInfo(_temp.Combine("game-install"));
        installDir.Create();
        return new InstalledGame(installDir, Configuration);
    }

    private (InstanceService service, IGameCatalog catalog) CreateService()
    {
        var store = new InstanceStore(new FileInfo(_temp.Combine("instances.json")));
        var gameDir = new DirectoryInfo(_temp.Combine("gameconfigs"));
        gameDir.Create();
        File.WriteAllText(
            Path.Combine(gameDir.FullName, "game.json"),
            """
            {
              "Name": "Test Game",
              "Platforms": [],
              "Vendors": { "Steam": 489830, "GOG": 0 },
              "Root": { "Key": "Root", "DirectoryPath": "", "Patterns": [] },
              "Deployments": [],
              "InstallTargets": []
            }
            """
        );
        var catalog = new GameCatalog(gameDir);
        return (new InstanceService(store, new InstalledGameSerializer(), catalog), catalog);
    }

    [Fact]
    public void Create_InitializesInstanceFolders_BindsGame_AndActivatesIt()
    {
        var (sut, _) = CreateService();
        var game = CreateGame();
        var folder = new DirectoryInfo(_temp.Combine("Default"));

        sut.Create("Default", folder, game);

        Assert.True(Directory.Exists(Path.Combine(folder.FullName, "Mods")));
        Assert.True(Directory.Exists(Path.Combine(folder.FullName, "Profiles")));
        Assert.True(Directory.Exists(Path.Combine(folder.FullName, "Staging")));
        Assert.Equal("Default", sut.Current?.Name);
        Assert.Equal(folder.FullName, sut.Current!.Folder.FullName);
        Assert.True(File.Exists(Path.Combine(folder.FullName, "steam_489830.json")));
    }

    [Fact]
    public void Create_EmptyName_Throws()
    {
        var (sut, _) = CreateService();

        Assert.Throws<ArgumentException>(() =>
            sut.Create("  ", new DirectoryInfo(_temp.Combine("x")), CreateGame())
        );
    }

    [Fact]
    public void Switch_ChangesCurrent_AndRejectsUnknownInstance()
    {
        var (sut, _) = CreateService();
        sut.Create("A", new DirectoryInfo(_temp.Combine("A")), CreateGame());
        sut.Create("B", new DirectoryInfo(_temp.Combine("B")), CreateGame());

        Assert.Equal("B", sut.Current?.Name);
        sut.Switch("A");
        Assert.Equal("A", sut.Current?.Name);
        Assert.Throws<ArgumentException>(() => sut.Switch("C"));
    }

    [Fact]
    public void ResolveGame_RoundTripsInstallPath_AndCatalogConfiguration()
    {
        var (sut, _) = CreateService();
        var game = CreateGame();
        sut.Create("Default", new DirectoryInfo(_temp.Combine("Default")), game);

        var resolved = sut.ResolveGame("Default");

        Assert.NotNull(resolved);
        Assert.Equal(game.InstallPath.FullName, resolved!.InstallPath.FullName);
        Assert.Equal("Test Game", resolved.Configuration.Name);
    }

    [Fact]
    public void TwoInstances_OfSameGame_StayPathDistinct()
    {
        var (sut, _) = CreateService();
        var firstGame = CreateGame();
        var secondInstall = new DirectoryInfo(_temp.Combine("game-install-2"));
        secondInstall.Create();
        var secondGame = new InstalledGame(secondInstall, Configuration);

        sut.Create("A", new DirectoryInfo(_temp.Combine("A")), firstGame);
        sut.Create("B", new DirectoryInfo(_temp.Combine("B")), secondGame);

        Assert.Equal(firstGame.InstallPath.FullName, sut.ResolveGame("A")!.InstallPath.FullName);
        Assert.Equal(secondGame.InstallPath.FullName, sut.ResolveGame("B")!.InstallPath.FullName);
    }

    [Fact]
    public void Remove_RegistryOnly_KeepsFolderOnDisk()
    {
        var (sut, _) = CreateService();
        var folder = sut.Create(
            "Default",
            new DirectoryInfo(_temp.Combine("Default")),
            CreateGame()
        );

        sut.Remove("Default", deleteFolder: false);

        Assert.True(Directory.Exists(folder.FullName));
        Assert.Empty(sut.Instances);
        Assert.Null(sut.Current);
    }

    [Fact]
    public void Remove_WithDelete_ScrubsFolderFromDisk()
    {
        var (sut, _) = CreateService();
        var folder = sut.Create(
            "Default",
            new DirectoryInfo(_temp.Combine("Default")),
            CreateGame()
        );

        sut.Remove("Default", deleteFolder: true);

        Assert.False(Directory.Exists(folder.FullName));
        Assert.Empty(sut.Instances);
    }

    [Fact]
    public void SuggestInstanceName_SuffixesUntilUnique()
    {
        var (sut, _) = CreateService();
        sut.Create("Default", new DirectoryInfo(_temp.Combine("A")), CreateGame());
        sut.Create("Default 2", new DirectoryInfo(_temp.Combine("B")), CreateGame());

        var service = (IInstanceService)sut;

        Assert.Equal("Default 3", service.SuggestInstanceName());
        Assert.Equal("Other", service.SuggestInstanceName("Other"));
    }

    [Fact]
    public void StaleLastActive_YieldsNoCurrent()
    {
        var store = new InstanceStore(new FileInfo(_temp.Combine("instances.json")));
        store.Add("A", new DirectoryInfo(_temp.Combine("A")));
        store.SetLastActive("Ghost");

        var service = new InstanceService(
            store,
            new InstalledGameSerializer(),
            new GameCatalog([])
        );

        Assert.Null(service.Current);
    }
}
