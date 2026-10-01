using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Reactive;
using System.Reactive.Linq;
using Ariadne.Contracts.Games;
using Ariadne.Contracts.ModManager;
using Ariadne.Downloads;
using Ariadne.ModManager.Serialization;
using Avalonia.Controls.ApplicationLifetimes;
using ReactiveUI;

namespace Ariadne.ModManager.GUI.ViewModels;

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
    private readonly IDeploymentService? _deploymentService;
    private readonly ILaunchTargetService? _launchTargetService;
    private readonly IProfileService? _profiles;

    private object? _currentViewModel;
    private ProfileViewModel? _activeProfile;
    private System.Reactive.Disposables.CompositeDisposable _workspaceLinks = [];
    private string? _sidePanelTabTitle;
    private bool _trayListView;
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
            _workspaceLinks.Dispose();
            _workspaceLinks = [];
            (_currentViewModel as IDisposable)?.Dispose();
            this.RaiseAndSetIfChanged(ref _currentViewModel, value);
            this.RaisePropertyChanged(nameof(WorkspaceVisible));
            this.RaisePropertyChanged(nameof(ActiveWorkspace));
            this.RaisePropertyChanged(nameof(ProfileSwitchLocked));
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
            if (
                value is not null
                && !_suppressProfileSwitch
                && value != _profiles?.Active?.ProfileFolder.Name
                && ActiveWorkspace?.IsDeployed != true
            )
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
    public ObservableCollection<InstanceMenuOptionViewModel> SwitchInstanceItems { get; } = [];
    public ObservableCollection<InstanceMenuOptionViewModel> RemoveInstanceItems { get; } = [];
    public ObservableCollection<ThemeEntryViewModel> Themes { get; } = [];
    public ObservableCollection<LaunchTargetItemViewModel> TrayItems { get; } = [];
    public ObservableCollection<ProfileImportOptionViewModel> ImportItems { get; } = [];

    public bool HasTrayItems => TrayItems.Count > 0;

    public bool TrayListView
    {
        get => _trayListView;
        set => this.RaiseAndSetIfChanged(ref _trayListView, value);
    }

    public Interaction<string, InstanceDeleteChoice> ConfirmDeleteInstance { get; } = new();
    public Interaction<(string Title, string Text), Unit> ShowInfo { get; } = new();
    public Interaction<Unit, string?> AskProfileName { get; } = new();

    public ReactiveCommand<Unit, Unit> InitializeCommand { get; }
    public ReactiveCommand<Unit, Unit> ExitCommand { get; }
    public ReactiveCommand<string, Unit> SwitchInstanceCommand { get; }
    public ReactiveCommand<string, Unit> AskDeleteCommand { get; }
    public ReactiveCommand<Unit, Unit> CreateModCommand { get; }
    public ReactiveCommand<Unit, Unit> CreateGroupCommand { get; }
    public ReactiveCommand<Unit, Unit> CreateProfileCommand { get; }
    public ReactiveCommand<Unit, Unit> ImportFromAllProfilesCommand { get; }
    public ReactiveCommand<string, Unit> ImportFromProfileCommand { get; }
    public ReactiveCommand<Unit, Unit> ImportFromDiskCommand { get; }

    private ModListViewModel? ActiveModList => (CurrentViewModel as WorkspaceViewModel)?.ModList;

    public WorkspaceViewModel? ActiveWorkspace => CurrentViewModel as WorkspaceViewModel;

    public bool ProfileSwitchLocked => ActiveWorkspace?.IsDeployed == true;

    public MainViewModel()
    {
        InitializeCommand = ReactiveCommand.Create(() => { });
        ExitCommand = ReactiveCommand.Create(Quit);
        SwitchInstanceCommand = ReactiveCommand.Create<string>(_ => { });
        AskDeleteCommand = ReactiveCommand.Create<string>(_ => { });
        CreateModCommand = ReactiveCommand.Create(() => { });
        CreateGroupCommand = ReactiveCommand.Create(() => { });
        CreateProfileCommand = ReactiveCommand.Create(() => { });
        ImportFromAllProfilesCommand = ReactiveCommand.Create(() => { });
        ImportFromProfileCommand = ReactiveCommand.Create<string>(_ => { });
        ImportFromDiskCommand = ReactiveCommand.Create(() => { });
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
        IModInstallService? installService = null,
        IDeploymentService? deploymentService = null,
        ILaunchTargetService? launchTargetService = null,
        IProfileService? profiles = null
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
        _deploymentService = deploymentService;
        _launchTargetService = launchTargetService;
        _profiles = profiles;

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
        var workspaceWritable = this.WhenAnyValue(x => x.ActiveWorkspace)
            .Select(workspace =>
                workspace?.WhenAnyValue(w => w.IsDeployed) ?? Observable.Return(false)
            )
            .Switch()
            .CombineLatest(workspaceActive, (deployed, active) => active && !deployed);
        CreateModCommand = ReactiveCommand.CreateFromObservable(
            () => ActiveModList!.CreateModCommand.Execute(),
            workspaceWritable
        );
        CreateGroupCommand = ReactiveCommand.CreateFromObservable(
            () => ActiveModList!.CreateGroupCommand.Execute(),
            workspaceWritable
        );
        CreateProfileCommand = ReactiveCommand.CreateFromTask(
            CreateProfileAsync,
            workspaceWritable
        );
        ImportFromAllProfilesCommand = ReactiveCommand.Create(
            ImportFromAllProfiles,
            workspaceWritable
        );
        ImportFromProfileCommand = ReactiveCommand.Create<string>(
            ImportFromProfile,
            workspaceWritable
        );
        ImportFromDiskCommand = ReactiveCommand.Create(ImportFromDisk, workspaceWritable);

        if (sources is not null)
        {
            Sources = sources;
            _ = sources.RefreshCommand.Execute().Subscribe();
        }

        if (launchTargetService is not null)
        {
            RebuildTrayItems();
            launchTargetService.LaunchTargetsChanged += (_, _) =>
                Avalonia.Threading.Dispatcher.UIThread.Post(RebuildTrayItems);
        }

        ProfileNames.CollectionChanged += (_, _) => RebuildImportItems();
        this.WhenAnyValue(x => x.ActiveProfile).Subscribe(_ => RebuildImportItems());
        RebuildImportItems();
    }

    private void RebuildImportItems()
    {
        ImportItems.Clear();
        if (_profiles is null)
        {
            return;
        }
        ImportItems.Add(
            new ProfileImportOptionViewModel(
                "All",
                ImportFromAllProfilesCommand,
                Gesture: new Avalonia.Input.KeyGesture(
                    Avalonia.Input.Key.I,
                    Avalonia.Input.KeyModifiers.Control
                )
            )
        );
        var active = _profiles.Active?.ProfileFolder.Name;
        foreach (var name in ProfileNames)
        {
            if (name != active)
            {
                ImportItems.Add(
                    new ProfileImportOptionViewModel(name, ImportFromProfileCommand, name)
                );
            }
        }
    }

    private void RebuildTrayItems()
    {
        TrayItems.Clear();
        if (_launchTargetService is null)
        {
            return;
        }
        foreach (var target in _launchTargetService.LaunchTargets)
        {
            TrayItems.Add(
                new LaunchTargetItemViewModel(
                    target.Name,
                    ExtractIcon(target.AbsolutePath),
                    ReactiveCommand.Create(() => _launchTargetService.Launch(target))
                )
            );
        }
        this.RaisePropertyChanged(nameof(HasTrayItems));
    }

    private static Avalonia.Media.Imaging.Bitmap? ExtractIcon(string path)
    {
        try
        {
            using var icon = System.Drawing.Icon.ExtractAssociatedIcon(path);
            if (icon is null)
            {
                return null;
            }
            using var bitmap = icon.ToBitmap();
            var stream = new MemoryStream();
            bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
            stream.Position = 0;
            return new Avalonia.Media.Imaging.Bitmap(stream);
        }
        catch (Exception ex)
            when (ex
                    is IOException
                        or InvalidOperationException
                        or ArgumentException
                        or PlatformNotSupportedException
            )
        {
            return null;
        }
    }

    public SourcesMenuViewModel? Sources { get; }

    private async Task InitializeAsync()
    {
        RefreshInstances();
        if (_instances!.Current is null || _profiles is null)
        {
            ShowGameSelection();
            return;
        }
        var currentGame = await Task.Run(() =>
        {
            _profiles.ActivateLatestOrDefault();
            return _instances.Current?.Game;
        });
        var profile = _profiles.Active!;
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
            _installService,
            _deploymentService
        );
        CurrentViewModel = viewModel;
        if (_sidePanelTabTitle is not null)
        {
            viewModel.SelectedTab =
                viewModel.SidePanelTabs.FirstOrDefault(tab => tab.Title == _sidePanelTabTitle)
                ?? viewModel.SelectedTab;
        }
        _workspaceLinks = new System.Reactive.Disposables.CompositeDisposable
        {
            viewModel.DeploymentFailed.Subscribe(failure => _ = ShowInfo.Handle(failure)),
            viewModel
                .WhenAnyValue(x => x.SelectedTab)
                .WhereNotNull()
                .Subscribe(tab => _sidePanelTabTitle = tab.Title),
            viewModel
                .WhenAnyValue(x => x.IsDeployed)
                .Subscribe(_ => this.RaisePropertyChanged(nameof(ProfileSwitchLocked))),
        };
        _ = viewModel
            .InitializeAsync()
            .ContinueWith(t => Debug.WriteLine(t.Exception), TaskContinuationOptions.OnlyOnFaulted);
    }

    private void LoadProfile(string name)
    {
        if (_profiles is null)
        {
            return;
        }
        var profile = _profiles.Switch(name);
        ActiveProfile = new ProfileViewModel(profile);
        ShowWorkspace();
    }

    private void ImportFromAllProfiles()
    {
        if (_profiles is null)
        {
            return;
        }
        _profiles.RefreshNames();
        var active = _profiles.Active?.ProfileFolder.Name;
        foreach (var name in _profiles.Names.Where(name => name != active).ToList())
        {
            ImportFromProfile(name);
        }
    }

    private void ImportFromProfile(string name)
    {
        if (
            _profiles is null
            || _profiles.Active is null
            || ActiveModList is null
            || name == _profiles.Active.ProfileFolder.Name
        )
        {
            return;
        }
        IModProfile source;
        try
        {
            source = _profiles.Read(name);
        }
        catch (Exception ex)
            when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException
            )
        {
            Debug.WriteLine(ex);
            return;
        }
        var activeMods = _profiles.Active.ModList;
        foreach (var entry in source.ModList)
        {
            if (activeMods.Contains(entry.Mod))
            {
                continue;
            }
            ActiveModList.RegisterMod(entry.Mod);
        }
    }

    private void ImportFromDisk()
    {
        if (_paths is null || _modSerializer is null || ActiveModList is null)
        {
            return;
        }
        var modsFolder = _paths.ModsFolder;
        modsFolder.Refresh();
        if (!modsFolder.Exists)
        {
            return;
        }
        foreach (var folder in modsFolder.EnumerateDirectories())
        {
            ILibraryMod mod;
            try
            {
                mod = _modSerializer.Load(folder);
            }
            catch (Exception ex)
                when (ex
                        is IOException
                            or UnauthorizedAccessException
                            or System.Text.Json.JsonException
                )
            {
                continue;
            }
            ActiveModList.RegisterMod(mod);
        }
    }

    private void RefreshProfileNames()
    {
        ProfileNames.Clear();
        if (_profiles is null)
        {
            return;
        }
        _profiles.RefreshNames();
        foreach (var name in _profiles.Names)
        {
            ProfileNames.Add(name);
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

    private async Task CreateProfileAsync()
    {
        if (_profiles is null)
        {
            return;
        }
        var name = await AskProfileName.Handle(Unit.Default);
        name = name?.Trim();
        if (string.IsNullOrEmpty(name) || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            return;
        }
        if (!ProfileNames.Contains(name, StringComparer.OrdinalIgnoreCase))
        {
            await Task.Run(() => _profiles.Create(name));
            RefreshProfileNames();
        }
        SelectedProfileName = name;
    }

    public void SaveActiveProfile()
    {
        _profiles?.SaveActive();
    }

    private void RefreshInstances()
    {
        Instances.Clear();
        SwitchInstanceItems.Clear();
        RemoveInstanceItems.Clear();
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
            var entry = new InstanceEntryViewModel(
                name,
                folder.FullName,
                name == _instances.Current?.Name
            );
            Instances.Add(entry);
            SwitchInstanceItems.Add(
                new InstanceMenuOptionViewModel(
                    entry.DisplayHeader,
                    SwitchInstanceCommand,
                    name,
                    !entry.IsCurrent
                )
            );
            RemoveInstanceItems.Add(
                new InstanceMenuOptionViewModel(entry.RemoveHeader, AskDeleteCommand, name)
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
