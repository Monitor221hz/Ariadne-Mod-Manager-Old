using Xunit;

namespace Ariadne.VFS.WinFsp.Tests;

public class OutputRuleRouterTests : IDisposable
{
    private readonly string _tmp;
    private readonly WMProcessObserver _tracker;

    public OutputRuleRouterTests()
    {
        Skip.IfNot(OperatingSystem.IsWindows(), "Windows only");

        _tmp = Path.Combine(
            Path.GetTempPath(),
            "AriadneRouterTests-" + Guid.NewGuid().ToString("N")
        );
        Directory.CreateDirectory(_tmp);
        _tracker = new WMProcessObserver(trackingAvailable: true);
    }

    public void Dispose()
    {
        _tracker.Dispose();
        Directory.Delete(_tmp, true);
    }

    private (string Mount, VirtualNode<BackedEntry> Root) Environment()
    {
        string mount = Path.Combine(_tmp, "mount");
        string mods = Path.Combine(_tmp, "mods");
        Directory.CreateDirectory(Path.Combine(mods, "tools"));
        File.WriteAllText(Path.Combine(mods, "tools", "test.exe"), "fake binary");

        var root = new VirtualNode<BackedEntry>("", NodeFlags.Directory, null, default);
        root.LinkDirectory(mods, "");
        return (mount, root);
    }

    [SkippableFact]
    public void Unknown_Process_Resolves_To_Nothing()
    {
        var (mount, root) = Environment();
        using var router = new OutputRuleRouter(
            new[]
            {
                new OutputRule(
                    Path.Combine(_tmp, "mods", "tools", "test.exe"),
                    Path.Combine(_tmp, "out")
                ),
            },
            _tracker,
            root,
            mount
        );
        Assert.False(router.TryResolve(4_777_001, out _));
    }

    [SkippableFact]
    public void Direct_Image_Match_Resolves()
    {
        var (mount, root) = Environment();
        string toolPath = Path.Combine(_tmp, "mods", "tools", "test.exe");
        using var router = new OutputRuleRouter(
            new[] { new OutputRule(toolPath, Path.Combine(_tmp, "out")) },
            _tracker,
            root,
            mount
        );
        _tracker.Register(4_777_002, toolPath, 0);
        Assert.True(router.TryResolve(4_777_002, out string? hit));
        Assert.Equal(Path.Combine(_tmp, "out"), hit);
    }

    [SkippableFact]
    public void Image_Match_Is_Case_Insensitive()
    {
        var (mount, root) = Environment();
        string toolPath = Path.Combine(_tmp, "mods", "tools", "test.exe");
        using var router = new OutputRuleRouter(
            new[] { new OutputRule(toolPath, Path.Combine(_tmp, "out")) },
            _tracker,
            root,
            mount
        );
        _tracker.Register(4_777_003, toolPath.ToUpperInvariant(), 0);
        Assert.True(router.TryResolve(4_777_003, out _));
    }

    [SkippableFact]
    public void Virtual_Launch_Translates_To_Physical_Before_Matching()
    {
        var (mount, root) = Environment();
        string physicalTool = Path.Combine(_tmp, "mods", "tools", "test.exe");
        using var router = new OutputRuleRouter(
            new[] { new OutputRule(physicalTool, Path.Combine(_tmp, "out")) },
            _tracker,
            root,
            mount
        );
        _tracker.Register(4_777_004, Path.Combine(mount, "tools", "test.exe"), 0);
        Assert.True(router.TryResolve(4_777_004, out string? hit));
        Assert.Equal(Path.Combine(_tmp, "out"), hit);
    }

    [SkippableFact]
    public void Child_Inherits_Parents_Rule()
    {
        var (mount, root) = Environment();
        string toolPath = Path.Combine(_tmp, "mods", "tools", "test.exe");
        using var router = new OutputRuleRouter(
            new[] { new OutputRule(toolPath, Path.Combine(_tmp, "out")) },
            _tracker,
            root,
            mount
        );
        _tracker.Register(4_777_010, toolPath, 0);
        _tracker.Register(4_777_011, Path.Combine(_tmp, "unrelated", "helper.exe"), 4_777_010);
        Assert.True(router.TryResolve(4_777_011, out string? hit));
        Assert.Equal(Path.Combine(_tmp, "out"), hit);
    }

    [SkippableFact]
    public void Stopped_Process_Loses_Its_Hit()
    {
        var (mount, root) = Environment();
        string toolPath = Path.Combine(_tmp, "mods", "tools", "test.exe");
        using var router = new OutputRuleRouter(
            new[] { new OutputRule(toolPath, Path.Combine(_tmp, "out")) },
            _tracker,
            root,
            mount
        );
        _tracker.Register(4_777_012, toolPath, 0);
        Assert.True(router.TryResolve(4_777_012, out _));

        _tracker.Unregister(4_777_012);
        Assert.False(router.TryResolve(4_777_012, out _));
    }

    [SkippableFact]
    public void Unrelated_Process_Stays_Unmapped()
    {
        var (mount, root) = Environment();
        using var router = new OutputRuleRouter(
            new[]
            {
                new OutputRule(
                    Path.Combine(_tmp, "mods", "tools", "test.exe"),
                    Path.Combine(_tmp, "out")
                ),
            },
            _tracker,
            root,
            mount
        );
        _tracker.Register(4_777_013, Path.Combine(_tmp, "unrelated", "something.exe"), 0);
        Assert.False(router.TryResolve(4_777_013, out _));
    }

    [SkippableFact]
    public void Dead_Tracking_Fails_Fast_Instead_Of_Silently_Ignoring_Rules()
    {
        var (mount, root) = Environment();
        Assert.Throws<InvalidOperationException>(() =>
            new OutputRuleRouter(
                new[]
                {
                    new OutputRule(
                        Path.Combine(_tmp, "mods", "tools", "test.exe"),
                        Path.Combine(_tmp, "out")
                    ),
                },
                new WMProcessObserver(trackingAvailable: false),
                root,
                mount
            )
        );
    }
}
