using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using Ariadne.Contracts.ModManager;
using Ariadne.ModManager.GUI.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
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
        viewModel.AskProfileName.RegisterHandler(HandleProfileNamePrompt);

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

    private async Task HandleProfileNamePrompt(IInteractionContext<Unit, string?> context)
    {
        ProfileNameBox.Text = "";
        ProfileOverlay.IsVisible = true;
        ProfileNameBox.Focus();
        using var filter = ProfileNameBox
            .GetObservable(TextBox.TextProperty)
            .WhereNotNull()
            .Subscribe(text =>
            {
                var filtered = PathName.Filter(text);
                if (filtered != text)
                {
                    ProfileNameBox.Text = filtered;
                }
            });
        var result = await Observable
            .Merge(
                ButtonObservables.ClicksOf(ProfileCreateButton).Select(_ => ProfileNameBox.Text),
                ButtonObservables.ClicksOf(ProfileCancelButton).Select(_ => (string?)null)
            )
            .FirstAsync()
            .ToTask();
        ProfileOverlay.IsVisible = false;
        context.SetOutput(string.IsNullOrWhiteSpace(result) ? null : result);
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
}
