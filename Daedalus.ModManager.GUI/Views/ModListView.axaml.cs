using System;
using System.Globalization;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Daedalus.ModManager.GUI.Converters;
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
                ClickOf(DeleteModButton).Select(_ => true),
                ClickOf(RemoveModCancelButton).Select(_ => false)
            )
            .FirstAsync()
            .ToTask();
        RemoveOverlay.IsVisible = false;
        context.SetOutput(confirmed);
    }

    private static IObservable<EventPattern<Avalonia.Interactivity.RoutedEventArgs>> ClickOf(
        Button button
    ) =>
        Observable.FromEventPattern<Avalonia.Interactivity.RoutedEventArgs>(
            handler => button.Click += handler,
            handler => button.Click -= handler
        );

    private static readonly NodeMenuItemsConverter MenuItems = new();

    private void TreeDataGrid_ContextRequested(object? sender, ContextRequestedEventArgs e)
    {
        var anchor = (e.Source as Control)
            ?.GetSelfAndVisualAncestors()
            .OfType<Control>()
            .FirstOrDefault(control => control.DataContext is TreeNodeViewModel);
        if (anchor is null || anchor.DataContext is not TreeNodeViewModel node)
        {
            return;
        }
        if (
            MenuItems.Convert(node, typeof(NodeMenuItem[]), null, CultureInfo.InvariantCulture)
            is not IEnumerable<NodeMenuItem> items
        )
        {
            return;
        }
        var menu = new ContextMenu();
        foreach (var item in items)
        {
            menu.Items.Add(new MenuItem { Header = item.Header, Command = item.Command });
        }
        if (menu.Items.Count == 0)
        {
            return;
        }
        e.Handled = true;
        menu.Open(anchor);
    }
}
