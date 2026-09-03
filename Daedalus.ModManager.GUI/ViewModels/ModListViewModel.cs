using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Templates;
using Daedalus.Contracts.ModManager;
using Daedalus.Contracts.Mods;
using Daedalus.ModManager;
using Daedalus.ModManager.Serialization;
using Daedalus.Mods;
using ReactiveUI;

namespace Daedalus.ModManager.GUI.ViewModels;

public sealed class ModListViewModel : ViewModelBase
{
    private readonly IModProfileSerializer _profileSerializer;
    private readonly ILibraryModSerializer _modSerializer;
    private readonly IModManagerPaths _paths;
    private readonly IInstanceService _instances;

    private ProfileViewModel? _activeProfile;
    private ModListGridSource? _modsSource;
    private string? _currentGameText;
    private string? _selectedProfileName;
    private bool _suppressProfileSwitch;
    private ObservableCollection<TreeNodeViewModel>? _dragRoots;
    private IDisposable? _dragSyncSubscription;
    private readonly SynchronizationContext? _uiContext = SynchronizationContext.Current;

    public ProfileViewModel? ActiveProfile
    {
        get => _activeProfile;
        private set => this.RaiseAndSetIfChanged(ref _activeProfile, value);
    }

    public ModListGridSource? ModsSource
    {
        get => _modsSource;
        private set => this.RaiseAndSetIfChanged(ref _modsSource, value);
    }

    public ObservableCollection<string> ProfileNames { get; } = [];

    public string? SelectedProfileName
    {
        get => _selectedProfileName;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedProfileName, value);
            if (value is not null && !_suppressProfileSwitch && value != ActiveProfile?.Name)
            {
                LoadProfileByName(value);
            }
        }
    }

    public string? CurrentGameText
    {
        get => _currentGameText;
        private set => this.RaiseAndSetIfChanged(ref _currentGameText, value);
    }

    public ReactiveCommand<Unit, Unit> InitializeCommand { get; }
    public ReactiveCommand<Unit, Unit> CreateModCommand { get; }
    public ReactiveCommand<Unit, Unit> CreateGroupCommand { get; }

    public ModListViewModel(
        IModProfileSerializer profileSerializer,
        ILibraryModSerializer modSerializer,
        IModManagerPaths paths,
        IInstanceService instances
    )
    {
        _profileSerializer = profileSerializer;
        _modSerializer = modSerializer;
        _paths = paths;
        _instances = instances;

        InitializeCommand = ReactiveCommand.CreateFromTask(InitializeAsync);
        var hasActiveProfile = this.WhenAnyValue(x => x.ActiveProfile)
            .Select(profile => profile is not null);
        CreateModCommand = ReactiveCommand.CreateFromTask(CreateEmptyModAsync, hasActiveProfile);
        CreateGroupCommand = ReactiveCommand.Create(CreateGroup, hasActiveProfile);
    }

    private static double RelativeLuminance(byte r, byte g, byte b)
    {
        static double Linear(double c) =>
            c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        return 0.2126 * Linear(r / 255.0) + 0.7152 * Linear(g / 255.0) + 0.0722 * Linear(b / 255.0);
    }

    private static System.Drawing.Color RandomHeaderColor()
    {
        for (var attempt = 0; attempt < 32; attempt++)
        {
            var hue = Random.Shared.NextDouble() * 360.0;
            var s = 0.5 + Random.Shared.NextDouble() * 0.25;
            var v = 0.45 + Random.Shared.NextDouble() * 0.2;
            var x = v * s * (1 - Math.Abs((hue / 60) % 2 - 1));
            var c = v * s;
            var m = v - c;
            var (r, g, b) = hue switch
            {
                < 60 => (c, x, 0.0),
                < 120 => (x, c, 0.0),
                < 180 => (0.0, c, x),
                < 240 => (0.0, x, c),
                < 300 => (x, 0.0, c),
                _ => (c, 0.0, x),
            };
            var color = System.Drawing.Color.FromArgb(
                255,
                (int)((r + m) * 255),
                (int)((g + m) * 255),
                (int)((b + m) * 255)
            );
            if (RelativeLuminance(color.R, color.G, color.B) <= 0.17)
            {
                return color;
            }
        }
        return System.Drawing.Color.FromArgb(255, 69, 71, 90);
    }

    private void CreateGroup()
    {
        var name = "New Group";
        var suffix = 2;
        while (ActiveProfile!.Model.ModList.ModGroups.Any(group => group.Name == name))
        {
            name = $"New Group {suffix++}";
        }
        ActiveProfile.AddGroup(new ModGroup(name, [], RandomHeaderColor()));
        _profileSerializer.Save(ActiveProfile.Model);
        ModsSource = BuildGridSource(ActiveProfile.Model.ModList);
    }

    private Task InitializeAsync() =>
        Task.Run(() =>
        {
            var profilesRoot = _paths.ProfilesFolder;
            profilesRoot.Create();
            var latestProfileFile = profilesRoot
                .EnumerateDirectories()
                .Select(dir => new FileInfo(Path.Join(dir.FullName, ModProfileSerializer.FileName)))
                .Where(file => file.Exists)
                .OrderByDescending(file => file.LastWriteTimeUtc)
                .FirstOrDefault();

            var profile = latestProfileFile is not null
                ? _profileSerializer.Load(latestProfileFile)
                : CreateDefaultProfile(
                    new DirectoryInfo(Path.Join(profilesRoot.FullName, "Default"))
                );

            NormalizePriorities(profile);
            var currentGame = _instances.CurrentGame;
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                ActiveProfile = new ProfileViewModel(profile);
                ModsSource = BuildGridSource(profile.ModList);
                RefreshProfileNames();
                SyncSelectedProfile(profile.ProfileFolder.Name);
                CurrentGameText = currentGame is { } game
                    ? $"{game.Configuration.Name} — {game.InstallPath.FullName}"
                    : null;
            });
        });

    private IModProfile CreateDefaultProfile(DirectoryInfo profileFolder)
    {
        var profile = new ModProfile(
            "Default",
            new ModList([], []),
            new Version(1, 0),
            profileFolder
        );
        profile.InitializeDisk();
        _profileSerializer.Save(profile);
        return profile;
    }

    private void RefreshProfileNames()
    {
        ProfileNames.Clear();
        var profilesRoot = _paths.ProfilesFolder;
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

    private static void NormalizePriorities(IModProfile profile)
    {
        var modList = profile.ModList;
        ModOrderSync.ApplyOrder(
            modList.LooseMods.ToList(),
            modList.ModGroups.ToList(),
            modList.ModGroups.Select(group => (IReadOnlyList<ILibraryMod>)group.ToList()).ToList(),
            modList
        );
    }

    private void LoadProfileByName(string name)
    {
        var profileFolder = new DirectoryInfo(Path.Join(_paths.ProfilesFolder.FullName, name));
        var profile = _profileSerializer.Load(profileFolder);
        NormalizePriorities(profile);
        ActiveProfile = new ProfileViewModel(profile);
        ModsSource = BuildGridSource(profile.ModList);
    }

    private Task CreateEmptyModAsync() =>
        Task.Run(() =>
        {
            var modsFolder = _paths.ModsFolder;
            var folder = UniqueModFolder(modsFolder);
            var mod = new LibraryMod(
                new ModInfo(
                    0,
                    "New Mod",
                    SourceType.Local,
                    "1.0.0",
                    [],
                    "",
                    (uint)(ActiveProfile!.Model.ModList.Count + 1)
                ),
                folder,
                []
            );
            folder.Create();
            _modSerializer.Save(mod);

            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                ActiveProfile!.AddLooseMod(mod);
                _profileSerializer.Save(ActiveProfile.Model);
                ModsSource = BuildGridSource(ActiveProfile.Model.ModList);
            });
        });

    private static DirectoryInfo UniqueModFolder(DirectoryInfo modsFolder)
    {
        var candidate = new DirectoryInfo(Path.Join(modsFolder.FullName, "New Mod"));
        for (var i = 2; candidate.Exists; i++)
        {
            candidate = new DirectoryInfo(Path.Join(modsFolder.FullName, $"New Mod {i}"));
        }
        return candidate;
    }

    private static ObservableCollection<TreeNodeViewModel> BuildRoots(IModList modList) =>
        new(
            modList
                .LooseMods.Select(mod =>
                    (TreeNodeViewModel)new ModEntryNodeViewModel((ILibraryMod)mod)
                )
                .Concat(modList.ModGroups.Select(group => new GroupHeaderNodeViewModel(group)))
        );

    private void HookDragSync(ObservableCollection<TreeNodeViewModel> roots)
    {
        _dragRoots = roots;
        _dragSyncSubscription?.Dispose();

        IObservable<System.Reactive.EventPattern<System.Collections.Specialized.NotifyCollectionChangedEventArgs>> Stream(
            ObservableCollection<TreeNodeViewModel> collection
        ) =>
            Observable.FromEventPattern<
                System.Collections.Specialized.NotifyCollectionChangedEventHandler,
                System.Collections.Specialized.NotifyCollectionChangedEventArgs
            >(
                handler => collection.CollectionChanged += handler,
                handler => collection.CollectionChanged -= handler
            );

        var changes = roots
            .OfType<GroupHeaderNodeViewModel>()
            .Select(group => Stream(group.ObservableChildren))
            .Aggregate(Stream(roots), (merged, stream) => merged.Merge(stream));

        _dragSyncSubscription = changes
            .Throttle(TimeSpan.FromMilliseconds(300))
            .ObserveOn(_uiContext ?? SynchronizationContext.Current ?? new SynchronizationContext())
            .Subscribe(_ => SyncDomainFromTree());
    }

    private void SyncDomainFromTree()
    {
        if (ActiveProfile is null || _dragRoots is null)
        {
            return;
        }

        var looseMods = _dragRoots
            .OfType<ModEntryNodeViewModel>()
            .Select(node => node.Model)
            .ToList();
        var groups = _dragRoots.OfType<GroupHeaderNodeViewModel>().ToList();
        var groupMembers = groups
            .Select(node =>
                (IReadOnlyList<ILibraryMod>)
                    node
                        .ObservableChildren.OfType<ModEntryNodeViewModel>()
                        .Select(child => child.Model)
                        .ToList()
            )
            .ToList();

        ModOrderSync.ApplyOrder(
            looseMods,
            groups.Select(g => g.Group).ToList(),
            groupMembers,
            ActiveProfile.Model.ModList
        );
        _profileSerializer.Save(ActiveProfile.Model);

        void RefreshNodes(IEnumerable<TreeNodeViewModel> nodes)
        {
            foreach (var node in nodes)
            {
                if (node is ModEntryNodeViewModel mod)
                {
                    mod.RefreshFromModel();
                }
                else if (node is GroupHeaderNodeViewModel group)
                {
                    RefreshNodes(group.ObservableChildren);
                }
            }
        }
        RefreshNodes(_dragRoots);
    }

    private ModListGridSource BuildGridSource(IModList modList)
    {
        var roots = BuildRoots(modList);
        HookDragSync(roots);
        var source = new HierarchicalTreeDataGridSource<TreeNodeViewModel>(roots)
        {
            Columns =
            {
                new HierarchicalExpanderColumn<TreeNodeViewModel>(
                    new TemplateColumn<TreeNodeViewModel>(
                        "Name",
                        new NodeCellTemplate(),
                        width: new GridLength(3, Avalonia.Controls.GridUnitType.Star)
                    ),
                    node => node.Children,
                    node => node.HasChildren
                ),
                new TextColumn<TreeNodeViewModel, uint?>(
                    "Priority",
                    node => node.PriorityValue,
                    width: new GridLength(90)
                ),
                new TextColumn<TreeNodeViewModel, string?>(
                    "Version",
                    node => node.VersionText,
                    width: new GridLength(120)
                ),
                new TextColumn<TreeNodeViewModel, string>(
                    "Size",
                    node => node.SizeText,
                    width: new GridLength(100)
                ),
            },
        };
        var gridSource = new ModListGridSource(source);

        for (var i = roots.Count - 1; i >= 0; i--)
        {
            if (roots[i] is GroupHeaderNodeViewModel)
            {
                gridSource.Expand(i);
            }
        }

        return gridSource;
    }

    private sealed class NodeCellTemplate : IDataTemplate
    {
        private static readonly Views.ViewLocator s_locator = new();

        public Control? Build(object? param) =>
            param is TreeNodeViewModel node ? s_locator.Build(node) : null;

        public bool Match(object? data) => data is TreeNodeViewModel;
    }
}
