using Ariadne.VFS;
using Xunit;

namespace Ariadne.VFS.WinFsp.Tests;

[Collection("WinFsp")]
[Trait("Category", "RequiresWinFsp")]
public class WinFspVirtualFileSystemTests
{
    private static readonly string[] WinFspCandidates = new[]
    {
        System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "WinFsp",
            "bin",
            "winfsp-x64.dll"
        ),
        System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            "WinFsp",
            "bin",
            "winfsp-x64.dll"
        ),
    };

    private static void RequireWinFsp()
    {
        Skip.IfNot(OperatingSystem.IsWindows(), "Windows only");
        Skip.IfNot(
            WinFspCandidates.Any(File.Exists),
            "WinFsp driver/runtime not installed (winfsp-x64.dll not found)"
        );
    }

    private static VirtualFileSystemSettings SettingsFor(DirectoryInfo mountPoint) =>
        new(true, mountPoint, []);

    private static VirtualNode<BackedEntry> CreateTargetRoot(string physicalDirectory)
    {
        var root = new VirtualNode<BackedEntry>("", NodeFlags.Directory, null, default);
        root.LinkDirectory(physicalDirectory, "", LinkFlags.CreateTarget);
        return root;
    }

    [SkippableFact]
    public void Mount_OverOccupiedMountPoint_Throws()
    {
        RequireWinFsp();
        using var root = new TempDirectory();
        var mountPoint = new DirectoryInfo(root.Path + "-mount");
        var settings = SettingsFor(mountPoint);

        using var first = new WinFspVirtualFileSystem();
        first.Mount(CreateTargetRoot(root.Path), settings);

        using var second = new WinFspVirtualFileSystem();
        Assert.Throws<IOException>(() => second.Mount(CreateTargetRoot(root.Path), settings));
    }

    [SkippableFact]
    public void Mount_ExistingEmptyDirectory_IsRemovedAndMounted()
    {
        RequireWinFsp();
        using var root = new TempDirectory();
        var mountPoint = new DirectoryInfo(Path.Combine(root.Path, "Staging", "_root"));
        mountPoint.Create();

        using var vfs = new WinFspVirtualFileSystem();
        vfs.Mount(CreateTargetRoot(root.Path), SettingsFor(mountPoint));

        File.WriteAllText(Path.Combine(mountPoint.FullName, "cow.txt"), "x");
        Assert.True(File.Exists(Path.Combine(root.Path, "cow.txt")));
    }

    [SkippableFact]
    public void Mount_NonEmptyExistingDirectory_IsReplaced()
    {
        RequireWinFsp();
        using var root = new TempDirectory();
        var mountPoint = Directory.CreateDirectory(Path.Combine(root.Path, "appdata"));
        File.WriteAllText(Path.Combine(mountPoint.FullName, "stale.txt"), "stale");

        using var vfs = new WinFspVirtualFileSystem();
        vfs.Mount(CreateTargetRoot(root.Path), SettingsFor(mountPoint));

        Assert.False(File.Exists(Path.Combine(mountPoint.FullName, "stale.txt")));
    }

    private static VirtualNode<BackedEntry> OverwriteRoot(DirectoryInfo overwrite)
    {
        var root = new VirtualNode<BackedEntry>("", NodeFlags.Directory, null, default);
        root.LinkDirectory(
            overwrite.FullName,
            "",
            LinkFlags.Recursive | LinkFlags.CreateTarget | LinkFlags.Whiteouts
        );
        return root;
    }

    [SkippableFact]
    public void InPlace_NonEmptyDirectory_JunctionsAndRestoresOnUnmount()
    {
        RequireWinFsp();
        using var root = new TempDirectory();
        var real = Directory.CreateDirectory(Path.Combine(root.Path, "appdata"));
        File.WriteAllText(Path.Combine(real.FullName, "existing.ini"), "orig");
        var overwrite = Directory.CreateDirectory(Path.Combine(root.Path, "overwrite"));
        Directory.CreateDirectory(Path.Combine(root.Path, "staging"));
        var staging = new DirectoryInfo(Path.Combine(root.Path, "staging", "_appData"));
        var settings = new VirtualFileSystemSettings(true, staging, [], real);

        var vfs = new WinFspVirtualFileSystem();
        vfs.Mount(OverwriteRoot(overwrite), settings);

        Assert.True(Junction.IsJunctionTo(real.FullName, staging.FullName));
        Assert.Equal("orig", File.ReadAllText(Path.Combine(real.FullName, "existing.ini")));
        File.WriteAllText(Path.Combine(real.FullName, "plugins.txt"), "x");
        Assert.True(File.Exists(Path.Combine(overwrite.FullName, "plugins.txt")));

        vfs.Unmount();
        vfs.Dispose();

        Assert.Null(new DirectoryInfo(real.FullName).LinkTarget);
        Assert.Equal("orig", File.ReadAllText(Path.Combine(real.FullName, "existing.ini")));
        Assert.False(File.Exists(Path.Combine(real.FullName, "plugins.txt")));
    }

    [SkippableFact]
    public void InPlace_EmptyDirectory_MountsDirectlyAtTarget()
    {
        RequireWinFsp();
        using var root = new TempDirectory();
        var real = Directory.CreateDirectory(Path.Combine(root.Path, "appdata"));
        var overwrite = Directory.CreateDirectory(Path.Combine(root.Path, "overwrite"));
        Directory.CreateDirectory(Path.Combine(root.Path, "staging"));
        var staging = new DirectoryInfo(Path.Combine(root.Path, "staging", "_appData"));

        using var vfs = new WinFspVirtualFileSystem();
        vfs.Mount(OverwriteRoot(overwrite), new VirtualFileSystemSettings(true, staging, [], real));

        Assert.False(Junction.IsJunctionTo(real.FullName, staging.FullName));
        File.WriteAllText(Path.Combine(real.FullName, "plugins.txt"), "x");
        Assert.True(File.Exists(Path.Combine(overwrite.FullName, "plugins.txt")));
    }

    [SkippableFact]
    public void InPlace_MissingDirectory_MountsDirectlyAtTarget()
    {
        RequireWinFsp();
        using var root = new TempDirectory();
        var real = new DirectoryInfo(Path.Combine(root.Path, "appdata"));
        var overwrite = Directory.CreateDirectory(Path.Combine(root.Path, "overwrite"));
        Directory.CreateDirectory(Path.Combine(root.Path, "staging"));
        var staging = new DirectoryInfo(Path.Combine(root.Path, "staging", "_appData"));

        using var vfs = new WinFspVirtualFileSystem();
        vfs.Mount(OverwriteRoot(overwrite), new VirtualFileSystemSettings(true, staging, [], real));

        Assert.False(Junction.IsJunctionTo(real.FullName, staging.FullName));
        File.WriteAllText(Path.Combine(real.FullName, "plugins.txt"), "x");
        Assert.True(File.Exists(Path.Combine(overwrite.FullName, "plugins.txt")));
    }

    [SkippableFact]
    public void InPlace_CrashedSession_RecoversBeforeMounting()
    {
        RequireWinFsp();
        using var root = new TempDirectory();
        var real = Directory.CreateDirectory(Path.Combine(root.Path, "appdata"));
        File.WriteAllText(Path.Combine(real.FullName, "existing.ini"), "orig");
        var overwrite = Directory.CreateDirectory(Path.Combine(root.Path, "overwrite"));
        Directory.CreateDirectory(Path.Combine(root.Path, "staging"));
        var staging = new DirectoryInfo(Path.Combine(root.Path, "staging", "_appData"));
        var backing = Path.Combine(root.Path, ".appdata.ariadne-backing");
        Directory.Move(real.FullName, backing);
        Junction.Create(real.FullName, staging.FullName);

        var vfs = new WinFspVirtualFileSystem();
        vfs.Mount(OverwriteRoot(overwrite), new VirtualFileSystemSettings(true, staging, [], real));

        Assert.Equal("orig", File.ReadAllText(Path.Combine(real.FullName, "existing.ini")));
        vfs.Unmount();
        vfs.Dispose();

        Assert.Null(new DirectoryInfo(real.FullName).LinkTarget);
        Assert.Equal("orig", File.ReadAllText(Path.Combine(real.FullName, "existing.ini")));
        Assert.False(Directory.Exists(backing));
    }

    [SkippableFact]
    public void SwapAsideTopology_NewFileGoesToOverwrite_NotBacking()
    {
        RequireWinFsp();
        using var root = new TempDirectory();
        var real = Directory.CreateDirectory(Path.Combine(root.Path, "appdata"));
        File.WriteAllText(Path.Combine(real.FullName, "existing.ini"), "orig");
        var overwrite = Directory.CreateDirectory(Path.Combine(root.Path, "overwrite"));
        var backing = Path.Combine(root.Path, ".appdata.ariadne-backing");
        Directory.Move(real.FullName, backing);

        var virtualRoot = new VirtualNode<BackedEntry>("", NodeFlags.Directory, null, default);
        virtualRoot.LinkDirectory(
            overwrite.FullName,
            "",
            LinkFlags.Recursive | LinkFlags.CreateTarget | LinkFlags.Whiteouts
        );
        virtualRoot.LinkDirectory(backing, "");

        using (var vfs = new WinFspVirtualFileSystem())
        {
            vfs.Mount(virtualRoot, SettingsFor(real));

            var mountedPlugins = Path.Combine(real.FullName, "plugins.txt");
            File.WriteAllText(mountedPlugins, "*Test.esp");

            Assert.True(File.Exists(Path.Combine(overwrite.FullName, "plugins.txt")));
            Assert.False(File.Exists(Path.Combine(backing, "plugins.txt")));
            Assert.Equal("orig", File.ReadAllText(Path.Combine(real.FullName, "existing.ini")));
        }

        Assert.False(Directory.Exists(real.FullName));
        Directory.Move(backing, real.FullName);
        Assert.True(File.Exists(Path.Combine(real.FullName, "existing.ini")));
        Assert.False(File.Exists(Path.Combine(real.FullName, "plugins.txt")));
    }

    [SkippableFact]
    public void RootMountTopology_ModContentVisibleThroughMount()
    {
        RequireWinFsp();
        using var root = new TempDirectory();
        var gameDir = Directory.CreateDirectory(Path.Combine(root.Path, "game"));
        Directory.CreateDirectory(Path.Combine(gameDir.FullName, "Data"));
        File.WriteAllText(Path.Combine(gameDir.FullName, "game.exe"), "exe");
        var modDir = Directory.CreateDirectory(Path.Combine(root.Path, "mod"));
        Directory.CreateDirectory(Path.Combine(modDir.FullName, "meshes"));
        File.WriteAllText(Path.Combine(modDir.FullName, "meshes", "a.nif"), "mesh");
        var overwrite = Directory.CreateDirectory(Path.Combine(root.Path, "overwrite"));
        Directory.CreateDirectory(Path.Combine(root.Path, "Staging"));
        var mountPoint = new DirectoryInfo(Path.Combine(root.Path, "Staging", "_root"));

        var virtualRoot = new VirtualNode<BackedEntry>("", NodeFlags.Directory, null, default);
        virtualRoot.LinkDirectory(
            overwrite.FullName,
            "",
            LinkFlags.Recursive | LinkFlags.CreateTarget | LinkFlags.Whiteouts
        );
        virtualRoot.LinkDirectory(gameDir.FullName, "");
        virtualRoot.LinkDirectory(
            Path.Combine(modDir.FullName, "meshes"),
            Path.Combine("Data", "meshes"),
            LinkFlags.Recursive | LinkFlags.Whiteouts
        );

        using var vfs = new WinFspVirtualFileSystem();
        vfs.Mount(virtualRoot, SettingsFor(mountPoint));

        Assert.Equal(
            "mesh",
            File.ReadAllText(Path.Combine(mountPoint.FullName, "Data", "meshes", "a.nif"))
        );
        Assert.Equal("exe", File.ReadAllText(Path.Combine(mountPoint.FullName, "game.exe")));
        File.WriteAllText(Path.Combine(mountPoint.FullName, "Data", "new.txt"), "cow");
        Assert.True(File.Exists(Path.Combine(overwrite.FullName, "Data", "new.txt")));
        Assert.False(File.Exists(Path.Combine(gameDir.FullName, "Data", "new.txt")));
    }

    [SkippableFact]
    public void Dispose_UnmountsSoMountPointCanBeReused()
    {
        RequireWinFsp();
        using var root = new TempDirectory();
        var mountPoint = new DirectoryInfo(root.Path + "-mount");
        var settings = SettingsFor(mountPoint);

        var first = new WinFspVirtualFileSystem();
        first.Mount(CreateTargetRoot(root.Path), settings);
        first.Dispose();

        using var second = new WinFspVirtualFileSystem();
        second.Mount(CreateTargetRoot(root.Path), settings);
    }
}
