using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia.Controls.ApplicationLifetimes;
using Daedalus.Contracts.Games;
using Daedalus.Contracts.ModManager;
using Daedalus.Contracts.Mods;
using ReactiveUI;

namespace Daedalus.ModManager.GUI.ViewModels;

public class MainViewModel : ViewModelBase
{
    private readonly IModProfileSerializer? _profileSerializer;
    private readonly IModInfoSerializer? _modSerializer;
    private readonly IModManagerPaths? _paths;
    private readonly IInstanceService? _instances;
    private readonly IGameCatalog? _catalog;
    private readonly IGameLocator? _locator;

    private object? _currentViewModel;
    private bool _deletePromptVisible;
    private string? _pendingDeleteName;

    public object? CurrentViewModel
    {
        get => _currentViewModel;
        private set => this.RaiseAndSetIfChanged(ref _currentViewModel, value);
    }

    public ObservableCollection<InstanceEntryViewModel> Instances { get; } = [];
    public ObservableCollection<ThemeEntryViewModel> Themes { get; } = [];

    public bool DeletePromptVisible
    {
        get => _deletePromptVisible;
        private set => this.RaiseAndSetIfChanged(ref _deletePromptVisible, value);
    }

    public string? PendingDeleteName
    {
        get => _pendingDeleteName;
        private set => this.RaiseAndSetIfChanged(ref _pendingDeleteName, value);
    }

    public ReactiveCommand<Unit, Unit> InitializeCommand { get; }
    public ReactiveCommand<Unit, Unit> ExitCommand { get; }
    public ReactiveCommand<string, Unit> SwitchInstanceCommand { get; }
    public ReactiveCommand<string, Unit> AskDeleteCommand { get; }
    public ReactiveCommand<Unit, Unit> CancelDeleteCommand { get; }
    public ReactiveCommand<Unit, Unit> RemoveRegistryOnlyCommand { get; }
    public ReactiveCommand<Unit, Unit> RemoveAndDeleteCommand { get; }

    // Design-time ctor.
    public MainViewModel()
    {
        InitializeCommand = ReactiveCommand.Create(() => { });
        ExitCommand = ReactiveCommand.Create(Quit);
        SwitchInstanceCommand = ReactiveCommand.Create<string>(_ => { });
        AskDeleteCommand = ReactiveCommand.Create<string>(_ => { });
        CancelDeleteCommand = ReactiveCommand.Create(() => { });
        RemoveRegistryOnlyCommand = ReactiveCommand.Create(() => { });
        RemoveAndDeleteCommand = ReactiveCommand.Create(() => { });
    }

    public MainViewModel(
        IModProfileSerializer profileSerializer,
        IModInfoSerializer modSerializer,
        IModManagerPaths paths,
        IInstanceService instances,
        IGameCatalog catalog,
        IGameLocator locator
    )
    {
        _profileSerializer = profileSerializer;
        _modSerializer = modSerializer;
        _paths = paths;
        _instances = instances;
        _catalog = catalog;
        _locator = locator;

        foreach (var theme in AppTheme.All)
        {
            var entry = new ThemeEntryViewModel(theme, theme.Id == AppTheme.Current.Id);
            entry
                .WhenAnyValue(x => x.IsChecked)
                .Where(isChecked => isChecked)
                .Subscribe(_ => AppTheme.Apply(entry.Id));
            Themes.Add(entry);
        }

        InitializeCommand = ReactiveCommand.CreateFromTask(InitializeAsync);
        ExitCommand = ReactiveCommand.Create(Quit);
        SwitchInstanceCommand = ReactiveCommand.Create<string>(SwitchInstance);
        AskDeleteCommand = ReactiveCommand.Create<string>(name =>
        {
            PendingDeleteName = name;
            DeletePromptVisible = true;
        });
        CancelDeleteCommand = ReactiveCommand.Create(() =>
        {
            PendingDeleteName = null;
            DeletePromptVisible = false;
        });
        RemoveRegistryOnlyCommand = ReactiveCommand.CreateFromTask(_ => RemoveInstanceAsync(false));
        RemoveAndDeleteCommand = ReactiveCommand.CreateFromTask(_ => RemoveInstanceAsync(true));
    }

    private Task InitializeAsync()
    {
        RefreshInstances();
        if (_instances!.CurrentFolder is null)
        {
            ShowGameSelection();
        }
        else
        {
            ShowWorkspace();
        }
        return Task.CompletedTask;
    }

    private void ShowGameSelection()
    {
        var viewModel = new GameSelectionViewModel(_catalog!, _locator!);
        CurrentViewModel = viewModel;
        viewModel
            .WhenAnyValue(x => x.ConfirmedGame)
            .WhereNotNull()
            .Take(1)
            .Subscribe(ShowInstanceSetup);
    }

    private void ShowInstanceSetup(IInstalledGame game)
    {
        var viewModel = new InstanceSetupViewModel(_instances!, _paths!, game);
        CurrentViewModel = viewModel;
        viewModel
            .WhenAnyValue(x => x.CompletedName)
            .WhereNotNull()
            .Take(1)
            .Subscribe(_ =>
            {
                RefreshInstances();
                ShowWorkspace();
            });
    }

    private void ShowWorkspace()
    {
        var viewModel = new ModListViewModel(
            _profileSerializer!,
            _modSerializer!,
            _paths!,
            _instances!
        );
        CurrentViewModel = viewModel;
        viewModel.InitializeCommand.Execute().Subscribe();
    }

    private void RefreshInstances()
    {
        Instances.Clear();
        if (_instances is null)
        {
            return;
        }
        foreach (
            var (name, folder) in _instances.Instances.OrderBy(
                kv => kv.Key,
                StringComparer.OrdinalIgnoreCase
            )
        )
        {
            Instances.Add(
                new InstanceEntryViewModel(name, folder.FullName, name == _instances.CurrentName)
            );
        }
    }

    private void SwitchInstance(string name)
    {
        if (_instances is null || name == _instances.CurrentName)
        {
            return;
        }
        _instances.Switch(name);
        RestartApplication();
    }

    private async Task RemoveInstanceAsync(bool deleteFolder)
    {
        if (PendingDeleteName is null || _instances is null)
        {
            return;
        }
        await Task.Run(() => _instances.Remove(PendingDeleteName, deleteFolder));
        PendingDeleteName = null;
        DeletePromptVisible = false;
        await InitializeAsync();
    }

    private static void Quit()
    {
        if (
            Avalonia.Application.Current?.ApplicationLifetime
            is IClassicDesktopStyleApplicationLifetime desktop
        )
        {
            desktop.Shutdown();
        }
    }

    private static void RestartApplication()
    {
        Process.Start(Environment.ProcessPath!);
        Quit();
    }
}
