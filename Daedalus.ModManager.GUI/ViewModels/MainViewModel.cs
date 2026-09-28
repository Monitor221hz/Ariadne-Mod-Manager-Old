using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Reactive;
using System.Reactive.Linq;
using Avalonia.Controls.ApplicationLifetimes;
using Daedalus.Contracts.Games;
using Daedalus.Contracts.ModManager;
using Daedalus.Downloads;
using Daedalus.ModManager.Serialization;
using ReactiveUI;

namespace Daedalus.ModManager.GUI.ViewModels;

public class MainViewModel : ViewModelBase
{
    private readonly IModProfileSerializer? _profileSerializer;
    private readonly ILibraryModSerializer? _modSerializer;
    private readonly IModManagerPaths? _paths;
    private readonly IInstanceService? _instances;
    private readonly IModProfileEditor? _editor;
    private readonly IGameCatalog? _catalog;
    private readonly IGameLocator? _locator;
    private readonly ILoadOrderBuilder? _loadOrderBuilder;
    private readonly ILibraryModFactory? _modFactory;
    private readonly IDownloadQueue? _downloads;
    private readonly IModInstallService? _installService;

    private object? _currentViewModel;
    private ProfileViewModel? _activeProfile;
    private string? _selectedProfileName;
    private string? _currentGameText;
    private bool _suppressProfileSwitch;

    public object? CurrentViewModel
    {
        get => _currentViewModel;
        private set
        {
            if (Equals(_currentViewModel, value))
            {
                return;
            }
            (_currentViewModel as IDisposable)?.Dispose();
            this.RaiseAndSetIfChanged(ref _currentViewModel, value);
            this.RaisePropertyChanged(nameof(WorkspaceVisible));
        }
    }

    public ProfileViewModel? ActiveProfile
    {
        get => _activeProfile;
        private set
        {
            this.RaiseAndSetIfChanged(ref _activeProfile, value);
            this.RaisePropertyChanged(nameof(WorkspaceVisible));
        }
    }

    public bool WorkspaceVisible =>
        ActiveProfile is not null && CurrentViewModel is WorkspaceViewModel;

    public ObservableCollection<string> ProfileNames { get; } = [];

    public string? SelectedProfileName
    {
        get => _selectedProfileName;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedProfileName, value);
            if (value is not null && !_suppressProfileSwitch && value != ActiveProfile?.Name)
            {
                LoadProfile(value);
            }
        }
    }

    public string? CurrentGameText
    {
        get => _currentGameText;
        private set => this.RaiseAndSetIfChanged(ref _currentGameText, value);
    }

    public ObservableCollection<InstanceEntryViewModel> Instances { get; } = [];
    public ObservableCollection<ThemeEntryViewModel> Themes { get; } = [];

    public Interaction<string, InstanceDeleteChoice> ConfirmDeleteInstance { get; } = new();

    public ReactiveCommand<Unit, Unit> InitializeCommand { get; }
    public ReactiveCommand<Unit, Unit> ExitCommand { get; }
    public ReactiveCommand<string, Unit> SwitchInstanceCommand { get; }
    public ReactiveCommand<string, Unit> AskDeleteCommand { get; }
    public ReactiveCommand<Unit, Unit> CreateModCommand { get; }
    public ReactiveCommand<Unit, Unit> CreateGroupCommand { get; }

    private ModListViewModel? ActiveModList => (CurrentViewModel as WorkspaceViewModel)?.ModList;

    public MainViewModel()
    {
        InitializeCommand = ReactiveCommand.Create(() => { });
        ExitCommand = ReactiveCommand.Create(Quit);
        SwitchInstanceCommand = ReactiveCommand.Create<string>(_ => { });
        AskDeleteCommand = ReactiveCommand.Create<string>(_ => { });
        CreateModCommand = ReactiveCommand.Create(() => { });
        CreateGroupCommand = ReactiveCommand.Create(() => { });
    }

    public MainViewModel(
        IModProfileSerializer profileSerializer,
        ILibraryModSerializer modSerializer,
        IModManagerPaths paths,
        IInstanceService instances,
        IModProfileEditor editor,
        IGameCatalog catalog,
        IGameLocator locator,
        ILoadOrderBuilder loadOrderBuilder,
        SourcesMenuViewModel? sources = null,
        IDownloadQueue? downloads = null,
        ILibraryModFactory? modFactory = null,
        IModInstallService? installService = null
    )
    {
        _profileSerializer = profileSerializer;
        _modSerializer = modSerializer;
        _paths = paths;
        _instances = instances;
        _editor = editor;
        _catalog = catalog;
        _locator = locator;
        _loadOrderBuilder = loadOrderBuilder;
        _downloads = downloads;
        _modFactory = modFactory;
        _installService = installService;

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
        InitializeCommand.ThrownExceptions.Subscribe(ex => Debug.WriteLine(ex));
        ExitCommand = ReactiveCommand.Create(Quit);
        SwitchInstanceCommand = ReactiveCommand.Create<string>(SwitchInstance);
        AskDeleteCommand = ReactiveCommand.CreateFromTask<string>(RemoveInstanceAsync);

        var workspaceActive = this.WhenAnyValue(x => x.CurrentViewModel)
            .Select(viewModel => viewModel is WorkspaceViewModel);
        CreateModCommand = ReactiveCommand.CreateFromObservable(
            () => ActiveModList!.CreateModCommand.Execute(),
            workspaceActive
        );
        CreateGroupCommand = ReactiveCommand.CreateFromObservable(
            () => ActiveModList!.CreateGroupCommand.Execute(),
            workspaceActive
        );

        if (sources is not null)
        {
            Sources = sources;
            _ = sources.RefreshCommand.Execute().Subscribe();
        }
    }

    public SourcesMenuViewModel? Sources { get; }

    private async Task InitializeAsync()
    {
        RefreshInstances();
        if (_instances!.Current is null)
        {
            ShowGameSelection();
            return;
        }
        var profilesRoot = _paths!.ProfilesFolder;
        var (profile, currentGame) = await Task.Run(() =>
        {
            profilesRoot.Create();
            var latestProfileFile = profilesRoot
                .EnumerateDirectories()
                .Select(dir => new FileInfo(Path.Join(dir.FullName, ModProfileSerializer.FileName)))
                .Where(file => file.Exists)
                .OrderByDescending(file => file.LastWriteTimeUtc)
                .FirstOrDefault();
            var loaded = latestProfileFile is not null
                ? _profileSerializer!.Load(latestProfileFile)
                : CreateDefaultProfile(
                    new DirectoryInfo(Path.Join(profilesRoot.FullName, "Default"))
                );
            NormalizePriorities(loaded);
            return (loaded, _instances.Current?.Game);
        });
        ActiveProfile = new ProfileViewModel(profile);
        RefreshProfileNames();
        SyncSelectedProfile(profile.ProfileFolder.Name);
        CurrentGameText =
            currentGame != null
                ? $"{currentGame.Configuration.Name} - {currentGame.InstallPath.FullName}"
                : null;
        ShowWorkspace();
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
            .Subscribe(completedName =>
            {
                RefreshInstances();
                _ = InitializeAsync();
            });
    }

    private void ShowWorkspace()
    {
        var viewModel = new WorkspaceViewModel(
            ActiveProfile!.Model,
            _modFactory!,
            _profileSerializer!,
            _modSerializer!,
            _paths!,
            _editor!,
            _loadOrderBuilder!,
            _instances!,
            _downloads,
            _installService
        );
        CurrentViewModel = viewModel;
        _ = viewModel
            .InitializeAsync()
            .ContinueWith(t => Debug.WriteLine(t.Exception), TaskContinuationOptions.OnlyOnFaulted);
    }

    private void LoadProfile(string name)
    {
        if (_paths is null || _profileSerializer is null)
        {
            return;
        }
        var profileFolder = new DirectoryInfo(Path.Join(_paths.ProfilesFolder.FullName, name));
        var profile = _profileSerializer.Load(profileFolder);
        NormalizePriorities(profile);
        ActiveProfile = new ProfileViewModel(profile);
        ShowWorkspace();
    }

    private void RefreshProfileNames()
    {
        ProfileNames.Clear();
        var profilesRoot = _paths!.ProfilesFolder;
        profilesRoot.Refresh();
        if (!profilesRoot.Exists)
        {
            return;
        }
        foreach (
            var dir in profilesRoot
                .EnumerateDirectories()
                .Where(dir => File.Exists(Path.Join(dir.FullName, ModProfileSerializer.FileName)))
                .OrderBy(dir => dir.Name, StringComparer.OrdinalIgnoreCase)
        )
        {
            ProfileNames.Add(dir.Name);
        }
    }

    private void SyncSelectedProfile(string? name)
    {
        _suppressProfileSwitch = true;
        try
        {
            SelectedProfileName = name;
        }
        finally
        {
            _suppressProfileSwitch = false;
        }
    }

    private IModProfile CreateDefaultProfile(DirectoryInfo profileFolder)
    {
        var profile = new ModProfile(
            "Default",
            new ModList([], []),
            new Version(1, 0),
            profileFolder
        );
        profile.InitializeDisk();
        _profileSerializer!.Save(profile);
        return profile;
    }

    internal static void NormalizePriorities(IModProfile profile)
    {
        var modList = profile.ModList;
        ModOrderSync.ApplyOrder(
            modList.LooseMods.ToList(),
            modList.ModGroups.ToList(),
            modList.ModGroups.Select(group => (IReadOnlyList<ILibraryMod>)group.ToList()).ToList(),
            modList
        );
    }

    public void SaveActiveProfile()
    {
        if (ActiveProfile is not null)
        {
            _profileSerializer!.Save(ActiveProfile.Model);
        }
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
                new InstanceEntryViewModel(name, folder.FullName, name == _instances.Current?.Name)
            );
        }
    }

    private void SwitchInstance(string name)
    {
        if (_instances is null || name == _instances.Current?.Name)
        {
            return;
        }
        _instances.Switch(name);
        RestartApplication();
    }

    private async Task RemoveInstanceAsync(string name)
    {
        if (_instances is null)
        {
            return;
        }
        var choice = await ConfirmDeleteInstance.Handle(name);
        if (choice == InstanceDeleteChoice.Cancel)
        {
            return;
        }
        await Task.Run(() => _instances.Remove(name, choice == InstanceDeleteChoice.DeleteFolder));
        await InitializeAsync();
    }

    public void SaveActiveInstance()
    {
        SaveActiveProfile();
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
