using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Selection;
using Avalonia.Reactive;
using Avalonia.Threading;
using Daedalus.ModManager.GUI.ViewModels;
using ReactiveUI;
using ReactiveUI.Avalonia;

namespace Daedalus.ModManager.GUI.Views;

public partial class LoadOrderView : UserControl
{
    public LoadOrderView()
    {
        InitializeComponent();
        this.GetObservable(DataContextProperty)
            .Select(dc => dc as LoadOrderViewModel)
            .WhereNotNull()
            .Take(1)
            .Subscribe(vm => vm.SelectionRequested += rows => ApplySelection(vm, rows));
    }

    private void ApplySelection(
        LoadOrderViewModel viewModel,
        IReadOnlyList<LoadOrderInfoViewModel> rows
    )
    {
        var selection = LoadOrderGrid.Selection;
        using var batch = selection.BatchUpdate();
        selection.Clear();
        foreach (var row in rows)
        {
            var index = viewModel.LoadOrder.IndexOf(row);
            if (index >= 0)
            {
                selection.Select(index);
            }
        }
    }

    private void LoadOrderGrid_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not LoadOrderViewModel viewModel)
        {
            return;
        }
        SyncSelectedNodes(
            viewModel,
            e.RemovedItems.OfType<LoadOrderInfoViewModel>().ToList(),
            e.AddedItems.OfType<LoadOrderInfoViewModel>().ToList()
        );
    }

    private void SyncSelectedNodes(
        LoadOrderViewModel viewModel,
        List<LoadOrderInfoViewModel> removed,
        List<LoadOrderInfoViewModel> added
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
}
