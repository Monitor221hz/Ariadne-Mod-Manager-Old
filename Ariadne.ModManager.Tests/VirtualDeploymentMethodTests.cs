using Ariadne.Contracts.ModManager;
using Ariadne.Games;
using Ariadne.VFS;
using Xunit;

namespace Ariadne.ModManager.Tests;

public class VirtualDeploymentMethodTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly DirectoryInfo _installDir;
    private readonly DirectoryInfo _appDataDir;
    private readonly DirectoryInfo _overwriteDir;
    private readonly DirectoryInfo _stagingDir;
    private readonly SupportedGame _config;
    private readonly FakeVirtualFileSystemFactory _factory = new();

    public VirtualDeploymentMethodTests()
    {
        _installDir = Directory.CreateDirectory(_temp.Combine("game"));
        _appDataDir = Directory.CreateDirectory(_temp.Combine("appdata"));
        _overwriteDir = Directory.CreateDirectory(_temp.Combine("overwrite"));
        _stagingDir = Directory.CreateDirectory(_temp.Combine("staging"));

        var root = new GamePath("Root", "", [], []);
        var appData = new GamePath("AppData", _appDataDir.FullName, [], []);
        var data = new GamePath("Data", "Data", [], [], basedOn: "Root");
        _config = new SupportedGame("Test Game", [], new VendorInfo(0, 0), root, [appData], [data]);
    }

    public void Dispose() => _temp.Dispose();

    private InstalledGame CreateGame() => new(_installDir, _config);

    private VirtualDeploymentMethod CreateMethod() =>
        new(_factory, new TestDeploymentPaths(_overwriteDir.FullName, _stagingDir.FullName));

    private LibraryMod CreateMod(string name, string target, out DirectoryInfo modDir)
    {
        modDir = Directory.CreateDirectory(_temp.Combine("mods", name));
        return new LibraryMod(new ModInfo(1, SourceType.Local, "1.0", [], target), modDir, []);
    }

    [Fact]
    public void Deploy_MountsRootIntoStagingAndDeploymentsInPlace()
    {
        var game = CreateGame();

        using var method = CreateMethod();
        method.Deploy(game, []);

        Assert.Equal(2, _factory.Created.Count);
        Assert.All(_factory.Created, vfs => Assert.NotNull(vfs.MountedRoot));

        var rootMount = Path.Combine(_stagingDir.FullName, "Root");
        Assert.False(Directory.Exists(rootMount));
        Assert.Equal(rootMount, _factory.Created[0].Settings!.MountPoint.FullName);
        Assert.Null(_factory.Created[0].Settings!.InPlaceTarget);

        Assert.False(Directory.Exists(Path.Combine(_stagingDir.FullName, "AppData")));
        Assert.Equal(
            Path.Combine(_stagingDir.FullName, "AppData"),
            _factory.Created[1].Settings!.MountPoint.FullName
        );
        Assert.Equal(_appDataDir.FullName, _factory.Created[1].Settings!.InPlaceTarget!.FullName);
    }

    [Fact]
    public void Deploy_WithInPlaceCapability_MountsRootInPlace()
    {
        var game = CreateGame();
        _factory.Capabilities = VirtualFileSystemCapabilities.InPlaceMount;
        var mod = CreateMod("ModC", "Data", out var modDir);
        File.WriteAllText(Path.Combine(modDir.FullName, "meshes.txt"), "meshes");

        using var method = CreateMethod();
        method.Deploy(game, [mod]);

        var rootVfs = _factory.Created[0];
        Assert.Equal(_installDir.FullName, rootVfs.Settings!.InPlaceTarget!.FullName);
        Assert.Equal(
            Path.Combine(modDir.FullName, "meshes.txt"),
            rootVfs.MountedRoot!.FindNode(Path.Join("Data", "meshes.txt"))!.Data.PhysicalPath
        );
    }

    [Fact]
    public void TryGetDeployedPath_ReturnsMountDirectoryForDeployedPaths()
    {
        var game = CreateGame();

        using var method = CreateMethod();
        method.Deploy(game, []);

        var foundRoot = method.TryGetDeployedPath(game, _config.Root, out var rootDir);
        var foundAppData = method.TryGetDeployedPath(game, _config["AppData"], out var appDataDir);

        Assert.True(foundRoot);
        Assert.True(foundAppData);
        Assert.Equal(Path.Combine(_stagingDir.FullName, "Root"), rootDir!.FullName);
        Assert.Equal(_appDataDir.FullName, appDataDir!.FullName);
    }

    [Fact]
    public void Deploy_OverwriteContent_WinsOverModsAndGame()
    {
        var game = CreateGame();
        File.WriteAllText(Path.Combine(_installDir.FullName, "game.txt"), "game");
        var mod = CreateMod("ModA", "Root", out var modDir);
        File.WriteAllText(Path.Combine(modDir.FullName, "game.txt"), "mod");
        Directory.CreateDirectory(Path.Combine(_overwriteDir.FullName, "Root"));
        File.WriteAllText(Path.Combine(_overwriteDir.FullName, "Root", "game.txt"), "overwrite");

        using var method = CreateMethod();
        method.Deploy(game, [mod]);

        var node = _factory.Created[0].MountedRoot!.FindNode("game.txt");
        Assert.Equal(
            Path.Combine(_overwriteDir.FullName, "Root", "game.txt"),
            node!.Data.PhysicalPath
        );
    }

    [Fact]
    public void Deploy_CreatesMissingOverwriteDirectory()
    {
        var game = CreateGame();
        _overwriteDir.Delete(true);

        using var method = CreateMethod();
        var exception = Record.Exception(() => method.Deploy(game, []));

        Assert.Null(exception);
        Assert.True(Directory.Exists(Path.Combine(_overwriteDir.FullName, "Root")));
        Assert.True(Directory.Exists(Path.Combine(_overwriteDir.FullName, "AppData")));
    }

    [Fact]
    public void TryGetDeployedPath_BeforeDeploy_ReturnsFalse()
    {
        var game = CreateGame();

        using var method = CreateMethod();

        Assert.False(method.TryGetDeployedPath(game, _config.Root, out var dir));
        Assert.Null(dir);
    }

    [Fact]
    public void Deploy_LinksModFilesIntoDeploymentVirtualRoot()
    {
        var game = CreateGame();
        var mod = CreateMod("ModA", "AppData", out var modDir);
        var modFile = Path.Combine(modDir.FullName, "plugins.txt");
        File.WriteAllText(modFile, "Test.esp");

        using var method = CreateMethod();
        method.Deploy(game, [mod]);

        var appDataVfs = _factory.Created[1];
        Assert.NotNull(appDataVfs.MountedRoot);
        var node = appDataVfs.MountedRoot!.FindNode("plugins.txt");
        Assert.NotNull(node);
        Assert.Equal(modFile, node.Data.PhysicalPath);
    }

    [Fact]
    public void Deploy_LinksModFilesThroughBasedOnChainIntoRootVirtualRoot()
    {
        var game = CreateGame();
        var mod = CreateMod("ModC", "Data", out var modDir);
        var modFile = Path.Combine(modDir.FullName, "meshes.txt");
        File.WriteAllText(modFile, "meshes");

        using var method = CreateMethod();
        method.Deploy(game, [mod]);

        var rootVfs = _factory.Created[0];
        var node = rootVfs.MountedRoot!.FindNode(Path.Join("Data", "meshes.txt"));
        Assert.NotNull(node);
        Assert.Equal(modFile, node.Data.PhysicalPath);
    }

    [Fact]
    public void Deploy_LinksModDirectoriesIntoDeploymentVirtualRoot()
    {
        var game = CreateGame();
        var mod = CreateMod("ModD", "Data", out var modDir);
        var subDir = Directory.CreateDirectory(Path.Combine(modDir.FullName, "meshes"));
        File.WriteAllText(Path.Combine(subDir.FullName, "a.nif"), "mesh");

        using var method = CreateMethod();
        var exception = Record.Exception(() => method.Deploy(game, [mod]));

        Assert.Null(exception);
        var rootVfs = _factory.Created[0];
        var node = rootVfs.MountedRoot!.FindNode(Path.Join("Data", "meshes"));
        Assert.NotNull(node);
        Assert.Equal(subDir.FullName, node.Data.PhysicalPath);
        var file = rootVfs.MountedRoot!.FindNode(Path.Join("Data", "meshes", "a.nif"));
        Assert.NotNull(file);
    }

    [Fact]
    public void Deploy_SkipsModsWithUnknownTarget()
    {
        var game = CreateGame();
        var mod = CreateMod("ModB", "NoSuchTarget", out var modDir);
        File.WriteAllText(Path.Combine(modDir.FullName, "orphan.txt"), "nope");

        using var method = CreateMethod();

        var exception = Record.Exception(() => method.Deploy(game, [mod]));

        Assert.Null(exception);
        Assert.DoesNotContain(
            _factory.Created,
            vfs => vfs.MountedRoot!.FindNode("orphan.txt") is not null
        );
    }

    [Fact]
    public void Deploy_SkipsModsWithMissingDirectory()
    {
        var game = CreateGame();
        var missing = new DirectoryInfo(_temp.Combine("mods", "ghost"));
        var mod = new LibraryMod(new ModInfo(2, SourceType.Local, "1.0", [], "Root"), missing, []);

        using var method = CreateMethod();

        var exception = Record.Exception(() => method.Deploy(game, [mod]));

        Assert.Null(exception);
    }

    [Fact]
    public void Revert_UnmountsAllMountedFileSystems()
    {
        var game = CreateGame();

        var method = CreateMethod();
        method.Deploy(game, []);
        _factory.Created.ForEach(vfs => Assert.True(vfs is { IsUnmounted: false }));

        method.Revert(game);

        _factory.Created.ForEach(vfs => Assert.True(vfs.IsUnmounted));
    }

    [Fact]
    public void Dispose_DisposesAllCreatedFileSystems()
    {
        var game = CreateGame();

        var method = CreateMethod();
        method.Deploy(game, []);

        method.Dispose();

        _factory.Created.ForEach(vfs => Assert.True(vfs.IsDisposed));
    }
}
