using Daedalus.VFS;
using Xunit;

namespace Daedalus.VFS.Tests;

public class FileSystemLinkerTests : IDisposable
{
    private readonly string _rootDir;

    public FileSystemLinkerTests()
    {
        _rootDir = Path.Combine(
            Path.GetTempPath(),
            "DaedalusBuilderTests-" + Guid.NewGuid().ToString("N")
        );
        Directory.CreateDirectory(_rootDir);
    }

    public void Dispose()
    {
        Directory.Delete(_rootDir, true);
    }

    private string ModDir(string name) =>
        Directory.CreateDirectory(Path.Combine(_rootDir, name)).FullName;

    private static string Touch(string dir, string relative)
    {
        string path = Path.Combine(dir, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "x");
        return path;
    }

    private static VirtualNode<BackedEntry> NewRoot() =>
        new("", NodeFlags.Directory, null, default);

    [Fact]
    public void LinkDirectory_Maps_Physical_Contents()
    {
        string mod = ModDir("modA");
        Touch(mod, "meshes\\sword.nif");
        Directory.CreateDirectory(Path.Combine(mod, "emptydir"));

        var root = NewRoot();
        root.LinkDirectory(mod, "");

        var sword = root.FindNode("meshes\\sword.nif");
        Assert.NotNull(sword);
        Assert.Equal(Path.Combine(mod, "meshes\\sword.nif"), sword!.Data.PhysicalPath);

        var empty = root.FindNode("emptydir");
        Assert.NotNull(empty);
        Assert.True(empty!.IsDirectory);
    }

    [Fact]
    public void LinkDirectory_Later_Links_Win_Conflicts()
    {
        string modA = ModDir("modA");
        string modB = ModDir("modB");
        Touch(modA, "sword.nif");
        Touch(modB, "sword.nif");
        Touch(modA, "only-in-a.txt");

        var root = NewRoot();
        root.LinkDirectory(modA, "");
        root.LinkDirectory(modB, "");

        Assert.Equal(
            Path.Combine(modB, "sword.nif"),
            root.FindNode("sword.nif")!.Data.PhysicalPath
        );
        Assert.Equal(
            Path.Combine(modA, "only-in-a.txt"),
            root.FindNode("only-in-a.txt")!.Data.PhysicalPath
        );
    }

    [Fact]
    public void LinkDirectory_Nested_Paths_Keep_Relative_Structure()
    {
        string mod = ModDir("modA");
        string deep = Touch(mod, "meshes\\weapons\\iron\\sword.nif");

        var root = NewRoot();
        root.LinkDirectory(mod, "Data");

        Assert.Equal(
            deep,
            root.FindNode("Data\\meshes\\weapons\\iron\\sword.nif")!.Data.PhysicalPath
        );
    }

    [Fact]
    public void LinkDirectory_CreateTarget_Sets_Flag_On_Destination()
    {
        string overwrite = ModDir("overwrite");

        var root = NewRoot();
        root.LinkDirectory(overwrite, "", LinkFlags.Recursive | LinkFlags.CreateTarget);
        Assert.True(root.HasFlag(NodeFlags.CreateTarget));

        var root2 = NewRoot();
        root2.LinkDirectory(overwrite, "saves", LinkFlags.Recursive | LinkFlags.CreateTarget);
        Assert.False(root2.HasFlag(NodeFlags.CreateTarget));
        Assert.True(root2.GetNode("saves").HasFlag(NodeFlags.CreateTarget));
    }

    [Fact]
    public void LinkDirectory_FailIfExists_Throws_On_Conflict()
    {
        string mod = ModDir("modA");
        Touch(mod, "sword.nif");

        var root = NewRoot();
        root.LinkDirectory(mod, "");
        Assert.Throws<IOException>(() => root.LinkDirectory(mod, "", LinkFlags.FailIfExists));
    }

    [Fact]
    public void LinkDirectory_NonRecursive_Skips_Nested()
    {
        string mod = ModDir("modA");
        Touch(mod, "top.txt");
        Touch(mod, "nested\\deep.txt");

        var root = NewRoot();
        root.LinkDirectory(mod, "", LinkFlags.None);

        Assert.NotNull(root.FindNode("top.txt"));
        Assert.NotNull(root.FindNode("nested"));
        Assert.Null(root.FindNode("nested\\deep.txt"));
    }

    [Fact]
    public void LinkFile_Creates_Placeholder_Intermediates()
    {
        var root = NewRoot();
        var node = root.LinkFile("C:\\mods\\x\\wall.dds", "textures\\dungeon\\wall.dds");

        Assert.Equal("C:\\mods\\x\\wall.dds", node.Data.PhysicalPath);
        Assert.True(root.FindNode("textures")!.IsDirectory);
        Assert.Null(root.FindNode("textures")!.Data.PhysicalPath); // placeholder: no mapping
    }

    [Fact]
    public void LinkFile_FailIfExists_Throws()
    {
        var root = NewRoot();
        root.LinkFile("C:\\one\\a.txt", "a.txt");
        Assert.Throws<IOException>(() =>
            root.LinkFile("C:\\two\\a.txt", "a.txt", LinkFlags.FailIfExists)
        );
    }

    private static string WriteFile(string dir, string relative)
    {
        string path = Path.Combine(dir, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "x");
        return path;
    }

    [Fact]
    public void LinkDirectory_Whiteouts_Hide_Lowered_Layer_And_Never_Link()
    {
        string mod = ModDir("modA");
        string overwrite = ModDir("overwrite");
        Touch(mod, "readme.txt");
        Touch(mod, "keep.txt");
        WriteFile(overwrite, "readme.txt.daehidden");
        Touch(overwrite, "session.log");

        var root = NewRoot();
        root.LinkDirectory(mod, "", LinkFlags.Recursive);
        root.LinkDirectory(overwrite, "", LinkFlags.Recursive | LinkFlags.Whiteouts);

        Assert.Null(root.FindNode("readme.txt"));
        Assert.Null(root.FindNode("readme.txt.daehidden"));
        Assert.NotNull(root.FindNode("keep.txt"));
        Assert.NotNull(root.FindNode("session.log"));
    }

    [Fact]
    public void LinkDirectory_Whiteouts_Marker_Wins_Even_Beside_Real_File()
    {
        string mod = ModDir("modA");
        string overwrite = ModDir("overwrite");
        Touch(mod, "readme.txt");
        Touch(overwrite, "readme.txt");
        WriteFile(overwrite, "readme.txt.daehidden");

        var root = NewRoot();
        root.LinkDirectory(mod, "", LinkFlags.Recursive);
        root.LinkDirectory(overwrite, "", LinkFlags.Recursive | LinkFlags.Whiteouts);

        Assert.Null(root.FindNode("readme.txt"));
    }

    [Fact]
    public void LinkDirectory_Without_Whiteouts_Leaves_Markers_As_Files()
    {
        string overwrite = ModDir("overwrite");
        WriteFile(overwrite, "gone.txt.daehidden");

        var root = NewRoot();
        root.LinkDirectory(overwrite, "", LinkFlags.Recursive);

        Assert.NotNull(root.FindNode("gone.txt.daehidden"));
    }
}
