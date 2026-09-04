using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
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
    private static readonly TimeSpan SyncDelay = TimeSpan.FromMilliseconds(300);

    private readonly IModProfileSerializer _profileSerializer;
    private readonly ILibraryModSerializer _modSerializer;
    private readonly IModManagerPaths _paths;
    private readonly IInstanceService _instances;
    private readonly IModProfileEditor _editor;
    private readonly SynchronizationContext? _uiContext = SynchronizationContext.Current;

    private ProfileViewModel? _activeProfile;
    private ModListGridSource? _modsSource;
    private string? _currentGameText;
    private string? _selectedProfileName;
    private bool _suppressProfileSwitch;
    private ObservableCollection<TreeNodeViewModel>? _dragRoots;
    private IDisposable? _dragSyncSubscription;
    private IDisposable? _nodeActionSubscription;
    private readonly SemaphoreSlim _syncGate = new(1, 1);

    public ReactiveCommand<Unit, Unit> InitializeCommand { get; }
    public ReactiveCommand<Unit, Unit> CreateModCommand { get; }
    public ReactiveCommand<Unit, Unit> CreateGroupCommand { get; }
    public ReactiveCommand<Unit, Unit> ClearSortCommand { get; }
    public Interaction<ModEntryNodeViewModel, bool> ConfirmRemoveMod { get; } = new();

    public ObservableCollection<string> ProfileNames { get; } = [];

    public ProfileViewModel? ActiveProfile
    {
        get => _activeProfile;
        private set => this.RaiseAndSetIfChanged(ref _activeProfile, value);
    }

    public ModListGridSource? ModsSource
    {
        get => _modsSource;
        private set
        {
            if (_modsSource is not null)
            {
                _modsSource.Sorted -= OnModsSourceSorted;
            }
            this.RaiseAndSetIfChanged(ref _modsSource, value);
            if (value is not null)
            {
                value.Sorted += OnModsSourceSorted;
            }
            this.RaisePropertyChanged(nameof(SortActive));
        }
    }

    public bool SortActive => ModsSource?.IsSorted == true;

    private void OnModsSourceSorted() => this.RaisePropertyChanged(nameof(SortActive));

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

    public ModListViewModel(
        IModProfileSerializer profileSerializer,
        ILibraryModSerializer modSerializer,
        IModManagerPaths paths,
        IInstanceService instances,
        IModProfileEditor editor
    )
    {
        _profileSerializer = profileSerializer;
        _modSerializer = modSerializer;
        _paths = paths;
        _instances = instances;
        _editor = editor;

        InitializeCommand = ReactiveCommand.CreateFromTask(InitializeAsync);
        var hasActiveProfile = this.WhenAnyValue(x => x.ActiveProfile)
            .Select(profile => profile is not null);
        CreateModCommand = ReactiveCommand.CreateFromTask(CreateEmptyMod, hasActiveProfile);
        CreateGroupCommand = ReactiveCommand.Create(CreateGroup, hasActiveProfile);
        ClearSortCommand = ReactiveCommand.Create(() =>
        {
            ModsSource?.ClearSort();
            this.RaisePropertyChanged(nameof(SortActive));
        });
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

    private void LoadProfile(string name)
    {
        var profileFolder = new DirectoryInfo(Path.Join(_paths.ProfilesFolder.FullName, name));
        var profile = _profileSerializer.Load(profileFolder);
        NormalizePriorities(profile);
        ActiveProfile = new ProfileViewModel(profile);
        ModsSource = BuildGridSource(profile.ModList);
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

    public void SaveActiveProfile()
    {
        if (ActiveProfile is not null)
        {
            _profileSerializer.Save(ActiveProfile.Model);
        }
    }

    private Task CreateEmptyMod() =>
        Task.Run(() =>
        {
            var folder = UniqueModFolder(_paths.ModsFolder);
            var mod = new LibraryMod(
                new ModInfo(
                    0,
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

    private void CreateGroup()
    {
        var baseName = "New Group";
        var name = baseName;
        var suffix = 2;
        while (ActiveProfile!.Model.ModList.ModGroups.Any(group => group.Name == name))
        {
            name = $"{baseName} {suffix++}";
        }
        ActiveProfile.AddGroup(new ModGroup(name, [], RandomHeaderColor()));
        _profileSerializer.Save(ActiveProfile.Model);
        ModsSource = BuildGridSource(ActiveProfile.Model.ModList);
    }

    private static DirectoryInfo UniqueModFolder(DirectoryInfo modsFolder)
    {
        var candidate = new DirectoryInfo(Path.Join(modsFolder.FullName, "New Mod"));
        for (var i = 2; candidate.Exists; i++)
        {
            candidate = new DirectoryInfo(Path.Join(modsFolder.FullName, $"New Mod {i}"));
        }
        return candidate;
    }

    // wcag

    private static System.Drawing.Color RandomHeaderColor()
    {
        for (var attempt = 0; attempt < 32; attempt++)
        {
            var color = System.Drawing.Color.FromArgb(
                255,
                Random.Shared.Next(40, 100),
                Random.Shared.Next(40, 140),
                Random.Shared.Next(40, 160)
            );
            if (RelativeLuminance(color.R, color.G, color.B) <= 0.17)
            {
                return color;
            }
        }
        return System.Drawing.Color.FromArgb(255, 69, 71, 90);
    }

    private static double RelativeLuminance(byte r, byte g, byte b)
    {
        static double Linear(double c) =>
            c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        return 0.2126 * Linear(r / 255.0) + 0.7152 * Linear(g / 255.0) + 0.0722 * Linear(b / 255.0);
    }

    private ModListGridSource BuildGridSource(IModList modList)
    {
        var roots = BuildRoots(modList);
        HookDomainSync(roots);
        var source = new HierarchicalTreeDataGridSource<TreeNodeViewModel>(roots)
        {
            Columns =
            {
                new HierarchicalExpanderColumn<TreeNodeViewModel>(
                    new TemplateColumn<TreeNodeViewModel>(
                        "Name",
                        new NodeCellTemplate(),
                        width: new GridLength(3, GridUnitType.Star)
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
            if (roots[i] is GroupHeaderNodeViewModel { HasChildren: true })
            {
                gridSource.Expand(i);
            }
        }

        return gridSource;
    }

    private static ObservableCollection<TreeNodeViewModel> BuildRoots(IModList modList) =>
        new(
            modList
                .LooseMods.Select(mod => (TreeNodeViewModel)new ModEntryNodeViewModel(mod))
                .Concat(modList.ModGroups.Select(group => new GroupHeaderNodeViewModel(group)))
        );

    private void HookDomainSync(ObservableCollection<TreeNodeViewModel> roots)
    {
        _dragRoots = roots;
        _dragSyncSubscription?.Dispose();
        _nodeActionSubscription?.Dispose();

        var groupNodes = roots.OfType<GroupHeaderNodeViewModel>().ToList();
        var collectionStreams = groupNodes
            .Select(group => StreamOf(group.ObservableChildren))
            .Append(StreamOf(roots))
            .Select(stream => stream.Select(_ => Unit.Default));
        var renameStreams = roots
            .Concat(groupNodes.SelectMany(group => group.ObservableChildren))
            .Select(node => node.RenameCommitted);

        _dragSyncSubscription = Observable
            .Merge(collectionStreams.Concat(renameStreams))
            .Throttle(SyncDelay)
            .ObserveOn(_uiContext ?? SynchronizationContext.Current!)
            .Subscribe(ignored =>
            {
                _ = SyncDomainFromTreeAsync();
            });

        var uiContext = _uiContext ?? SynchronizationContext.Current!;
        var modNodes = roots
            .Concat(groupNodes.SelectMany(group => group.ObservableChildren))
            .OfType<ModEntryNodeViewModel>()
            .ToList();
        _nodeActionSubscription = new CompositeDisposable
        {
            Observable
                .Merge(modNodes.Select(node => node.RemoveRequested.Select(_ => node)))
                .ObserveOn(uiContext)
                .Subscribe(node =>
                {
                    _ = RemoveModNodeAsync(node);
                }),
            Observable
                .Merge(groupNodes.Select(group => group.DissolveRequested.Select(_ => group)))
                .ObserveOn(uiContext)
                .Subscribe(group =>
                {
                    _ = DissolveGroupNodeAsync(group);
                }),
        };

        static IObservable<EventPattern<NotifyCollectionChangedEventArgs>> StreamOf(
            ObservableCollection<TreeNodeViewModel> collection
        ) =>
            Observable.FromEventPattern<
                NotifyCollectionChangedEventHandler,
                NotifyCollectionChangedEventArgs
            >(
                handler => collection.CollectionChanged += handler,
                handler => collection.CollectionChanged -= handler
            );
    }

    private async Task SyncDomainFromTreeAsync()
    {
        if (ActiveProfile is null || _dragRoots is null)
        {
            return;
        }

        var profile = ActiveProfile.Model;
        var roots = _dragRoots;
        var looseMods = roots.OfType<ModEntryNodeViewModel>().Select(node => node.Model).ToList();
        var groupNodes = roots.OfType<GroupHeaderNodeViewModel>().ToList();
        var groupMembers = groupNodes
            .Select(node =>
                (IReadOnlyList<ILibraryMod>)
                    node
                        .ObservableChildren.OfType<ModEntryNodeViewModel>()
                        .Select(child => child.Model)
                        .ToList()
            )
            .ToList();

        try
        {
            await _syncGate.WaitAsync();
            try
            {
                await Task.Run(() =>
                {
                    ModOrderSync.ApplyOrder(
                        looseMods,
                        groupNodes.Select(group => group.Group).ToList(),
                        groupMembers,
                        profile.ModList
                    );
                    _profileSerializer.Save(profile);
                });
            }
            finally
            {
                _syncGate.Release();
            }
            RefreshPriorities(roots);
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }
    }

    private async Task RemoveModNodeAsync(ModEntryNodeViewModel node)
    {
        if (ActiveProfile is null || _dragRoots is null)
        {
            return;
        }
        try
        {
            if (!await ConfirmRemoveMod.Handle(node))
            {
                return;
            }
            await _editor.RemoveModAsync(ActiveProfile.Model, node.Model);
            if (!_dragRoots.Remove(node))
            {
                foreach (var group in _dragRoots.OfType<GroupHeaderNodeViewModel>())
                {
                    if (group.ObservableChildren.Remove(node))
                    {
                        break;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }
    }

    private async Task DissolveGroupNodeAsync(GroupHeaderNodeViewModel node)
    {
        if (ActiveProfile is null || _dragRoots is null)
        {
            return;
        }
        try
        {
            await _editor.DissolveGroupAsync(ActiveProfile.Model, node.Group);
            var index = _dragRoots.IndexOf(node);
            if (index < 0)
            {
                return;
            }
            var children = node.ObservableChildren.ToList();
            _dragRoots.RemoveAt(index);
            if (index < _dragRoots.Count && _dragRoots[index] is GroupHeaderNodeViewModel nextGroup)
            {
                foreach (var child in children)
                {
                    nextGroup.ObservableChildren.Add(child);
                }
                ModsSource?.Expand(new IndexPath(index));
            }
            else
            {
                foreach (var child in children)
                {
                    _dragRoots.Insert(index++, child);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }
    }

    private static void RefreshPriorities(IEnumerable<TreeNodeViewModel> nodes)
    {
        foreach (var node in nodes)
        {
            switch (node)
            {
                case ModEntryNodeViewModel mod:
                    mod.RefreshFromModel();
                    break;
                case GroupHeaderNodeViewModel group:
                    RefreshPriorities(group.ObservableChildren);
                    break;
            }
        }
    }

    private sealed class NodeCellTemplate : IDataTemplate
    {
        private static readonly Views.ViewLocator s_locator = new();

        public Control? Build(object? param) =>
            param is TreeNodeViewModel node ? s_locator.Build(node) : null;

        public bool Match(object? data) => data is TreeNodeViewModel;
    }
}
