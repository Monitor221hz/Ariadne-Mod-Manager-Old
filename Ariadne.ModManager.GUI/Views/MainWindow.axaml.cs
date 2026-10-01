using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using Ariadne.ModManager.GUI.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ReactiveUI;

namespace Ariadne.ModManager.GUI.Views;

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
        viewModel.ShowInfo.RegisterHandler(HandleInfoPrompt);
        RebuildInstanceMenus(viewModel);
        viewModel.Instances.CollectionChanged += (_, _) => RebuildInstanceMenus(viewModel);

        if (viewModel.Sources is { } sources)
        {
            sources.ShowInfo.RegisterHandler(HandleInfoPrompt);
            SourcesItem.SubmenuOpened += (_, _) => sources.RefreshCommand.Execute().Subscribe();
        }

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
                ButtonObservables
                    .ClicksOf(RegistryOnlyButton)
                    .Select(_ => InstanceDeleteChoice.RegistryOnly),
                ButtonObservables
                    .ClicksOf(DeleteFolderButton)
                    .Select(_ => InstanceDeleteChoice.DeleteFolder),
                ButtonObservables
                    .ClicksOf(DeleteCancelButton)
                    .Select(_ => InstanceDeleteChoice.Cancel)
            )
            .FirstAsync()
            .ToTask();
        DeleteOverlay.IsVisible = false;
        context.SetOutput(choice);
    }

    private async Task HandleInfoPrompt(
        IInteractionContext<(string Title, string Text), Unit> context
    )
    {
        InfoTitleText.Text = context.Input.Title;
        InfoContentText.Text = context.Input.Text;
        InfoOverlay.IsVisible = true;
        await ButtonObservables.ClicksOf(InfoOkButton).FirstAsync().ToTask();
        InfoOverlay.IsVisible = false;
        context.SetOutput(Unit.Default);
    }

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
