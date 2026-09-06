using Avalonia.Controls.DataGridDragDrop;
using Avalonia.Input;
using Daedalus.Contracts.Mods;
using Daedalus.ModManager.GUI.DragDrop;
using Daedalus.ModManager.GUI.ViewModels;
using Daedalus.Mods;
using Xunit;

namespace Daedalus.ModManager.GUI.Tests;

public class ModListDropRulesTests
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

    private static ModEntryNodeViewModel Mod(string name) => new(new FakeMod(name));

    private static GroupHeaderNodeViewModel Group(string name, params TreeNodeViewModel[] children)
    {
        var group = new GroupHeaderNodeViewModel(new ModGroup(name, []));
        foreach (var child in children)
        {
            group.ObservableChildren.Add(child);
        }
        return group;
    }

    private static readonly DirectoryNodeViewModel Dir = new(
        new Daedalus.VFS.VirtualNode<ModFileEntry>(
            "dir",
            Daedalus.VFS.NodeFlags.Directory,
            null,
            default
        )
    );

    [Fact]
    public void Mod_Before_Mod_At_Root_Is_Legal()
    {
        var dragged = Mod("A");
        var target = Mod("B");
        Assert.True(
            ModListDropRules.IsLegal(
                [dragged],
                target,
                null,
                DataGridRowDropPosition.Before,
                DragDropEffects.Move
            )
        );
    }

    [Fact]
    public void Mod_Inside_Group_Is_Legal()
    {
        var dragged = Mod("A");
        var target = Group("G");
        Assert.True(
            ModListDropRules.IsLegal(
                [dragged],
                target,
                null,
                DataGridRowDropPosition.Inside,
                DragDropEffects.Move
            )
        );
    }

    [Fact]
    public void Mod_Inside_Mod_Is_Illegal()
    {
        var dragged = Mod("A");
        var target = Mod("B");
        Assert.False(
            ModListDropRules.IsLegal(
                [dragged],
                target,
                null,
                DataGridRowDropPosition.Inside,
                DragDropEffects.Move
            )
        );
    }

    [Fact]
    public void Mod_Before_Mod_Child_Of_Group_Is_Legal()
    {
        var targetChild = Mod("B");
        var group = Group("G", targetChild);
        Assert.True(
            ModListDropRules.IsLegal(
                [Mod("A")],
                targetChild,
                group,
                DataGridRowDropPosition.After,
                DragDropEffects.Move
            )
        );
    }

    [Fact]
    public void Mod_Before_GroupChild_Without_Group_Parent_Is_Illegal_Inconsistent_State()
    {
        Assert.False(
            ModListDropRules.IsLegal(
                [Dir],
                Mod("B"),
                null,
                DataGridRowDropPosition.Before,
                DragDropEffects.Move
            )
        );
    }

    [Fact]
    public void Group_Inside_Group_Is_Illegal()
    {
        Assert.False(
            ModListDropRules.IsLegal(
                [Group("G1")],
                Group("G2"),
                null,
                DataGridRowDropPosition.Inside,
                DragDropEffects.Move
            )
        );
    }

    [Fact]
    public void Group_Before_GroupChild_Is_Illegal()
    {
        var groupChild = Mod("X");
        var parentGroup = Group("G2", groupChild);
        Assert.False(
            ModListDropRules.IsLegal(
                [Group("G1")],
                groupChild,
                parentGroup,
                DataGridRowDropPosition.Before,
                DragDropEffects.Move
            )
        );
    }

    [Fact]
    public void Group_Before_Root_Group_Is_Legal()
    {
        Assert.True(
            ModListDropRules.IsLegal(
                [Group("G1")],
                Group("G2"),
                null,
                DataGridRowDropPosition.Before,
                DragDropEffects.Move
            )
        );
    }

    [Fact]
    public void Directory_Is_Always_Illegal()
    {
        Assert.False(
            ModListDropRules.IsLegal(
                [Dir],
                Mod("B"),
                null,
                DataGridRowDropPosition.Before,
                DragDropEffects.Move
            )
        );
        Assert.False(
            ModListDropRules.IsLegal(
                [Dir],
                Group("G"),
                null,
                DataGridRowDropPosition.Inside,
                DragDropEffects.Move
            )
        );
    }

    [Fact]
    public void MoveIntoGroup_Moves_Mod_From_Root_Into_Group()
    {
        var dragged = Mod("A");
        var roots = new System.Collections.ObjectModel.ObservableCollection<TreeNodeViewModel>
        {
            Mod("B"),
            dragged,
            Group("G"),
        };
        var target = roots.OfType<GroupHeaderNodeViewModel>().Single();

        ModListDropRules.MoveIntoGroup(roots, target, [dragged]);

        Assert.Equal(new[] { "B", "G" }, roots.Select(node => node.DisplayName));
        Assert.Equal(new[] { "A" }, target.ObservableChildren.Select(node => node.DisplayName));
    }

    [Fact]
    public void MoveIntoGroup_From_Source_Group_To_Another()
    {
        var dragged = Mod("A");
        var source = Group("G1", dragged, Mod("B"));
        var target = Group("G2");
        var roots = new System.Collections.ObjectModel.ObservableCollection<TreeNodeViewModel>
        {
            source,
            target,
        };

        ModListDropRules.MoveIntoGroup(roots, target, [dragged]);

        Assert.Equal(new[] { "B" }, source.ObservableChildren.Select(node => node.DisplayName));
        Assert.Equal(new[] { "A" }, target.ObservableChildren.Select(node => node.DisplayName));
    }

    [Fact]
    public void MoveIntoGroup_Skips_NonMods()
    {
        var roots = new System.Collections.ObjectModel.ObservableCollection<TreeNodeViewModel>
        {
            Dir,
        };
        var target = Group("G");

        ModListDropRules.MoveIntoGroup(roots, target, [Dir]);

        Assert.Empty(target.ObservableChildren);
        Assert.Single(roots);
    }

    [Fact]
    public void Non_Move_Effect_Is_Illegal()
    {
        Assert.False(
            ModListDropRules.IsLegal(
                [Mod("A")],
                Group("G"),
                null,
                DataGridRowDropPosition.Inside,
                DragDropEffects.Copy
            )
        );
    }
}
