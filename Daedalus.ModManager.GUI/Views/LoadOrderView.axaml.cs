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
        SyncSelectedNodes(viewModel);
    }

    private void SyncSelectedNodes(LoadOrderViewModel viewModel)
    {
        var selection = LoadOrderGrid.Selection;
        if (selection == null)
        {
            return;
        }
        var list = selection.SelectedItems.OfType<LoadOrderInfoViewModel>().ToList();
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
}
