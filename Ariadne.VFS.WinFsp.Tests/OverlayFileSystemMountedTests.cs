using System.Collections.Concurrent;
using System.Text;
using Fsp;
using Xunit;

namespace Ariadne.VFS.WinFsp.Tests;

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

    private static string ReadShared(string path)
    {
        using var s = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete
        );
        using var r = new StreamReader(s);
        return r.ReadToEnd();
    }

    public OverlayFileSystemMountedTests()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        _tmp = Path.Combine(
            Path.GetTempPath(),
            "AriadneOverlayMounted-" + Guid.NewGuid().ToString("N")
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
        root.LinkDirectory(
            OverwriteDir,
            "",
            LinkFlags.Recursive | LinkFlags.CreateTarget | LinkFlags.Whiteouts
        );
        return root;
    }

    [SkippableFact]
    public void Mounted_OutputRule_Routes_Writes_To_Rule_Target()
    {
        RequireWinFsp();

        string ruleTarget = _tmp + "\\ruletarget";
        Directory.CreateDirectory(ruleTarget);
        string selfPath = System.Diagnostics.Process.GetCurrentProcess().MainModule!.FileName!;
        var rules = new[] { new OutputRule(selfPath, ruleTarget) };
        string mountPoint = _tmp + "-mount";

        var fs = new OverlayFileSystem(
            BuildTree(),
            new OverlayFileSystemOptions
            {
                OutputRules = rules,
                ProcessTracker = new WMProcessObserver(),
                PhysicalMountRoot = mountPoint,
            }
        );
        using var host = new FileSystemHost(fs);
        int status = host.Mount(mountPoint, null, false, 0);
        Assert.True(status >= 0, $"Mount failed: 0x{unchecked((uint)status):X8}");
        string mounted = host.MountPoint()!;

        File.WriteAllText(mounted + "\\rule-created.txt", "wrote-through-rule");
        File.WriteAllText(mounted + "\\config.ini", "edited-through-rule");

        host.Unmount();

        Assert.True(File.Exists(ruleTarget + "\\rule-created.txt"));
        Assert.False(File.Exists(OverwriteDir + "\\rule-created.txt"));
        Assert.True(File.Exists(ruleTarget + "\\config.ini"));
        Assert.False(File.Exists(OverwriteDir + "\\config.ini"));
        Assert.Equal("from-mod", File.ReadAllText(ModDir + "\\config.ini"));
    }

    [SkippableFact]
    public void Mounted_Concurrent_CopyUps_Under_Write_Pressure()
    {
        RequireWinFsp();

        Directory.CreateDirectory(ModDir + "\\stress");
        for (int i = 0; i < 10; i++)
        {
            File.WriteAllText(ModDir + $"\\stress\\s{i:D3}.dat", $"stock-{i:D3}");
        }

        string mountPoint = _tmp + "-mount";
        var fs = new OverlayFileSystem(BuildTree());
        using var host = new FileSystemHost(fs);
        int status = host.Mount(mountPoint, null, false, 0);
        Assert.True(status >= 0, $"Mount failed: 0x{unchecked((uint)status):X8}");
        string mounted = host.MountPoint()!;

        var errors = new ConcurrentQueue<Exception>();
        var tasks = new List<Task>();
        for (int t = 0; t < 4; t++)
        {
            int worker = t;
            tasks.Add(
                Task.Run(() =>
                {
                    for (int round = 0; round < 25; round++)
                    {
                        int fileIndex = (worker + round) % 10;
                        string name = $"s{fileIndex:D3}.dat";
                        try
                        {
                            var bytesToWrite = Encoding.UTF8.GetBytes($"edited-{worker}-{round}");
                            using (
                                var stream = new FileStream(
                                    mounted + "\\stress\\" + name,
                                    FileMode.Create,
                                    FileAccess.Write,
                                    FileShare.ReadWrite
                                )
                            )
                            {
                                stream.Write(bytesToWrite, 0, bytesToWrite.Length);
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

        Assert.True(Task.WaitAll(tasks.ToArray(), TimeSpan.FromMinutes(2)));
        Assert.Empty(errors);

        host.Unmount();

        for (int i = 0; i < 10; i++)
        {
            string modFile = ModDir + $"\\stress\\s{i:D3}.dat";
            string sinkFile = OverwriteDir + $"\\stress\\s{i:D3}.dat";
            Assert.Equal($"stock-{i:D3}", ReadShared(modFile));
            Assert.Matches(@"^edited-\d+-\d+$", ReadShared(sinkFile));
        }
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
    public void Mounted_LargeFile_Roundtrips_Without_Corruption()
    {
        RequireWinFsp();
        var fs = new OverlayFileSystem(BuildTree());
        string mountPoint = _tmp + "-mount";

        using var host = new FileSystemHost(fs);
        int status = host.Mount(mountPoint, null, false, 0);
        Assert.True(status >= 0, $"Mount failed: 0x{unchecked((uint)status):X8}");
        string mounted = host.MountPoint()!;

        var payload = new byte[20 * 1024 * 1024];
        new Random(42).NextBytes(payload);
        string path = mounted + "\\blob.bin";
        File.WriteAllBytes(path, payload);

        byte[] back = File.ReadAllBytes(path);
        Assert.Equal(payload, back);
        Assert.Equal(
            System.Security.Cryptography.SHA256.HashData(payload),
            System.Security.Cryptography.SHA256.HashData(back)
        );

        host.Unmount();

        Assert.Equal(
            System.Security.Cryptography.SHA256.HashData(payload),
            System.Security.Cryptography.SHA256.HashData(
                File.ReadAllBytes(OverwriteDir + "\\blob.bin")
            )
        );
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

    [SkippableFact]
    public void Deleted_Files_Stay_Hidden_Across_Mount_Cycles()
    {
        RequireWinFsp();

        {
            var fs = new OverlayFileSystem(BuildTree());
            using var host1 = new FileSystemHost(fs);
            int status = host1.Mount(_tmp + "-mount", null, false, 0);
            Assert.True(status >= 0, $"Mount failed: 0x{unchecked((uint)status):X8}");
            string mounted = host1.MountPoint()!;
            File.Delete(mounted + "\\readme.txt");
            host1.Unmount();
        }

        string mountAfter = _tmp + "-mount";
        if (Directory.Exists(mountAfter))
        {
            Directory.Delete(mountAfter);
        }

        {
            var fs = new OverlayFileSystem(BuildTree());
            using var host2 = new FileSystemHost(fs);
            int status = host2.Mount(_tmp + "-mount", null, false, 0);
            Assert.True(status >= 0, $"Mount failed: 0x{unchecked((uint)status):X8}");
            string mounted = host2.MountPoint()!;

            Assert.DoesNotContain(
                "readme.txt",
                Directory.GetFileSystemEntries(mounted).Select(Path.GetFileName)
            );
            Assert.False(File.Exists(mounted + "\\readme.txt"));
            Assert.True(File.Exists(BaseDir + "\\readme.txt"));
        }
    }
}
