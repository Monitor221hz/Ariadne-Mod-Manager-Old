using GameFinder.StoreHandlers.Steam;
using GameFinder.StoreHandlers.Steam.Models;
using GameFinder.StoreHandlers.Steam.Models.ValueTypes;
using GameFinder.StoreHandlers.Steam.Services;
using NexusMods.Paths;
using Xunit;

namespace Daedalus.Games.Tests;

public class GameLocatorTests
{
    private static InMemoryFileSystem CreateSteamFileSystem(
        uint appId,
        string folderName,
        out string installDirectory
    )
    {
        var fs = new InMemoryFileSystem();

        var steamPath = SteamLocationFinder.GetDefaultSteamInstallationPaths(fs).First();
        fs.AddDirectory(steamPath);

        var libraryFoldersFilePath = SteamLocationFinder.GetLibraryFoldersFilePath(steamPath);
        fs.AddEmptyFile(libraryFoldersFilePath);

        var libraryPath = fs.GetKnownPath(KnownPath.TempDirectory).Combine("SteamLibrary");
        fs.AddDirectory(libraryPath);

        var libraryManifest = new LibraryFoldersManifest
        {
            ManifestPath = libraryFoldersFilePath,
            LibraryFolders =
            [
                new LibraryFolder
                {
                    Path = libraryPath,
                    Label = "",
                    TotalDiskSize = Size.Zero,
                    AppSizes = new Dictionary<AppId, Size> { [AppId.From(appId)] = Size.From(1ul) },
                },
            ],
        };
        Assert.True(
            LibraryFoldersManifestWriter.Write(libraryManifest, libraryFoldersFilePath).IsSuccess
        );

        var acfPath = libraryPath.Combine("steamapps").Combine($"appmanifest_{appId}.acf");
        var appManifest = new AppManifest
        {
            ManifestPath = acfPath,
            AppId = AppId.From(appId),
            Name = folderName,
            StateFlags = StateFlags.FullyInstalled,
            InstallationDirectory = libraryPath
                .Combine("steamapps")
                .Combine("common")
                .Combine(folderName),
        };
        Assert.True(AppManifestWriter.Write(appManifest, acfPath).IsSuccess);

        installDirectory = new DirectoryInfo(
            appManifest.InstallationDirectory.GetFullPath()
        ).FullName;
        return fs;
    }

    private static SupportedGame CreateConfig(string name, uint steamId) =>
        new(
            name,
            [],
            new VendorInfo(steamId, 0),
            new GamePath("Root", "", []),
            [new GamePath("AppData", @"D:\appdata", [])],
            [new GamePath("Data", "Data", [], "Root")]
        );

    [Fact]
    public void FindInstalledGames_InstalledGame_ReturnsInstalledGameWithConfig()
    {
        var fs = CreateSteamFileSystem(489830, "Skyrim Special Edition", out var installDir);
        var locator = new GameLocator(new SteamHandler(fs, registry: null));
        var config = CreateConfig("Skyrim Special Edition", 489830);

        var found = locator.FindInstalledGames(config).ToList();

        var installed = Assert.Single(found);
        Assert.Equal(installDir, installed.InstallPath.FullName);
        Assert.Same(config, installed.Configuration);
    }

    [Fact]
    public void FindInstalledGames_NotInstalled_YieldsNothing()
    {
        var fs = CreateSteamFileSystem(489830, "Skyrim Special Edition", out _);
        var locator = new GameLocator(new SteamHandler(fs, registry: null));
        var config = CreateConfig("Oblivion", 22330);

        Assert.Empty(locator.FindInstalledGames(config));
    }

    [Fact]
    public void FindInstalledGames_SteamZeroId_YieldsNothing()
    {
        var fs = CreateSteamFileSystem(489830, "Skyrim Special Edition", out _);
        var locator = new GameLocator(new SteamHandler(fs, registry: null));

        Assert.Empty(locator.FindInstalledGames(CreateConfig("No Steam", 0)));
    }

    [Fact]
    public void FindInstalledGames_NoSteamInstallation_YieldsNothing()
    {
        var fs = new InMemoryFileSystem();
        var locator = new GameLocator(new SteamHandler(fs, registry: null));

        Assert.Empty(locator.FindInstalledGames(CreateConfig("Skyrim Special Edition", 489830)));
    }

    [Fact]
    public void FindInstalledGames_MultipleConfigs_ReturnsOnlyInstalled()
    {
        var fs = CreateSteamFileSystem(489830, "Skyrim Special Edition", out var installDir);
        var locator = new GameLocator(new SteamHandler(fs, registry: null));
        var installed1 = CreateConfig("Skyrim Special Edition", 489830);
        var missing = CreateConfig("Oblivion", 22330);

        var found = locator.FindInstalledGames([installed1, missing]).ToList();

        Assert.Single(found);
        Assert.Same(installed1, found[0].Configuration);
        Assert.Equal(installDir, found[0].InstallPath.FullName);
    }
}
