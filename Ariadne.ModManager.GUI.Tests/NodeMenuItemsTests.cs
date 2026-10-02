using Ariadne.Contracts.Games;
using Ariadne.Contracts.ModManager;
using Ariadne.Games;
using Ariadne.ModManager;
using Ariadne.ModManager.GUI.ViewModels;
using Ariadne.ModManager.GUI.Views;
using Xunit;

namespace Ariadne.ModManager.GUI.Tests;

public class NodeMenuItemsTests
{
    private sealed class FakeMod(string name) : ILibraryMod
    {
        public IModInfo Info { get; } = new ModManager.ModInfo(0, SourceType.Local, "1.0", [], "");
        public string Name { get; } = name;
        public DirectoryInfo Directory => new(".");
        public Ariadne.VFS.VirtualNode<ModFileEntry> Content { get; } =
            new("", Ariadne.VFS.NodeFlags.Directory, null, default);

        public void RefreshContent() { }

        public void ReplaceInfo(IModInfo info) { }

        public void RenameTo(string newName) { }

        public bool Equals(ILibraryMod? x, ILibraryMod? y) => ReferenceEquals(x, y);

        public int GetHashCode(ILibraryMod obj) => obj.GetHashCode();

        public bool Equals(ILibraryMod? other) => ReferenceEquals(this, other);
    }

    [Fact]
    public void Mod_Nodes_Get_Rename_And_Remove_Bound_To_Own_Commands()
    {
        var node = new ModEntryNodeViewModel(new ModListEntry(new FakeMod("A"), true));
        var items = NodeMenuItems.For(node);

        Assert.Equal(new[] { "Rename", "Forget", "Delete" }, items.Select(item => item.Header));
        Assert.Same(node.StartRenameCommand, items[0].Command);
        Assert.Same(node.ForgetFromProfileCommand, items[1].Command);
        Assert.Same(node.DeleteFromDiskCommand, items[2].Command);
        Assert.Contains("without deleting", items[1].Tooltip!);
        Assert.Contains("cannot be undone", items[2].Tooltip!);
        Assert.Equal("F2", items[0].Gesture?.ToString());
        Assert.Equal("Delete", items[2].Gesture?.ToString());
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
            new Ariadne.VFS.VirtualNode<ModFileEntry>(
                "file.txt",
                Ariadne.VFS.NodeFlags.None,
                null,
                entry
            ),
            new ModEntryNodeViewModel(new ModListEntry(mod, true))
        );
        var items = NodeMenuItems.For(node);

        Assert.Equal(new[] { "Open" }, items.Select(item => item.Header));
        Assert.Same(node.OpenCommand, items[0].Command);
    }

    [Fact]
    public void Directory_Nodes_Get_No_Items()
    {
        var node = new DirectoryNodeViewModel(
            new Ariadne.VFS.VirtualNode<ModFileEntry>(
                "dir",
                Ariadne.VFS.NodeFlags.Directory,
                null,
                default
            ),
            new ModEntryNodeViewModel(new ModListEntry(new FakeMod("M"), true))
        );

        Assert.Empty(NodeMenuItems.For(node));
    }

    [Fact]
    public void Mod_Nodes_Get_Target_Submenu_For_Game_InstallTargets()
    {
        var node = new ModEntryNodeViewModel(new ModListEntry(new FakeMod("A"), true));
        var targets = new IGamePath[]
        {
            new GamePath("Data", "Data", []),
            new GamePath("Root", "", []),
        };

        var items = NodeMenuItems.For(node, targets);

        var targetItem = Assert.Single(items, item => item.Header == "Target...");
        Assert.Null(targetItem.Command);
        Assert.Equal(
            new[] { "Data", "Root" },
            targetItem.Children!.Select(child => child.Header).ToArray()
        );
        Assert.All(
            targetItem.Children!,
            child => Assert.Same(node.SetTargetCommand, child.Command)
        );
        Assert.Same(targets[0], targetItem.Children![0].CommandParameter);
    }

    [Fact]
    public void Mod_Nodes_Without_Targets_Get_No_Target_Item()
    {
        var node = new ModEntryNodeViewModel(new ModListEntry(new FakeMod("A"), true));

        Assert.DoesNotContain(NodeMenuItems.For(node), item => item.Header == "Target...");
        Assert.DoesNotContain(NodeMenuItems.For(node, []), item => item.Header == "Target...");
    }

    [Fact]
    public void Target_Submenu_Command_Sets_Mod_Target()
    {
        var mod = new FakeMod("A");
        var node = new ModEntryNodeViewModel(new ModListEntry(mod, true));
        var target = new GamePath("Data", "Data", []);
        var changed = 0;
        node.TargetChanged.Subscribe(_ => changed++);

        node.SetTargetCommand.Execute(target).Subscribe();

        Assert.Equal("Data", mod.Info.Target);
        Assert.Equal("Data", node.Target);
        Assert.Equal(1, changed);
    }
}
