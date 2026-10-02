using Ariadne.Contracts.Games;
using Ariadne.Contracts.ModManager;
using Ariadne.Games;
using Xunit;

namespace Ariadne.ModManager.Tests;

public class QuickPatternModTargeterTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    private static readonly ISupportedGame Game = new SupportedGame(
        "Test Game",
        [],
        new VendorInfo(0, 0),
        new GamePath("_root", "", []),
        [],
        [
            new GamePath("Data", "Data", ["*.esp", "*.esm", "*.bsa"], basedOn: "_root"),
            new GamePath("Root", "", ["skse64*.dll", "skse64*loader.exe"], basedOn: "_root"),
        ]
    );

    private LibraryMod ModWith(params string[] relativePaths)
    {
        var dir = new DirectoryInfo(Path.Combine(_temp.Path, $"mod-{Guid.NewGuid():N}"));
        foreach (var relativePath in relativePaths)
        {
            var path = Path.Combine(dir.FullName, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, "x");
        }

        return new LibraryMod(new ModInfo(new ModID(0, SourceType.Local), "", [], ""), dir, []);
    }

    [Fact]
    public void FlatContent_MatchesTargetDirectly()
    {
        var mod = ModWith("plugin.esp");
        var targeter = new QuickPatternModTargeter();

        Assert.Equal("Data", targeter.GetTarget(Game, mod).Key);
        Assert.True(File.Exists(Path.Combine(mod.Directory.FullName, "plugin.esp")));
    }

    [Fact]
    public void GetTarget_DoesNotTouchDisk()
    {
        var mod = ModWith("data/plugin.esp");

        var target = new QuickPatternModTargeter().GetTarget(Game, mod);

        Assert.True(Directory.Exists(Path.Combine(mod.Directory.FullName, "data")));
    }

    [Fact]
    public void RootPattern_MatchesRootTarget()
    {
        var mod = ModWith("skse64_loader.exe");
        var targeter = new QuickPatternModTargeter();

        Assert.Equal("Root", targeter.GetTarget(Game, mod).Key);
    }

    [Fact]
    public void NoMatches_FallsBackToFirstInstallTarget()
    {
        var mod = ModWith("readme.txt");
        var targeter = new QuickPatternModTargeter();

        Assert.Equal("Data", targeter.GetTarget(Game, mod).Key);
    }
}
