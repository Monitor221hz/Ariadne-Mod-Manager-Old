using Fsp;
using Xunit;

namespace Daedalus.WinFsp.Tests;

[Collection("WinFsp")]
[Trait("Category", "RequiresWinFsp")]
public class MountedFileSystemTests
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

    private static FileSystemHost Mount(
        PassthroughFileSystem fs,
        string mountPoint,
        out string canonicalMountPoint
    )
    {
        var host = new FileSystemHost(fs);
        int status = host.Mount(mountPoint, null, false, 0);
        Assert.True(status >= 0, $"Mount failed with NTSTATUS 0x{unchecked((uint)status):X8}");
        canonicalMountPoint = host.MountPoint();
        Assert.False(string.IsNullOrEmpty(canonicalMountPoint));
        return host;
    }

    [SkippableFact]
    public void Mounted_Volume_RoundTrips_Reads()
    {
        RequireWinFsp();
        using var root = new TempDirectory();
        File.WriteAllText(root.Combine("seeded.txt"), "seed");
        string mountPoint = root.Path + "-mount";

        using var host = Mount(new PassthroughFileSystem(root.Path), mountPoint, out var mounted);

        Assert.Equal("seed", File.ReadAllText(System.IO.Path.Combine(mounted, "seeded.txt")));
        Assert.Contains(System.IO.Path.Combine(mounted, "seeded.txt"), Directory.GetFiles(mounted));
    }

    [SkippableFact]
    public void Mounted_Volume_Create_Write_Delete()
    {
        RequireWinFsp();
        using var root = new TempDirectory();
        string mountPoint = root.Path + "-mount";

        using var host = Mount(new PassthroughFileSystem(root.Path), mountPoint, out var mounted);

        string seenFromMount = System.IO.Path.Combine(mounted, "created.txt");
        File.WriteAllText(seenFromMount, "through the driver");

        Assert.Equal("through the driver", File.ReadAllText(seenFromMount));
        Assert.True(File.Exists(root.Combine("created.txt")));

        File.SetAttributes(seenFromMount, FileAttributes.ReadOnly);
        Assert.True(
            File.GetAttributes(root.Combine("created.txt")).HasFlag(FileAttributes.ReadOnly)
        );
        File.SetAttributes(seenFromMount, FileAttributes.Normal);

        File.Delete(seenFromMount);
        Assert.False(File.Exists(root.Combine("created.txt")));
    }
}

[CollectionDefinition("WinFsp", DisableParallelization = true)]
public class WinFspCollection { }
