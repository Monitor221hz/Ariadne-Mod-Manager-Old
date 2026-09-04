using System;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Daedalus.ModManager.GUI.ViewModels;
using ReactiveUI;

namespace Daedalus.ModManager.GUI.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        this.GetObservable(DataContextProperty)
            .Select(dataContext => dataContext as MainViewModel)
            .WhereNotNull()
            .Take(1)
            .Subscribe(Attach);
    }

    private void Attach(MainViewModel viewModel)
    {
        viewModel.ConfirmDeleteInstance.RegisterHandler(HandleDeletePrompt);
        RebuildInstanceMenus(viewModel);
        viewModel.Instances.CollectionChanged += (_, _) => RebuildInstanceMenus(viewModel);

        ThemeItem.ItemsSource = viewModel
            .Themes.Select(entry =>
            {
                var item = new MenuItem
                {
                    Header = entry.Display,
                    ToggleType = MenuItemToggleType.Radio,
                    GroupName = "AppTheme",
                    DataContext = entry,
                };
                item.Bind(
                    MenuItem.IsCheckedProperty,
                    new Avalonia.Data.Binding(nameof(ThemeEntryViewModel.IsChecked))
                    {
                        Mode = Avalonia.Data.BindingMode.TwoWay,
                    }
                );
                return item;
            })
            .ToList();
    }

    private async Task HandleDeletePrompt(IInteractionContext<string, InstanceDeleteChoice> context)
    {
        DeletePromptText.Text = $"Remove instance: {context.Input} ?";
        DeleteOverlay.IsVisible = true;
        var choice = await Observable
            .Merge(
                ClickOf(RegistryOnlyButton).Select(_ => InstanceDeleteChoice.RegistryOnly),
                ClickOf(DeleteFolderButton).Select(_ => InstanceDeleteChoice.DeleteFolder),
                ClickOf(DeleteCancelButton).Select(_ => InstanceDeleteChoice.Cancel)
            )
            .FirstAsync()
            .ToTask();
        DeleteOverlay.IsVisible = false;
        context.SetOutput(choice);
    }

    private static IObservable<EventPattern<RoutedEventArgs>> ClickOf(Button button) =>
        Observable.FromEventPattern<RoutedEventArgs>(
            handler => button.Click += handler,
            handler => button.Click -= handler
        );

    private void RebuildInstanceMenus(MainViewModel viewModel)
    {
        SwitchInstanceItem.ItemsSource = viewModel
            .Instances.Select(instance => new MenuItem
            {
                Header = instance.DisplayHeader,
                Command = viewModel.SwitchInstanceCommand,
                CommandParameter = instance.Name,
                IsEnabled = !instance.IsCurrent,
            })
            .ToList();
        RemoveInstanceItem.ItemsSource = viewModel
            .Instances.Select(instance => new MenuItem
            {
                Header = instance.RemoveHeader,
                Command = viewModel.AskDeleteCommand,
                CommandParameter = instance.Name,
            })
            .ToList();
    }
}
