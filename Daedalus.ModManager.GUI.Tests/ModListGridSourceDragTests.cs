using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Controls.Models;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Daedalus.Contracts.Mods;
using Daedalus.ModManager.GUI.ViewModels;
using Daedalus.Mods;
using Xunit;

namespace Daedalus.ModManager.GUI.Tests;

public class ModListGridSourceDragTests
{
    private sealed class FakeMod(string name) : ILibraryMod
    {
        public IModInfo Info { get; } = new Mods.ModInfo(0, SourceType.Local, "1.0", [], "", 0);
        public string Name { get; } = name;
        public DirectoryInfo Directory => new(".");
        public Daedalus.VFS.VirtualNode<ModFileEntry> Content { get; } =
            new("", Daedalus.VFS.NodeFlags.Directory, null, default);

        public void RefreshContent() { }

        public void RenameTo(string newName) { }
    }

    private sealed record Grid(
        ModListGridSource Source,
        System.Collections.ObjectModel.ObservableCollection<TreeNodeViewModel> Roots
    );

    private static Grid NewGrid(
        List<ILibraryMod> loose,
        Dictionary<string, List<ILibraryMod>> groups
    )
    {
        var roots = new System.Collections.ObjectModel.ObservableCollection<TreeNodeViewModel>(
            loose
                .Select(mod => (TreeNodeViewModel)new ModEntryNodeViewModel((ILibraryMod)mod))
                .Concat(
                    groups.Select(kv =>
                        (TreeNodeViewModel)
                            new GroupHeaderNodeViewModel(new ModGroup(kv.Key, kv.Value))
                    )
                )
        );

        var inner = new HierarchicalTreeDataGridSource<TreeNodeViewModel>(roots)
        {
            Columns =
            {
                new HierarchicalExpanderColumn<TreeNodeViewModel>(
                    new TemplateColumn<TreeNodeViewModel>(
                        "Name",
                        new FuncDataTemplate<TreeNodeViewModel>((_, _) => new TextBlock())
                    ),
                    node => node.Children,
                    node => node.HasChildren
                ),
                new TextColumn<TreeNodeViewModel, string>("Name", node => node.DisplayName),
            },
        };

        return new Grid(new ModListGridSource(inner), roots);
    }

    private static void Drag(
        ModListGridSource source,
        IndexPath from,
        IndexPath target,
        TreeDataGridRowDropPosition position
    ) =>
        ((ITreeDataGridSource)source).DragDropRows(
            source,
            [from],
            target,
            position,
            DragDropEffects.Move
        );

    private static void AssertFlatOrder(
        System.Collections.ObjectModel.ObservableCollection<TreeNodeViewModel> roots,
        params string[] expected
    )
    {
        var actual = roots
            .SelectMany(root =>
                root switch
                {
                    GroupHeaderNodeViewModel group => group
                        .ObservableChildren.OfType<ModEntryNodeViewModel>()
                        .Select(m => m.Model.Name),
                    ModEntryNodeViewModel mod => new[] { mod.Model.Name },
                    _ => Enumerable.Empty<string>(),
                }
            )
            .ToArray();
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Drag_FromGroupIntoLooseBefore_ProducesFlatUniques()
    {
        var m1 = new FakeMod("A");
        var m2 = new FakeMod("B");
        var m3 = new FakeMod("C");
        var grid = NewGrid([m1, m2], new() { ["G"] = [m3] });

        Drag(grid.Source, new IndexPath(2, 0), new IndexPath(0), TreeDataGridRowDropPosition.After);

        AssertFlatOrder(grid.Roots, "A", "C", "B");
    }

    [Fact]
    public void Drag_GroupInsideGroup_IsRejected()
    {
        var m1 = new FakeMod("A");
        var m2 = new FakeMod("B");
        var grid = NewGrid([m1], new() { ["G1"] = [m2], ["G2"] = [] });

        Drag(grid.Source, new IndexPath(1), new IndexPath(2), TreeDataGridRowDropPosition.Inside);

        AssertFlatOrder(grid.Roots, "A", "B");
        Assert.Empty(
            grid.Roots.OfType<GroupHeaderNodeViewModel>()
                .Single(g => g.Group.Name == "G2")
                .ObservableChildren
        );
    }

    private static string[] PresentedNames(ModListGridSource source) =>
        source.Rows.Select(row => ((TreeNodeViewModel)row.Model!).DisplayName).ToArray();

    [Fact]
    public void Sort_Then_ClearSort_Restores_Natural_Order()
    {
        var b = new FakeMod("B");
        var a = new FakeMod("A");
        var z = new FakeMod("Z");
        var grid = NewGrid([b, a], new() { ["G"] = [z] });

        Assert.True(grid.Source.SortBy(grid.Source.Columns[1], ListSortDirection.Ascending));
        Assert.True(grid.Source.IsSorted);
        Assert.Equal(new[] { "A", "B", "G" }, PresentedNames(grid.Source));

        grid.Source.ClearSort();
        Assert.False(grid.Source.IsSorted);
        Assert.Equal(new[] { "B", "A", "G" }, PresentedNames(grid.Source));
    }

    [Fact]
    public void Drag_Is_Rejected_While_Sorted_And_Works_After_ClearSort()
    {
        var m1 = new FakeMod("A");
        var m2 = new FakeMod("B");
        var grid = NewGrid([m1, m2], new());
        grid.Source.SortBy(grid.Source.Columns[1], ListSortDirection.Ascending);

        Drag(grid.Source, new IndexPath(0), new IndexPath(1), TreeDataGridRowDropPosition.After);
        AssertFlatOrder(grid.Roots, "A", "B");

        grid.Source.ClearSort();
        Drag(grid.Source, new IndexPath(0), new IndexPath(1), TreeDataGridRowDropPosition.After);
        AssertFlatOrder(grid.Roots, "B", "A");
    }

    [Fact]
    public void Drag_ModInsideGroup_Works()
    {
        var m1 = new FakeMod("A");
        var m2 = new FakeMod("B");
        var grid = NewGrid([m1], new() { ["G"] = [m2] });

        Drag(grid.Source, new IndexPath(0), new IndexPath(1), TreeDataGridRowDropPosition.Inside);

        AssertFlatOrder(grid.Roots, "B", "A");
        Assert.Empty(grid.Roots.OfType<ModEntryNodeViewModel>());
    }
}
