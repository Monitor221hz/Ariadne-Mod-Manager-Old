using System.Security.AccessControl;
using System.Text;
using Daedalus.VFS;
using Daedalus.VFS.WinFsp;
using Fsp;
using Xunit;
using FileInfo = Fsp.Interop.FileInfo;

namespace Daedalus.VFS.WinFsp.Tests;

public class OverlayFileSystemHeadlessTests : IDisposable
{
    private static readonly int STATUS_DIRECTORY_NOT_EMPTY = unchecked((int)0xC0000101);
    private static readonly int STATUS_NOT_SUPPORTED = unchecked((int)0xC00000BB);

    private readonly string _tmp;
    public string BaseDir { get; }
    public string ModADir { get; }
    public string ModBDir { get; }
    public string OverwriteDir { get; }
    public VirtualNode<BackedEntry> Root { get; }
    public OverlayFileSystem FS { get; }

    public OverlayFileSystemHeadlessTests()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        _tmp = Path.Combine(
            Path.GetTempPath(),
            "DaedalusOverlayTests-" + Guid.NewGuid().ToString("N")
        );
        BaseDir = Path.Combine(_tmp, "base");
        ModADir = Path.Combine(_tmp, "modA");
        ModBDir = Path.Combine(_tmp, "modB");
        OverwriteDir = Path.Combine(_tmp, "overwrite");
        Directory.CreateDirectory(BaseDir);
        Directory.CreateDirectory(ModADir);
        Directory.CreateDirectory(ModBDir);
        Directory.CreateDirectory(OverwriteDir);

        File.WriteAllText(Path.Combine(BaseDir, "readme.txt"), "base-readme");
        Directory.CreateDirectory(Path.Combine(BaseDir, "textures"));
        File.WriteAllText(Path.Combine(BaseDir, "textures", "armor.dds"), "base-armor");
        Directory.CreateDirectory(Path.Combine(ModADir, "textures"));
        File.WriteAllText(Path.Combine(ModADir, "textures", "armor.dds"), "modA-armor");
        Directory.CreateDirectory(Path.Combine(ModADir, "meshes"));
        File.WriteAllText(Path.Combine(ModADir, "meshes", "sword.nif"), "modA-mesh");
        Directory.CreateDirectory(Path.Combine(ModBDir, "textures"));
        File.WriteAllText(Path.Combine(ModBDir, "textures", "armor.dds"), "modB-armor");

        Root = new VirtualNode<BackedEntry>("", NodeFlags.Directory, null, default);
        Root.LinkDirectory(BaseDir, "");
        Root.LinkDirectory(ModADir, "");
        Root.LinkDirectory(ModBDir, "");
        Root.LinkDirectory(
            OverwriteDir,
            "",
            LinkFlags.Recursive | LinkFlags.CreateTarget | LinkFlags.Whiteouts
        );
        FS = new OverlayFileSystem(Root);
    }

    [SkippableFact]
    public void Ctor_With_Rules_Tracker_MountRoot_Does_Not_Throw()
    {
        SkipNonWindows();
        var fs = new OverlayFileSystem(
            Root,
            new OverlayFileSystemOptions
            {
                OutputRules = new[] { new OutputRule("C:\\Tools\\test.exe", BaseDir) },
                PhysicalMountRoot = Path.Combine(_tmp, "mount"),
            }
        );
        Assert.NotNull(fs);
    }

    public void Dispose()
    {
        if (_tmp == null)
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

    private void SkipNonWindows() => Skip.If(!OperatingSystem.IsWindows(), "Windows only");

    private FileSystemDescription OpenFile(string name, FileSystemRights access)
    {
        int status = FS.Open(name, (FileCreateOptions)0, access, out _, out var desc, out _, out _);
        Assert.Equal(0, status);
        Assert.NotNull(desc);
        return desc!;
    }

    private byte[] ReadAll(FileSystemDescription desc)
    {
        var bytes = new byte[desc.Stream!.Length];
        FS.Read(null!, desc, bytes, 0, (uint)bytes.Length, out uint read);
        Assert.Equal((uint)bytes.Length, read);
        return bytes;
    }

    [SkippableFact]
    public void Open_Resolves_To_Highest_Priority_Layer()
    {
        SkipNonWindows();
        using var desc = OpenFile("\\textures\\armor.dds", FileSystemRights.ReadData);
        Assert.Equal("modB-armor", Encoding.UTF8.GetString(ReadAll(desc)));
    }

    [SkippableFact]
    public void Open_Resolves_Base_Only_File()
    {
        SkipNonWindows();
        using var desc = OpenFile("\\readme.txt", FileSystemRights.ReadData);
        Assert.Equal("base-readme", Encoding.UTF8.GetString(ReadAll(desc)));
    }

    [SkippableFact]
    public void Open_ReadOnly_Never_Copies_To_Overwrite()
    {
        SkipNonWindows();
        using var desc = OpenFile("\\meshes\\sword.nif", FileSystemRights.ReadData);
        Assert.Empty(Directory.EnumerateFileSystemEntries(OverwriteDir));
    }

    [SkippableFact]
    public void Open_WriteAccess_Without_Writing_Still_Copies_Nothing()
    {
        SkipNonWindows();
        // Tannin hedge: speculative write opens must not trigger copy-up
        using var desc = OpenFile("\\textures\\armor.dds", FileSystemRights.FullControl);
        Assert.Empty(Directory.EnumerateFileSystemEntries(OverwriteDir));
    }

    [SkippableFact]
    public void Write_Triggers_CopyUp_Into_Overwrite()
    {
        SkipNonWindows();
        var desc = OpenFile("\\textures\\armor.dds", FileSystemRights.FullControl);
        FS.Write(null!, desc, Encoding.UTF8.GetBytes("edited"), 0, 6, false, false, out _, out _);
        // close before external reads: our FullControl handle isn't share-compatible
        // with a FileShare.Read open
        desc.Dispose();

        // original untouched
        Assert.Equal(
            "modB-armor",
            File.ReadAllText(Path.Combine(ModBDir, "textures", "armor.dds"))
        );
        // overwrite copy exists and carries whole content
        string sink = Path.Combine(OverwriteDir, "textures", "armor.dds");
        Assert.True(File.Exists(sink));
        Assert.Equal("edited", File.ReadAllText(sink).Substring(0, 6));

        // tree now resolves to the sink
        FS.Open(
            "\\textures\\armor.dds",
            (FileCreateOptions)0,
            FileSystemRights.ReadData,
            out _,
            out var reopened,
            out _,
            out _
        );
        Assert.StartsWith(
            OverwriteDir,
            reopened!.Owner.Data.PhysicalPath,
            StringComparison.OrdinalIgnoreCase
        );
    }

    [SkippableFact]
    public void Create_New_File_Lands_In_Overwrite()
    {
        SkipNonWindows();
        int status = FS.Create(
            "\\saves\\slot1.sav",
            (FileCreateOptions)0,
            FileSystemRights.FullControl,
            FileAttributes.Normal,
            null,
            0,
            out var node,
            out var desc,
            out _,
            out _
        );
        Assert.Equal(0, status);
        Assert.True(File.Exists(Path.Combine(OverwriteDir, "saves", "slot1.sav")));

        FS.Close(node!, desc!);
    }

    [SkippableFact]
    public void Create_Deep_Path_Makes_Placeholders_And_Physical_Dirs()
    {
        SkipNonWindows();
        int status = FS.Create(
            "\\newdir\\deep\\file.txt",
            (FileCreateOptions)0,
            FileSystemRights.FullControl,
            FileAttributes.Normal,
            null,
            0,
            out var node,
            out var desc,
            out _,
            out _
        );
        Assert.Equal(0, status);
        Assert.True(
            Directory.Exists(Path.Combine(OverwriteDir, "newdir"))
                && File.Exists(Path.Combine(OverwriteDir, "newdir", "deep", "file.txt"))
        );
        FS.Close(node!, desc!);

        // intermediate dir is a placeholder until materialized
        var dirNode = Root.FindNode("newdir");
        Assert.NotNull(dirNode);
        Assert.True(dirNode!.IsDirectory);
    }

    [SkippableFact]
    public void Create_Existing_Returns_Collision()
    {
        SkipNonWindows();
        int status = FS.Create(
            "\\readme.txt",
            (FileCreateOptions)0,
            FileSystemRights.FullControl,
            FileAttributes.Normal,
            null,
            0,
            out _,
            out _,
            out _,
            out _
        );
        Assert.Equal(unchecked((int)0xC0000035), status); // STATUS_OBJECT_NAME_COLLISION
    }

    [SkippableFact]
    public void Create_Directory_In_Overwrite()
    {
        SkipNonWindows();
        int status = FS.Create(
            "\\saves",
            FileCreateOptions.DirectoryFile,
            FileSystemRights.FullControl,
            FileAttributes.Directory,
            null,
            0,
            out var node,
            out _,
            out _,
            out _
        );
        Assert.Equal(0, status);
        Assert.True(Directory.Exists(Path.Combine(OverwriteDir, "saves")));
        Assert.NotNull(Root.FindNode("saves"));
    }

    [SkippableFact]
    public void Delete_Masks_File_With_Marker_And_Hides_Tree_Entry()
    {
        SkipNonWindows();
        FS.Open(
            "\\readme.txt",
            (FileCreateOptions)0,
            FileSystemRights.FullControl,
            out var fsNode,
            out var desc,
            out _,
            out _
        );

        Assert.Equal(0, FS.CanDelete(fsNode!, desc!, "\\readme.txt"));
        FS.Cleanup(fsNode!, desc!, "\\readme.txt", CleanupFlags.Delete);
        FS.Close(fsNode!, desc!);

        Assert.True(File.Exists(Path.Combine(OverwriteDir, "readme.txt.daehidden")));
        Assert.Null(Root.FindNode("readme.txt"));
    }

    [SkippableFact]
    public void CanDelete_NonEmptyDirectory_Is_Refused()
    {
        SkipNonWindows();
        var desc = new FileSystemDescription(
            Root.FindNode("textures")!,
            new DirectoryInfo(ModBDir + "\\textures")
        );
        Assert.Equal(STATUS_DIRECTORY_NOT_EMPTY, FS.CanDelete(null!, desc, "\\textures"));
    }

    [SkippableFact]
    public void Rename_Moves_Into_Sink_And_Rekeys_Tree()
    {
        SkipNonWindows();
        var desc = OpenFile("\\meshes\\sword.nif", FileSystemRights.FullControl);
        var ok = FS.Open(
            "\\meshes\\sword.nif",
            (FileCreateOptions)0,
            FileSystemRights.FullControl,
            out var fsNode,
            out _,
            out _,
            out _
        );

        int status = FS.Rename(fsNode!, desc, "\\meshes\\sword.nif", "\\meshes\\blade.nif", false);
        Assert.Equal(0, status);
        desc.Dispose();

        Assert.Null(Root.FindNode("meshes\\sword.nif"));
        var moved = Root.FindNode("meshes\\blade.nif");
        Assert.NotNull(moved);
        Assert.StartsWith(
            OverwriteDir,
            moved!.Data.PhysicalPath,
            StringComparison.OrdinalIgnoreCase
        );
        Assert.True(File.Exists(Path.Combine(OverwriteDir, "meshes", "blade.nif")));
        Assert.Equal(
            "modA-mesh",
            File.ReadAllText(Path.Combine(OverwriteDir, "meshes", "blade.nif"))
        );
    }

    [SkippableFact]
    public void CopyUp_Consumes_Stale_Marker()
    {
        SkipNonWindows();
        Directory.CreateDirectory(Path.Combine(OverwriteDir, "meshes"));
        string marker = Path.Combine(OverwriteDir, "meshes", "sword.nif.daehidden");
        File.WriteAllText(marker, "");

        var desc = OpenFile("\\meshes\\sword.nif", FileSystemRights.FullControl);
        FS.Write(null!, desc, Encoding.UTF8.GetBytes("x"), 0, 1, false, false, out _, out _);
        desc.Dispose();

        Assert.False(File.Exists(marker));
        Assert.True(File.Exists(Path.Combine(OverwriteDir, "meshes", "sword.nif")));
    }

    [SkippableFact]
    public void Create_At_Hidden_Path_Overwrites_Physical_Remnant()
    {
        SkipNonWindows();
        File.WriteAllText(Path.Combine(OverwriteDir, "ghost.txt"), "stale-content");
        Assert.False(File.Exists(Path.Combine(OverwriteDir, "ghost.txt.daehidden")));

        int status = FS.Create(
            "\\ghost.txt",
            (FileCreateOptions)0,
            FileSystemRights.FullControl,
            FileAttributes.Normal,
            null,
            0,
            out var node,
            out var desc,
            out _,
            out _
        );
        Assert.Equal(0, status);

        FS.Close(node!, desc!);
        desc!.Dispose();
        Assert.Equal("", File.ReadAllText(Path.Combine(OverwriteDir, "ghost.txt")));
    }

    [SkippableFact]
    public void Read_Beyond_End_Of_File_Returns_StatusEndOfFile()
    {
        SkipNonWindows();
        var desc = OpenFile("\\readme.txt", FileSystemRights.FullControl);
        Span<byte> buffer = stackalloc byte[16];
        int status = FS.Read(null!, desc, buffer, 100, 10, out uint transferred);
        Assert.Equal(unchecked((int)0xC0000011), status); // STATUS_END_OF_FILE
        Assert.Equal(0u, transferred);
        desc.Dispose();
    }

    [SkippableFact]
    public void GetSecurity_Works_Without_ReadControl_On_Handle()
    {
        SkipNonWindows();
        var desc = OpenFile(
            "\\meshes\\sword.nif",
            FileSystemRights.ReadData
                | FileSystemRights.ReadAttributes
                | FileSystemRights.Synchronize
        );
        byte[]? sd = null;
        Assert.Equal(0, FS.GetSecurity(null!, desc, ref sd));
        Assert.NotNull(sd);
        Assert.NotEmpty(sd!);
        desc.Dispose();
    }

    [SkippableFact]
    public void Create_Consumes_Stale_Marker()
    {
        SkipNonWindows();
        string marker = Path.Combine(OverwriteDir, "fresh.txt.daehidden");
        File.WriteAllText(marker, "");

        int status = FS.Create(
            "\\fresh.txt",
            (FileCreateOptions)0,
            FileSystemRights.FullControl,
            FileAttributes.Normal,
            null,
            0,
            out var node,
            out var desc,
            out _,
            out _
        );
        Assert.Equal(0, status);
        Assert.False(File.Exists(marker));
        FS.Close(node!, desc!);
    }

    [SkippableFact]
    public void Delete_Then_Recreate_Keeps_File_Visible()
    {
        SkipNonWindows();
        FS.Open(
            "\\readme.txt",
            (FileCreateOptions)0,
            FileSystemRights.FullControl,
            out var fsNode,
            out var desc,
            out _,
            out _
        );
        FS.Cleanup(fsNode!, desc!, "\\readme.txt", CleanupFlags.Delete);
        FS.Close(fsNode!, desc!);

        int status = FS.Create(
            "\\readme.txt",
            (FileCreateOptions)0,
            FileSystemRights.FullControl,
            FileAttributes.Normal,
            null,
            0,
            out var newNode,
            out var newDesc,
            out _,
            out _
        );
        Assert.Equal(0, status);
        FS.Close(newNode!, newDesc!);

        Assert.False(File.Exists(Path.Combine(OverwriteDir, "readme.txt.daehidden")));
        Assert.NotNull(Root.FindNode("readme.txt"));
    }

    [SkippableFact]
    public void Rename_Directory_Is_Refused()
    {
        SkipNonWindows();
        int ok = FS.Open(
            "\\textures",
            (FileCreateOptions)0,
            FileSystemRights.FullControl,
            out var fsNode,
            out var desc,
            out _,
            out _
        );
        Assert.Equal(0, ok);
        int status = FS.Rename(fsNode!, desc!, "\\textures", "\\textures2", false);
        Assert.Equal(STATUS_NOT_SUPPORTED, status);
    }

    [SkippableFact]
    public void Rename_SinkResidentDirectory_Moves_Subtree()
    {
        SkipNonWindows();
        // runtime content is sink-resident from birth: \session\sub\a.txt
        FS.Create(
            "\\session",
            FileCreateOptions.DirectoryFile,
            FileSystemRights.FullControl,
            FileAttributes.Directory,
            null,
            0,
            out var dirFsNode,
            out var dirDesc,
            out _,
            out _
        );
        FS.Create(
            "\\session\\sub\\a.txt",
            (FileCreateOptions)0,
            FileSystemRights.FullControl,
            FileAttributes.Normal,
            null,
            0,
            out var fileFsNode,
            out var fileDesc,
            out _,
            out _
        );

        FS.Close(fileFsNode!, fileDesc!);

        int status = FS.Rename(dirFsNode!, dirDesc!, "\\session", "\\archive", false);
        Assert.Equal(0, status);

        Assert.Null(Root.FindNode("session"));
        var moved = Root.FindNode("archive\\sub\\a.txt");
        Assert.NotNull(moved);
        Assert.True(
            Directory.Exists(Path.Combine(OverwriteDir, "archive"))
                && File.Exists(Path.Combine(OverwriteDir, "archive", "sub", "a.txt"))
        );
        Assert.False(Directory.Exists(Path.Combine(OverwriteDir, "session")));
    }

    [SkippableFact]
    public void SetBasicInfo_On_Mod_File_Applies_In_Place_Without_CopyUp()
    {
        SkipNonWindows();
        using var desc = OpenFile("\\meshes\\sword.nif", FileSystemRights.FullControl);
        string sink = Path.Combine(OverwriteDir, "meshes", "sword.nif");
        string modFile = Path.Combine(ModADir, "meshes", "sword.nif");

        FS.SetBasicInfo(
            null!,
            desc,
            System.IO.FileAttributes.Normal,
            null,
            null,
            new DateTime(2021, 5, 5, 5, 5, 5, DateTimeKind.Utc),
            null,
            out _
        );

        Assert.False(File.Exists(sink));
        Assert.Equal(
            new DateTime(2021, 5, 5, 5, 5, 5, DateTimeKind.Utc),
            File.GetLastWriteTimeUtc(modFile)
        );
    }

    [SkippableFact]
    public void GetSecurityByName_Returns_Attributes()
    {
        SkipNonWindows();
        int status = FS.GetSecurityByName("\\textures\\armor.dds", out var attrs, ref _nullBytes);
        Assert.Equal(0, status);
        Assert.False(attrs.HasFlag(System.IO.FileAttributes.Directory));

        status = FS.GetSecurityByName("\\textures", out var dirAttrs, ref _nullBytes);
        Assert.Equal(0, status);
        Assert.True(dirAttrs.HasFlag(System.IO.FileAttributes.Directory));
    }

    private static byte[]? _nullBytes = null;

    [SkippableFact]
    public void GetSecurityByName_MissingPath_Returns_NotFound_Without_Throwing()
    {
        SkipNonWindows();
        byte[]? sd = null;
        int status = FS.GetSecurityByName("\\no\\such\\path.dll", out _, ref sd);
        Assert.Equal(unchecked((int)0xC0000034), status); // STATUS_OBJECT_NAME_NOT_FOUND
        Assert.Null(sd);
    }

    [SkippableFact]
    public void Open_MissingPath_Returns_NotFound_Without_Throwing()
    {
        SkipNonWindows();
        int status = FS.Open(
            "\\no\\such\\path.dll",
            (FileCreateOptions)0,
            FileSystemRights.FullControl,
            out var node,
            out var desc,
            out _,
            out _
        );
        Assert.Equal(unchecked((int)0xC0000034), status);
        Assert.Null(node);
        Assert.Null(desc);
    }

    private FileSystemNode OpenNode(string name)
    {
        int status = FS.Open(
            name,
            (FileCreateOptions)0,
            FileSystemRights.FullControl,
            out var node,
            out _,
            out _,
            out _
        );
        Assert.Equal(0, status);
        return node!;
    }

    [SkippableFact]
    public void ReadDirectoryEntry_Enumerates_Union()
    {
        SkipNonWindows();
        var rootNode = OpenNode("\\");
        var rootDesc = new FileSystemDescription(Root);
        var names = new List<string>();
        object? context = null;
        while (
            FS.ReadDirectoryEntry(rootNode, rootDesc, null, null, ref context, out var name, out _)
        )
        {
            names.Add(name!);
        }

        Assert.Contains("textures", names);
        Assert.Contains("meshes", names);
        Assert.Contains("readme.txt", names);
    }

    [SkippableFact]
    public void ReadDirectoryEntry_Returns_DotEntries_First_Like_NTFS()
    {
        SkipNonWindows();
        var rootNode = OpenNode("\\");
        var rootDesc = new FileSystemDescription(Root);
        var names = new List<string>();
        object? context = null;
        while (
            FS.ReadDirectoryEntry(rootNode, rootDesc, null, null, ref context, out var name, out _)
        )
        {
            names.Add(name!);
        }
        Assert.Equal(".", names[0]);
        Assert.Equal("..", names[1]);
        Assert.Contains("textures", names);
        Assert.Contains("readme.txt", names);
    }

    [SkippableFact]
    public void ReadDirectoryEntry_Pattern_Filters()
    {
        SkipNonWindows();
        var texNode = OpenNode("\\textures");
        var texDesc = new FileSystemDescription(
            Root.FindNode("textures")!,
            new DirectoryInfo(Path.Combine(ModBDir, "textures"))
        );
        var names = new List<string>();
        object? context = null;
        while (
            FS.ReadDirectoryEntry(texNode, texDesc, "*.dds", null, ref context, out var name, out _)
        )
        {
            names.Add(name!);
        }
        Assert.Contains("armor.dds", names);
        Assert.DoesNotContain("readme.txt", names);
    }

    [SkippableFact]
    public void ReadDirectoryEntry_Marker_Resume_After_Pattern()
    {
        SkipNonWindows();
        var rootNode = OpenNode("\\");
        var rootDesc = new FileSystemDescription(Root);
        object? context = null;
        FS.ReadDirectoryEntry(rootNode, rootDesc, null, null, ref context, out _, out _);

        object? resume = null;
        bool hasNext = FS.ReadDirectoryEntry(
            rootNode,
            rootDesc,
            null,
            "meshes",
            ref resume,
            out var next,
            out _
        );
        Assert.True(hasNext);
        Assert.Equal("readme.txt", next);
    }

    [SkippableFact]
    public void SetBasicInfo_On_Placeholder_Dir_Materializes_It()
    {
        SkipNonWindows();
        int status = FS.Create(
            "\\virtual\\deep.txt",
            (FileCreateOptions)0,
            FileSystemRights.FullControl,
            FileAttributes.Normal,
            null,
            0,
            out var fileNode,
            out var desc,
            out _,
            out _
        );
        FS.Close(fileNode!, desc!);

        var dirNode = Root.FindNode("virtual");
        Assert.NotNull(dirNode);
        Assert.Null(dirNode!.Data.PhysicalPath);

        var dirDesc = new FileSystemDescription(dirNode);
        var info = dirDesc.GetFileInfo();
        Assert.True(
            ((System.IO.FileAttributes)info.FileAttributes).HasFlag(
                System.IO.FileAttributes.Directory
            )
        );

        FS.SetBasicInfo(
            null!,
            dirDesc,
            System.IO.FileAttributes.Directory | System.IO.FileAttributes.Hidden,
            null,
            null,
            null,
            null,
            out _
        );

        Assert.True(Directory.Exists(Path.Combine(OverwriteDir, "virtual")));
        Assert.True(
            File.GetAttributes(Path.Combine(OverwriteDir, "virtual"))
                .HasFlag(System.IO.FileAttributes.Hidden)
        );
    }

    [SkippableFact]
    public void GetFileInfo_Survives_Backing_Deleted_After_Open()
    {
        SkipNonWindows();
        var desc = OpenFile("\\meshes\\sword.nif", FileSystemRights.FullControl);
        string physical = Path.Combine(ModADir, "meshes", "sword.nif");
        File.Delete(physical);

        int status = FS.GetFileInfo(null!, desc, out var info);
        Assert.Equal(0, status);
        Assert.True(info.FileSize > 0);
        desc.Dispose();
    }

    [SkippableFact]
    public void GetFileInfo_Works_With_SliverHandle()
    {
        SkipNonWindows();
        // metadata from backing path not caller's handle access mask
        var desc = OpenFile(
            "\\meshes\\sword.nif",
            FileSystemRights.ReadData | FileSystemRights.Synchronize
        );
        int status = FS.GetFileInfo(null!, desc, out var info);
        Assert.Equal(0, status);
        Assert.True(info.FileSize > 0);
        desc.Dispose();
    }

    [SkippableFact]
    public void Concurrent_Writes_To_Same_File_CopyUp_Exactly_Once()
    {
        SkipNonWindows();
        var descA = OpenFile("\\meshes\\sword.nif", FileSystemRights.FullControl);
        var descB = OpenFile("\\meshes\\sword.nif", FileSystemRights.FullControl);
        var payload = Encoding.UTF8.GetBytes("x");

        var taskA = Task.Run(() =>
        {
            for (int i = 0; i < 100; i++)
            {
                FS.Write(null!, descA, payload, 0, 1, false, false, out _, out _);
            }
        });
        var taskB = Task.Run(() =>
        {
            for (int i = 0; i < 100; i++)
            {
                FS.Write(null!, descB, payload, 0, 1, false, false, out _, out _);
            }
        });
        Assert.True(Task.WaitAll(new[] { taskA, taskB }, TimeSpan.FromSeconds(30)));

        string sink = Path.Combine(OverwriteDir, "meshes", "sword.nif");
        Assert.True(File.Exists(sink));
        descA.Dispose();
        descB.Dispose();
        Assert.Equal("modA-mesh", File.ReadAllText(Path.Combine(ModADir, "meshes", "sword.nif")));
    }

    [SkippableFact]
    public void Write_After_CopyUp_Completed_Redirects_Stale_Handle()
    {
        SkipNonWindows();
        var descA = OpenFile("\\meshes\\sword.nif", FileSystemRights.FullControl);
        var descB = OpenFile("\\meshes\\sword.nif", FileSystemRights.FullControl);
        var payload = Encoding.UTF8.GetBytes("x");

        FS.Write(null!, descA, payload, 0, 1, false, false, out _, out _);
        Assert.True(File.Exists(Path.Combine(OverwriteDir, "meshes", "sword.nif")));

        FS.Write(null!, descB, payload, 0, 1, false, false, out _, out _);

        descA.Dispose();
        descB.Dispose();
        Assert.Equal("modA-mesh", File.ReadAllText(Path.Combine(ModADir, "meshes", "sword.nif")));
        Assert.Equal(
            "xodA-mesh",
            File.ReadAllText(Path.Combine(OverwriteDir, "meshes", "sword.nif"))
        );
    }
}
