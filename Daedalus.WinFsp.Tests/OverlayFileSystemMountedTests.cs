using System.Collections.Concurrent;
using Daedalus.VFS;
using Daedalus.WinFsp;
using Fsp;
using Xunit;

namespace Daedalus.WinFsp.Tests;

[Collection("WinFsp")]
[Trait("Category", "RequiresWinFsp")]
public class OverlayFileSystemMountedTests : IDisposable
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

    private string? _tmp;
    private string BaseDir => _tmp + "\\base";
    private string ModDir => _tmp + "\\mod";
    private string OverwriteDir => _tmp + "\\overwrite";

    public OverlayFileSystemMountedTests()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        _tmp = Path.Combine(
            Path.GetTempPath(),
            "DaedalusOverlayMounted-" + Guid.NewGuid().ToString("N")
        );
        Directory.CreateDirectory(BaseDir);
        Directory.CreateDirectory(ModDir);
        Directory.CreateDirectory(OverwriteDir);
        Directory.CreateDirectory(ModDir + "\\textures");
        File.WriteAllText(BaseDir + "\\readme.txt", "base-readme");
        File.WriteAllText(BaseDir + "\\config.ini", "from-base");
        File.WriteAllText(ModDir + "\\config.ini", "from-mod");
        File.WriteAllText(ModDir + "\\textures\\armor.dds", "mod-armor");
    }

    public void Dispose()
    {
        if (_tmp == null || !Directory.Exists(_tmp))
        {
            return;
        }
        foreach (
            var entry in Directory.EnumerateFileSystemEntries(
                _tmp,
                "*",
                SearchOption.AllDirectories
            )
        )
        {
            File.SetAttributes(entry, FileAttributes.Normal);
        }
        Directory.Delete(_tmp, true);
    }

    private static void RequireWinFsp()
    {
        Skip.IfNot(OperatingSystem.IsWindows(), "Windows only");
        Skip.IfNot(
            WinFspCandidates.Any(File.Exists),
            "WinFsp driver/runtime not installed (winfsp-x64.dll not found)"
        );
    }

    private VirtualNode<BackedEntry> BuildTree()
    {
        var root = new VirtualNode<BackedEntry>("", NodeFlags.Directory, null, default);
        root.LinkDirectory(BaseDir, "");
        root.LinkDirectory(ModDir, "");
        root.LinkDirectory(OverwriteDir, "", LinkFlags.Recursive | LinkFlags.CreateTarget);
        return root;
    }

    [SkippableFact]
    public void Mounted_Reads_PriorityWinner_Through_Driver()
    {
        RequireWinFsp();
        var fs = new OverlayFileSystem(BuildTree());
        string mountPoint = _tmp + "-mount";

        using var host = new FileSystemHost(fs);
        int status = host.Mount(mountPoint, null, false, 0);
        Assert.True(status >= 0, $"Mount failed: 0x{unchecked((uint)status):X8}");
        string mounted = host.MountPoint()!;

        Assert.Equal("from-mod", File.ReadAllText(mounted + "\\config.ini"));
        Assert.Equal("base-readme", File.ReadAllText(mounted + "\\readme.txt"));
        Assert.Equal("mod-armor", File.ReadAllText(mounted + "\\textures\\armor.dds"));
    }

    [SkippableFact]
    public void Mounted_Write_Lands_In_Overwrite_Mod_Untouched()
    {
        RequireWinFsp();
        var fs = new OverlayFileSystem(BuildTree());
        string mountPoint = _tmp + "-mount";

        using var host = new FileSystemHost(fs);
        int status = host.Mount(mountPoint, null, false, 0);
        Assert.True(status >= 0, $"Mount failed: 0x{unchecked((uint)status):X8}");
        string mounted = host.MountPoint()!;

        File.WriteAllText(mounted + "\\config.ini", "written-through");
        Assert.Equal("written-through", File.ReadAllText(mounted + "\\config.ini"));

        host.Unmount();

        Assert.Equal("from-mod", File.ReadAllText(ModDir + "\\config.ini"));
        Assert.Equal("written-through", File.ReadAllText(OverwriteDir + "\\config.ini"));
    }

    [SkippableFact]
    public void Mounted_Create_Delete_And_Enumerate()
    {
        RequireWinFsp();
        var fs = new OverlayFileSystem(BuildTree());
        string mountPoint = _tmp + "-mount";

        using var host = new FileSystemHost(fs);
        int status = host.Mount(mountPoint, null, false, 0);
        Assert.True(status >= 0, $"Mount failed: 0x{unchecked((uint)status):X8}");
        string mounted = host.MountPoint()!;

        var before = Directory.GetFileSystemEntries(mounted).Select(Path.GetFileName).ToList();
        Assert.Contains("readme.txt", before);
        Assert.Contains("textures", before);

        File.WriteAllText(mounted + "\\runtime.log", "new");
        Assert.Contains(
            "runtime.log",
            Directory.GetFileSystemEntries(mounted).Select(Path.GetFileName)
        );

        host.Unmount();

        Assert.True(File.Exists(OverwriteDir + "\\runtime.log"));

        File.Delete(OverwriteDir + "\\runtime.log");
    }

    [SkippableFact]
    public void Mounted_Parallel_ReadWrite_Stress()
    {
        RequireWinFsp();

        Directory.CreateDirectory(ModDir + "\\assets");
        for (int i = 0; i < 100; i++)
        {
            File.WriteAllText(ModDir + $"\\assets\\a{i:D3}.dat", $"asset-{i:D3}");
        }

        var fs = new OverlayFileSystem(BuildTree());
        string mountPoint = _tmp + "-mount";

        using var host = new FileSystemHost(fs);
        int mountStatus = host.Mount(mountPoint, null, false, 0);
        Assert.True(mountStatus >= 0, $"Mount failed: 0x{unchecked((uint)mountStatus):X8}");
        string mounted = host.MountPoint()!;

        Directory.CreateDirectory(mounted + "\\created");

        var errors = new ConcurrentQueue<Exception>();
        var tasks = new List<Task>();

        for (int t = 0; t < 6; t++)
        {
            int worker = t;
            tasks.Add(
                Task.Run(() =>
                {
                    var rnd = new Random(worker);
                    for (int i = 0; i < 200; i++)
                    {
                        int n = rnd.Next(100);
                        try
                        {
                            string text;
                            using (
                                var s = new FileStream(
                                    mounted + $"\\assets\\a{n:D3}.dat",
                                    FileMode.Open,
                                    FileAccess.Read,
                                    FileShare.ReadWrite
                                )
                            )
                            using (var r = new StreamReader(s))
                            {
                                text = r.ReadToEnd();
                            }
                            if (n < 20)
                            {
                                Assert.True(
                                    text == $"asset-{n:D3}" || text == $"edited-{n:D3}",
                                    $"a{n:D3} content mismatch: {text}"
                                );
                            }
                            else
                            {
                                Assert.Equal($"asset-{n:D3}", text);
                            }
                        }
                        catch (Exception ex)
                        {
                            errors.Enqueue(ex);
                        }
                    }
                })
            );
        }

        for (int t = 0; t < 4; t++)
        {
            int worker = t;
            tasks.Add(
                Task.Run(() =>
                {
                    for (int i = 0; i < 5; i++)
                    {
                        int n = worker * 5 + i;
                        try
                        {
                            using (
                                var s = new FileStream(
                                    mounted + $"\\assets\\a{n:D3}.dat",
                                    FileMode.Create,
                                    FileAccess.Write,
                                    FileShare.ReadWrite
                                )
                            )
                            {
                                s.Write(System.Text.Encoding.UTF8.GetBytes($"edited-{n:D3}"));
                            }
                        }
                        catch (Exception ex)
                        {
                            errors.Enqueue(ex);
                        }
                    }
                })
            );
        }

        for (int t = 0; t < 2; t++)
        {
            int worker = t;
            tasks.Add(
                Task.Run(() =>
                {
                    for (int i = 0; i < 25; i++)
                    {
                        try
                        {
                            File.WriteAllText(
                                mounted + $"\\created\\new-{worker}-{i}.log",
                                $"create-{worker}-{i}"
                            );
                        }
                        catch (Exception ex)
                        {
                            errors.Enqueue(ex);
                        }
                    }
                })
            );
        }

        tasks.Add(
            Task.Run(() =>
            {
                for (int i = 0; i < 100; i++)
                {
                    try
                    {
                        File.ReadAllText(mounted + "\\assets\\a050.dat");
                    }
                    catch (Exception ex)
                    {
                        errors.Enqueue(ex);
                    }
                }
            })
        );

        var all = Task.WhenAll(tasks);
        Assert.True(all.Wait(TimeSpan.FromMinutes(2)), "stress test timed out");
        Assert.Empty(errors);

        host.Unmount();

        for (int n = 0; n < 20; n++)
        {
            Assert.Equal($"asset-{n:D3}", File.ReadAllText(ModDir + $"\\assets\\a{n:D3}.dat"));
            Assert.Equal(
                $"edited-{n:D3}",
                File.ReadAllText(OverwriteDir + $"\\assets\\a{n:D3}.dat")
            );
        }
        Assert.Equal(50, Directory.GetFiles(OverwriteDir + "\\created").Length);
    }

    [SkippableFact]
    public void Mounted_Delete_Produces_Whiteout_And_Entry_Vanishes()
    {
        RequireWinFsp();
        var fs = new OverlayFileSystem(BuildTree());
        string mountPoint = _tmp + "-mount";

        using var host = new FileSystemHost(fs);
        int status = host.Mount(mountPoint, null, false, 0);
        Assert.True(status >= 0, $"Mount failed: 0x{unchecked((uint)status):X8}");
        string mounted = host.MountPoint()!;

        File.Delete(mounted + "\\readme.txt");

        Assert.DoesNotContain(
            "readme.txt",
            Directory.GetFileSystemEntries(mounted).Select(Path.GetFileName)
        );

        host.Unmount();

        Assert.True(File.Exists(OverwriteDir + "\\readme.txt.daehidden"));
        Assert.True(File.Exists(BaseDir + "\\readme.txt"));
    }
}
