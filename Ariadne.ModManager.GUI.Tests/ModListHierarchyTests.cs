using Avalonia.Controls.DataGridHierarchical;
using Ariadne.Contracts.ModManager;
using Ariadne.ModManager;
using Ariadne.ModManager.GUI.ViewModels;
using Xunit;

namespace Ariadne.ModManager.GUI.Tests;

public class ModListHierarchyTests
{
    private sealed class FakeMod(string name) : ILibraryMod
    {
        public IModInfo Info { get; } =
            new ModManager.ModInfo(0, SourceType.Local, "1.0", [], "", 0, false);
        public string Name { get; } = name;
        public DirectoryInfo Directory => new(".");
        public Ariadne.VFS.VirtualNode<ModFileEntry> Content { get; } =
            new("", Ariadne.VFS.NodeFlags.Directory, null, default);

        public void RefreshContent() { }

        public void RenameTo(string newName) { }

        public bool Equals(ILibraryMod? x, ILibraryMod? y) => ReferenceEquals(x, y);

        public int GetHashCode(ILibraryMod obj) => obj.GetHashCode();

        public bool Equals(ILibraryMod? other) => ReferenceEquals(this, other);
    }

    private static (
        HierarchicalModel<TreeNodeViewModel> Model,
        GroupHeaderNodeViewModel Group
    ) CreateModel(params string[] looseNames)
    {
        var loose = looseNames
            .Select(name => (TreeNodeViewModel)new ModEntryNodeViewModel(new FakeMod(name)))
            .ToList();
        var group = new GroupHeaderNodeViewModel(new ModGroup("G", []));
        var roots = new System.Collections.ObjectModel.ObservableCollection<TreeNodeViewModel>(
            loose.Concat([group])
        );
        var model = new HierarchicalModel<TreeNodeViewModel>(
            new HierarchicalOptions<TreeNodeViewModel>
            {
                ChildrenSelector = node => node.Children,
                VirtualizeChildren = true,
                ExpandedStateKeyMode = ExpandedStateKeyMode.Item,
                IsExpandedSelector = node => node.IsExpanded,
                IsExpandedSetter = (node, expanded) => node.IsExpanded = expanded,
            }
        );
        model.SetRoots(roots);
        return (model, group);
    }

    private static string[] FlattenedNames(HierarchicalModel<TreeNodeViewModel> model) =>
        model.Flattened.Select(node => ((TreeNodeViewModel)node.Item).DisplayName).ToArray();

    [Fact]
    public void Expand_Then_Collapse_Empty_Group_Ratchets_Until_Child_Added()
    {
        var (model, group) = CreateModel();
        var node = model.Flattened.Single(n => ReferenceEquals(n.Item, group));

        model.Expand(node);
        Assert.True(node.IsExpanded);

        model.Collapse(node);
        Assert.True(node.IsExpanded);

        group.ObservableChildren.Add(new ModEntryNodeViewModel(new FakeMod("X")));

        var expandedNode = model.Flattened.Single(n => ReferenceEquals(n.Item, group));
        model.Collapse(expandedNode);
        Assert.False(expandedNode.IsExpanded);
    }

    [Fact]
    public void Natural_Order_Follows_Priority_Insertion_Order()
    {
        var (model, _) = CreateModel("B", "A", "C");
        Assert.Equal(new[] { "B", "A", "C", "G" }, FlattenedNames(model));
    }

    [Fact]
    public void Sibling_Comparer_Sorts_Loose_Siblings_Ascending()
    {
        var (model, _) = CreateModel("B", "A", "C");
        model.ApplySiblingComparer(
            Comparer<TreeNodeViewModel>.Create(
                (left, right) =>
                    string.Compare(left.DisplayName, right.DisplayName, StringComparison.Ordinal)
            ),
            recursive: true
        );

        Assert.Equal(new[] { "A", "B", "C", "G" }, FlattenedNames(model));
    }

    [Fact]
    public void Clearing_Comparer_Then_Refresh_Restores_Natural_Order()
    {
        var (model, _) = CreateModel("B", "A", "C");
        model.ApplySiblingComparer(
            Comparer<TreeNodeViewModel>.Create(
                (left, right) =>
                    string.Compare(left.DisplayName, right.DisplayName, StringComparison.Ordinal)
            ),
            recursive: true
        );

        model.ApplySiblingComparer(null, recursive: true);
        model.Refresh();

        Assert.Equal(new[] { "B", "A", "C", "G" }, FlattenedNames(model));
    }

    [Fact]
    public void Group_Expanded_State_Persists_Across_Sort()
    {
        var (model, group) = CreateModel("B", "A");
        group.IsExpanded = true;
        model.ApplySiblingComparer(
            Comparer<TreeNodeViewModel>.Create(
                (left, right) =>
                    string.Compare(left.DisplayName, right.DisplayName, StringComparison.Ordinal)
            ),
            recursive: true
        );

        Assert.Equal(new[] { "A", "B", "G" }, FlattenedNames(model));
    }

    [Fact]
    public void Sort_Clear_Refresh_Preserves_Expansion_Via_Selector()
    {
        var (model, group) = CreateModel("B", "A");
        group.IsExpanded = true;
        group.ObservableChildren.Add(new ModEntryNodeViewModel(new FakeMod("Child")));
        model.ApplySiblingComparer(
            Comparer<TreeNodeViewModel>.Create(
                (left, right) =>
                    string.Compare(left.DisplayName, right.DisplayName, StringComparison.Ordinal)
            ),
            recursive: true
        );

        model.ApplySiblingComparer(null, recursive: true);
        model.Refresh();

        Assert.Equal(new[] { "B", "A", "G", "Child" }, FlattenedNames(model));
    }
}
