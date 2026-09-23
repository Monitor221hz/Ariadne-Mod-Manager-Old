using System.Collections.Generic;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.DataGridHierarchical;
using Avalonia.Controls.Selection;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Daedalus.ModManager.GUI.ViewModels;
using ReactiveUI;

namespace Daedalus.ModManager.GUI.Views;

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
        SyncSelectedNodes(viewModel);
    }

    private void SyncSelectedNodes(ModListViewModel viewModel)
    {
        var selection = ModsGrid.Selection;
        if (selection == null)
        {
            return;
        }
        var list = new List<TreeNodeViewModel>();
        foreach (var item in selection.SelectedItems)
        {
            var node = item switch
            {
                TreeNodeViewModel direct => direct,
                HierarchicalNode { Item: TreeNodeViewModel wrapped } => wrapped,
                _ => null,
            };
            if (node is not null)
            {
                list.Add(node);
            }
        }
        var selected = viewModel.SelectedNodes;
        var same = list.Count == selected.Count && list.All(selected.Contains);
        if (same)
        {
            return;
        }
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
        var items = NodeMenuItems.For(node);
        if (items.Length == 0)
        {
            return;
        }
        var menu = new ContextMenu();
        foreach (var item in items)
        {
            menu.Items.Add(new MenuItem { Header = item.Header, Command = item.Command });
        }
        e.Handled = true;
        menu.Open(anchor);
    }
}
