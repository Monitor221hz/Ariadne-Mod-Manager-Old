using Daedalus.Contracts.Mods;
using Daedalus.ModManager.GUI.ViewModels;
using Daedalus.ModManager.GUI.Views;
using Daedalus.Mods;
using Xunit;

namespace Daedalus.ModManager.GUI.Tests;

public class NodeMenuItemsTests
{
    private sealed class FakeMod(string name) : ILibraryMod
    {
        public IModInfo Info { get; } =
            new Mods.ModInfo(0, SourceType.Local, "1.0", [], "", 0, false);
        public string Name { get; } = name;
        public DirectoryInfo Directory => new(".");
        public Daedalus.VFS.VirtualNode<ModFileEntry> Content { get; } =
            new("", Daedalus.VFS.NodeFlags.Directory, null, default);

        public void RefreshContent() { }

        public void RenameTo(string newName) { }

        public bool Equals(ILibraryMod? x, ILibraryMod? y) => ReferenceEquals(x, y);

        public int GetHashCode(ILibraryMod obj) => obj.GetHashCode();

        public bool Equals(ILibraryMod? other) => ReferenceEquals(this, other);
    }

    [Fact]
    public void Mod_Nodes_Get_Rename_And_Remove_Bound_To_Own_Commands()
    {
        var node = new ModEntryNodeViewModel(new FakeMod("A"));
        var items = NodeMenuItems.For(node);

        Assert.Equal(new[] { "Rename", "Remove" }, items.Select(item => item.Header));
        Assert.Same(node.StartRenameCommand, items[0].Command);
        Assert.Same(node.RemoveCommand, items[1].Command);
    }

    [Fact]
    public void Group_Nodes_Get_Rename_And_Dissolve_Bound_To_Own_Commands()
    {
        var node = new GroupHeaderNodeViewModel(new ModGroup("G", []));
        var items = NodeMenuItems.For(node);

        Assert.Equal(new[] { "Rename", "Dissolve" }, items.Select(item => item.Header));
        Assert.Same(node.StartRenameCommand, items[0].Command);
        Assert.Same(node.DissolveCommand, items[1].Command);
    }

    [Fact]
    public void File_Nodes_Get_Open()
    {
        var mod = new FakeMod("M");
        var entry = new ModFileEntry(
            "file.txt",
            ModEntryKind.File,
            mod.Info,
            "C:/tmp/file.txt",
            1,
            DateTimeOffset.Now
        );
        var node = new FileLeafNodeViewModel(
            new Daedalus.VFS.VirtualNode<ModFileEntry>(
                "file.txt",
                Daedalus.VFS.NodeFlags.None,
                null,
                entry
            )
        );
        var items = NodeMenuItems.For(node);

        Assert.Equal(new[] { "Open" }, items.Select(item => item.Header));
        Assert.Same(node.OpenCommand, items[0].Command);
    }

    [Fact]
    public void Directory_Nodes_Get_No_Items()
    {
        var node = new DirectoryNodeViewModel(
            new Daedalus.VFS.VirtualNode<ModFileEntry>(
                "dir",
                Daedalus.VFS.NodeFlags.Directory,
                null,
                default
            )
        );

        Assert.Empty(NodeMenuItems.For(node));
    }
}
