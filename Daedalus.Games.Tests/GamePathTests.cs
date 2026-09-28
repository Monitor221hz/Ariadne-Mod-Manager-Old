using Daedalus.Contracts.Games;
using Xunit;

namespace Daedalus.Games.Tests;

public class GamePathTests
{
    private static readonly DirectoryInfo InstallPath = new(@"C:\Games\Test Game");

    private static SupportedGame CreateGame(
        GamePath? root = null,
        IReadOnlyList<IGamePath>? deployments = null,
        IReadOnlyList<IGamePath>? installTargets = null
    ) =>
        new(
            "Test Game",
            [],
            new VendorInfo(0, 0),
            root ?? new GamePath("Root", "", [], []),
            deployments ?? [],
            installTargets ?? []
        );

    private static InstalledGame CreateInstalledGame(SupportedGame? config = null) =>
        new(InstallPath, config ?? CreateGame());

    [SkippableFact]
    public void GetAbsolutePath_AbsoluteDirectoryPath_ReturnsDirectoryPath()
    {
        Skip.IfNot(OperatingSystem.IsWindows(), "Windows only");

        var game = CreateInstalledGame();
        var absolute = @"D:\Somewhere\Else";
        var path = new GamePath("AppData", absolute, [], []);

        Assert.Equal(absolute, path.GetAbsolutePath(game));
    }

    [Fact]
    public void GetAbsolutePath_RelativePathWithoutBase_CombinesWithInstallPath()
    {
        var game = CreateInstalledGame();
        var path = new GamePath("Data", "Data", [], []);

        Assert.Equal(Path.Combine(InstallPath.FullName, "Data"), path.GetAbsolutePath(game));
    }

    [Fact]
    public void GetAbsolutePath_EmptyRootPath_ResolvesToInstallPath()
    {
        var game = CreateInstalledGame();
        var root = new GamePath("Root", "", [], []);

        Assert.Equal(InstallPath.FullName, root.GetAbsolutePath(game));
    }

    [Fact]
    public void GetAbsolutePath_BasedOnParentKey_JoinsParentAbsolutePath()
    {
        var root = new GamePath("Root", "", [], []);
        var data = new GamePath("Data", "Data", [], [], basedOn: "Root");
        var config = CreateGame(root: root, installTargets: [data]);
        var game = CreateInstalledGame(config);

        Assert.Equal(Path.Combine(InstallPath.FullName, "Data"), data.GetAbsolutePath(game));
    }

    [Fact]
    public void GetAbsolutePath_BasedOnUnknownKey_FallsBackToDirectoryPath()
    {
        var path = new GamePath("Orphan", "SubFolder", [], [], basedOn: "Missing");
        var game = CreateInstalledGame();

        Assert.Equal("SubFolder", path.GetAbsolutePath(game));
    }

    [Fact]
    public void GetAbsolutePath_CachesResult()
    {
        var game = CreateInstalledGame();
        var path = new GamePath("Data", "Data", [], []);

        var first = path.GetAbsolutePath(game);
        var second = path.GetAbsolutePath(game);

        Assert.Same(first, second);
    }

    [SkippableFact]
    public void Constructor_ExpandsEnvironmentVariables()
    {
        Skip.IfNot(OperatingSystem.IsWindows(), "Windows only");

        const string directoryPath = "%LOCALAPPDATA%\\Skyrim Special Edition";
        var path = new GamePath("AppData", directoryPath, [], []);

        var expected = Environment.ExpandEnvironmentVariables(directoryPath);
        Assert.Equal(expected, path.DirectoryPath);

        Assert.True(Path.IsPathFullyQualified(expected));
        Assert.Equal(expected, path.GetAbsolutePath(CreateInstalledGame()));
    }

    [Fact]
    public void Equals_SameKeyDifferentInstances_ReturnsTrue()
    {
        IGamePath a = new GamePath("Data", "Data", [], []);
        IGamePath b = new GamePath("Data", @"D:\Other", [], [], basedOn: "Root");

        Assert.True(a.Equals(b));
        Assert.True(((IEqualityComparer<IGamePath>)a).Equals(a, b));

        Assert.Equal(a, b);
    }

    [Fact]
    public void Equals_DifferentKeys_ReturnsFalse()
    {
        IGamePath a = new GamePath("Root", "", [], []);
        IGamePath b = new GamePath("Data", "", [], []);

        Assert.False(a.Equals(b));
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Comparer_NullHandling()
    {
        var a = new GamePath("Data", "Data", [], []);
        var comparer = (IEqualityComparer<IGamePath>)a;

        Assert.False(comparer.Equals(null, a));
        Assert.False(comparer.Equals(a, null));
    }

    [Fact]
    public void GetHashCode_MatchesKeyHashCode()
    {
        var a = new GamePath("Data", "Data", [], []);
        var b = new GamePath("Data", @"D:\Other", [], []);

        Assert.Equal("Data".GetHashCode(), ((IEqualityComparer<IGamePath>)a).GetHashCode(a));
        Assert.Equal(
            ((IEqualityComparer<IGamePath>)a).GetHashCode(a),
            ((IEqualityComparer<IGamePath>)b).GetHashCode(b)
        );
    }
}
