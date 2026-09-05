using Xunit;

namespace Daedalus.VFS.Tests;

public class VirtualNodeTests
{
    private static VirtualNode<string> NewTree()
    {
        var root = new VirtualNode<string>("", NodeFlags.Directory, null, null);
        root.AddDirectory("meshes\\weapons");
        root.AddFile("meshes\\weapons\\sword.nif", "modA");
        root.AddFile("meshes\\armor.nif", "modB");
        root.AddFile("readme.txt", "base");
        return root;
    }

    [Fact]
    public void FindNode_Deep_Path_Resolves()
    {
        var root = NewTree();
        Assert.Equal("modA", root.FindNode("meshes\\weapons\\sword.nif")?.Data);
    }

    [Fact]
    public void FindNode_Is_CaseInsensitive_And_Separator_Agnostic()
    {
        var root = NewTree();
        Assert.Equal("modA", root.FindNode("MESHES\\Weapons\\Sword.NIF")?.Data);
        Assert.Equal("modA", root.FindNode("meshes/weapons/sword.nif")?.Data);
        Assert.Equal("modB", root.FindNode("meshes\\\\.\\\\armor.nif")?.Data);
    }

    [Fact]
    public void FindNode_Missing_Returns_Null()
    {
        var root = NewTree();
        Assert.Null(root.FindNode("meshes\\missing"));
        Assert.Null(root.FindNode("meshes\\weapons\\sword.nif\\extra")); // file is not a dir
    }

    [Fact]
    public void FindNode_EmptyPath_Returns_Self()
    {
        var root = NewTree();
        Assert.Same(root, root.FindNode(""));
    }

    [Fact]
    public void GetPath_Builds_Virtual_Path()
    {
        var root = NewTree();
        Assert.Equal("", root.GetPath());
        Assert.Equal("meshes", root.GetNode("meshes").GetPath());
        Assert.Equal("meshes\\weapons", root.GetNode("meshes").GetNode("weapons").GetPath());
    }

    [Fact]
    public void Counts_Reflect_Structure()
    {
        var root = NewTree();
        Assert.Equal(2, root.Count);
        Assert.Equal(6, root.CountRecursive);
    }

    [Fact]
    public void Children_Enumerate_Sorted()
    {
        var root = NewTree();
        Assert.Equal(
            new[] { "armor.nif", "weapons" },
            root.GetNode("meshes").Children.Select(c => c.Name)
        );
    }

    [Fact]
    public void AddFile_Creates_Placeholder_Intermediate_Directories()
    {
        var root = new VirtualNode<string>("", NodeFlags.Directory, null, null);
        var node = root.AddFile("a\\b\\c.txt", "payload");

        Assert.Equal("payload", node.Data);
        Assert.False(node.IsDirectory);
        var a = root.FindNode("a");
        Assert.True(a!.IsDirectory);
        Assert.Null(a.Data);
    }

    [Fact]
    public void AddFile_Existing_Node_Is_LastWriteWins()
    {
        var root = NewTree();
        root.AddFile("meshes\\weapons\\sword.nif", "modC");
        Assert.Equal("modC", root.FindNode("meshes\\weapons\\sword.nif")?.Data);
    }

    [Fact]
    public void AddFile_KindMismatch_Replaces_Placeholder_Directory()
    {
        var root = NewTree();
        // "meshes" exists as a directory; adding a FILE there must replace it
        root.AddFile("meshes", "now-a-file");

        var node = root.FindNode("meshes");
        Assert.NotNull(node);
        Assert.False(node!.IsDirectory);
        Assert.Equal("now-a-file", node.Data);
    }

    [Fact]
    public void AddDirectory_Existing_Node_Promotes_To_Directory()
    {
        var root = new VirtualNode<string>("", NodeFlags.Directory, null, null);
        root.AddFile("x\\y.txt", "data");
        root.AddDirectory("x\\y.txt");

        Assert.True(root.FindNode("x\\y.txt")!.IsDirectory);
    }

    [Fact]
    public void TryGetNode_And_Contains_Query_Direct_Children_Only()
    {
        var root = NewTree();
        Assert.True(root.GetNode("meshes").TryGetNode("weapons", out var node));
        Assert.Equal("weapons", node.Name);
        Assert.True(root.GetNode("meshes").ContainsChild("ARMOR.NIF"));
        Assert.False(root.TryGetNode("missing", out _));
        Assert.Throws<KeyNotFoundException>(() => root.GetNode("missing"));
    }

    [Fact]
    public void RemoveFromTree_Detaches_Node()
    {
        var root = NewTree();
        root.FindNode("meshes\\armor.nif")!.RemoveFromParent();

        Assert.Null(root.FindNode("meshes\\armor.nif"));
        Assert.Equal(1, root.GetNode("meshes").Count);
    }

    [Fact]
    public void VisitPath_Visits_Components_In_Order()
    {
        var root = NewTree();
        var visited = new List<string>();
        root.VisitPath("meshes\\weapons\\sword.nif", n => visited.Add(n.Name));

        Assert.Equal(new[] { "meshes", "weapons", "sword.nif" }, visited);
    }

    [Fact]
    public void VisitPath_Stops_At_First_Missing_Component()
    {
        var root = NewTree();
        var visited = new List<string>();
        root.VisitPath("meshes\\nowhere\\deep", n => visited.Add(n.Name));

        Assert.Equal(new[] { "meshes" }, visited);
    }

    [Fact]
    public void Clear_Removes_All_Children()
    {
        var root = NewTree();
        root.Clear();
        Assert.Equal(0, root.Count);
    }

    [Fact]
    public void Flags_Set_And_Clear()
    {
        var node = new VirtualNode<string>("n", NodeFlags.None, null, null);
        node.SetFlag(NodeFlags.Placeholder).SetFlag(NodeFlags.Directory);

        Assert.True(node.HasFlag(NodeFlags.Placeholder));
        Assert.True(node.IsDirectory);

        node.SetFlag(NodeFlags.Placeholder, false);
        Assert.False(node.HasFlag(NodeFlags.Placeholder));
        Assert.True(node.IsDirectory);
    }

    [Fact]
    public void Root_Walks_Upwards()
    {
        var root = NewTree();
        var leaf = root.FindNode("meshes\\weapons\\sword.nif")!;
        Assert.Same(root, leaf.Root);
    }

    [Fact]
    public void Dump_Writes_Hierarchy()
    {
        var root = NewTree();
        var writer = new StringWriter();
        root.Dump(writer);
        var dump = writer.ToString();

        Assert.Contains("meshes ->", dump);
        Assert.Contains("sword.nif -> modA", dump);
        Assert.Contains("readme.txt -> base", dump);
    }
}

public class FindTests
{
    private static VirtualNode<string> NewTree()
    {
        var root = new VirtualNode<string>("", NodeFlags.Directory, null, null);
        root.AddDirectory("meshes\\weapons");
        root.AddFile("meshes\\weapons\\sword.nif", "modA");
        root.AddFile("meshes\\weapons\\axe.nif", "modC");
        root.AddFile("meshes\\armor.nif", "modB");
        root.AddFile("readme.txt", "base");
        return root;
    }

    [Fact]
    public void Find_StarPattern_Stays_On_One_Level()
    {
        var root = NewTree();
        var hits = root.GetNode("meshes").Find("*.nif").Select(n => n.Name).ToList();
        Assert.Equal(new[] { "armor.nif" }, hits); // weapons/ contents are one level deeper
    }

    [Fact]
    public void Find_FixedPrefix_Narrows_To_Subtree()
    {
        var root = NewTree();
        var hits = root.Find("meshes\\weapons\\*.nif").Select(n => n.Name).OrderBy(n => n).ToList();
        Assert.Equal(new[] { "axe.nif", "sword.nif" }, hits);
    }

    [Fact]
    public void Find_Unknown_Prefix_Returns_Nothing()
    {
        var root = NewTree();
        Assert.Empty(root.Find("nowhere\\*.nif"));
    }

    [Fact]
    public void Find_StarDirectory_Spans_One_Level()
    {
        // diverges from usvfs: here "*\" obeys its documented intent
        var root = NewTree();
        var hits = root.Find("*\\*\\sword.nif");
        Assert.Single(hits);
        Assert.Equal("modA", hits[0].Data);
    }

    [Fact]
    public void Find_Literal_MultiComponent_Path_Resolves()
    {
        // diverges from usvfs for the same remainder-strip reason
        var root = NewTree();
        var hits = root.Find("meshes\\armor.nif");
        Assert.Single(hits);
        Assert.Equal("modB", hits[0].Data);
    }

    [Fact]
    public void Find_StarDirectory_Does_Not_Span_Two_Levels()
    {
        // meshes\*\sword.nif hits meshes\weapons but root-level "*\sword.nif" must NOT reach meshes\weapons
        var root = NewTree();
        Assert.Empty(root.Find("*\\sword.nif"));
        Assert.Single(root.Find("*\\armor.nif"));
    }

    [Fact]
    public void Find_Literal_Single_Name_Finds_Node()
    {
        var root = NewTree();
        var hits = root.Find("readme.txt");
        Assert.Single(hits);
        Assert.Equal("base", hits[0].Data);
    }
}
