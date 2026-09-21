using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.DataGridHierarchical;
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
            .Subscribe(viewModel => viewModel.ConfirmRemoveMod.RegisterHandler(HandleRemovePrompt));
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
        Dispatcher.UIThread.Post(() => SyncSelectedNodes(viewModel), DispatcherPriority.Loaded + 1);
    }

    private void SyncSelectedNodes(ModListViewModel viewModel)
    {
        var selection = ModsGrid.Selection;
        if (selection == null)
        {
            return;
        }
        viewModel.SelectedNodes.Clear();
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
                viewModel.SelectedNodes.Add(node);
            }
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
