using System.Collections.Generic;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using Ariadne.ModManager.GUI.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.DataGridHierarchical;
using Avalonia.Controls.Selection;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ReactiveUI;

namespace Ariadne.ModManager.GUI.Views;

public partial class ModListView : UserControl
{
    public ModListView()
    {
        InitializeComponent();
        this.GetObservable(DataContextProperty)
            .Select(dataContext => dataContext as ModListViewModel)
            .WhereNotNull()
            .Take(1)
            .Subscribe(viewModel =>
            {
                viewModel.ConfirmRemoveMod.RegisterHandler(HandleRemovePrompt);
                viewModel.SelectionRequested += rows => ApplySelection(viewModel, rows);
            });
    }

    private void ApplySelection(ModListViewModel viewModel, IReadOnlyList<TreeNodeViewModel> rows)
    {
        var model = viewModel.Model;
        var selection = ModsGrid.Selection;
        using var batch = selection.BatchUpdate();
        selection.Clear();
        if (model is null)
        {
            return;
        }
        var indexByItem = new Dictionary<TreeNodeViewModel, int>();
        var flatIndex = 0;
        foreach (var node in model.Flattened)
        {
            if (node.Item is TreeNodeViewModel row)
            {
                indexByItem[row] = flatIndex;
            }
            flatIndex++;
        }
        foreach (var row in rows)
        {
            if (indexByItem.TryGetValue(row, out var index))
            {
                selection.Select(index);
            }
        }
    }

    private async Task HandleRemovePrompt(IInteractionContext<ModEntryNodeViewModel, bool> context)
    {
        RemovePromptText.Text = $"Remove mod: {context.Input.DisplayName} ?";
        RemoveOverlay.IsVisible = true;
        var confirmed = await Observable
            .Merge(
                ButtonObservables.ClicksOf(DeleteModButton).Select(_ => true),
                ButtonObservables.ClicksOf(RemoveModCancelButton).Select(_ => false)
            )
            .FirstAsync()
            .ToTask();
        RemoveOverlay.IsVisible = false;
        context.SetOutput(confirmed);
    }

    private void ModsGrid_SelectionChanged(
        object? sender,
        Avalonia.Controls.SelectionChangedEventArgs e
    )
    {
        if (DataContext is not ModListViewModel viewModel)
        {
            return;
        }
        SyncSelectedNodes(viewModel, UnwrapRows(e.RemovedItems), UnwrapRows(e.AddedItems));
    }

    private static List<TreeNodeViewModel> UnwrapRows(System.Collections.IList items)
    {
        var rows = new List<TreeNodeViewModel>(items.Count);
        foreach (var item in items)
        {
            var row = item switch
            {
                TreeNodeViewModel direct => direct,
                HierarchicalNode { Item: TreeNodeViewModel wrapped } => wrapped,
                _ => null,
            };
            if (row is not null)
            {
                rows.Add(row);
            }
        }
        return rows;
    }

    private void SyncSelectedNodes(
        ModListViewModel viewModel,
        List<TreeNodeViewModel> removed,
        List<TreeNodeViewModel> added
    )
    {
        var list = viewModel.SelectedNodes.Except(removed).Concat(added).Distinct().ToList();
        var same =
            list.Count == viewModel.SelectedNodes.Count
            && list.All(viewModel.SelectedNodes.Contains);
        if (same)
        {
            return;
        }
        var selected = viewModel.SelectedNodes;
        selected.Clear();
        if (list.Count > 0)
        {
            selected.AddRange(list);
        }
    }

    private void ModsGrid_ContextRequested(object? sender, ContextRequestedEventArgs e)
    {
        var anchor = (e.Source as Control)
            ?.GetSelfAndVisualAncestors()
            .OfType<Control>()
            .FirstOrDefault(control =>
                control.DataContext is TreeNodeViewModel
                || control.DataContext is HierarchicalNode { Item: TreeNodeViewModel }
            );
        if (anchor is null)
        {
            return;
        }
        var node = anchor.DataContext switch
        {
            TreeNodeViewModel direct => direct,
            HierarchicalNode { Item: TreeNodeViewModel wrapped } => wrapped,
            _ => null,
        };
        if (node is null)
        {
            return;
        }
        var items = NodeMenuItems.For(node, (DataContext as ModListViewModel)?.InstallTargets);
        if (items.Length == 0)
        {
            return;
        }
        var menu = new ContextMenu();
        foreach (var item in items)
        {
            menu.Items.Add(ToMenuItem(item));
        }
        e.Handled = true;
        menu.Open(anchor);
    }

    private static MenuItem ToMenuItem(NodeMenuItem item)
    {
        var menuItem = new MenuItem
        {
            Header = item.Header,
            Command = item.Command,
            CommandParameter = item.CommandParameter,
        };
        ToolTip.SetTip(menuItem, item.Tooltip);
        if (item.Children is { Count: > 0 } children)
        {
            menuItem.ItemsSource = children.Select(ToMenuItem).ToList();
        }
        return menuItem;
    }
}
