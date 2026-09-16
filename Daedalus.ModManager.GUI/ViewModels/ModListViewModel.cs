using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Threading.Tasks;
using Avalonia.Controls.DataGridDragDrop;
using Avalonia.Controls.DataGridHierarchical;
using Avalonia.Controls.DataGridSorting;
using ByteSizeLib;
using Daedalus.Contracts.ModManager;
using Daedalus.Contracts.Mods;
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
    private CancellationTokenSource? _loadContentCts;

    private ProfileViewModel? _activeProfile;
    private HierarchicalModel<TreeNodeViewModel>? _model;
    private string? _currentGameText;
    private string? _selectedProfileName;
    private bool _suppressProfileSwitch;
    private ObservableCollection<TreeNodeViewModel>? _roots;
    private readonly Subject<Unit> _structureChanged = new();
    private IDisposable? _syncSubscription;
    private CompositeDisposable _syncHooks = new();
    private CompositeDisposable _nodeActionHooks = new();
    private readonly SemaphoreSlim _syncGate = new(1, 1);

    public ReactiveCommand<Unit, Unit> InitializeCommand { get; }
    public ReactiveCommand<Unit, Unit> CreateModCommand { get; }
    public ReactiveCommand<Unit, Unit> CreateGroupCommand { get; }
    public ReactiveCommand<Unit, Unit> ClearSortCommand { get; }
    public Interaction<ModEntryNodeViewModel, bool> ConfirmRemoveMod { get; } = new();
    public ReactiveCommand<ModEntryNodeViewModel, Unit> LoadInsertedRow { get; }

    public ObservableCollection<string> ProfileNames { get; } = [];
    public ObservableCollection<TreeNodeViewModel> SelectedNodes { get; } = [];

    public HierarchicalModel<TreeNodeViewModel>? Model
    {
        get => _model;
        private set => this.RaiseAndSetIfChanged(ref _model, value);
    }

    public ISortingModel SortingModel { get; } =
        new SortingModel { CycleMode = SortCycleMode.AscendingDescendingNone };

    public IDataGridRowDropHandler DropHandler { get; }

    public bool SortActive => SortingModel.Descriptors.Count > 0;

    public ProfileViewModel? ActiveProfile
    {
        get => _activeProfile;
        private set => this.RaiseAndSetIfChanged(ref _activeProfile, value);
    }

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
        TreeNodeViewModel
            .FileOpenRequested.ObserveOn(_uiContext ?? SynchronizationContext.Current!)
            .Subscribe(node =>
            {
                if (node is FileLeafNodeViewModel leaf)
                {
                    _ = OpenFileNode(leaf);
                }
            });
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
        ClearSortCommand = ReactiveCommand.Create(() => SortingModel.Clear());
        LoadInsertedRow = ReactiveCommand.CreateFromTask<ModEntryNodeViewModel>(
            LoadInsertedRowAsync
        );
        LoadInsertedRow.ThrownExceptions.Subscribe(ex => Debug.WriteLine(ex));
        _syncSubscription = _structureChanged
            .Throttle(SyncDelay)
            .ObserveOn(_uiContext ?? SynchronizationContext.Current!)
            .Subscribe(signal =>
            {
                _ = SyncDomainFromTreeAsync();
            });
        DropHandler = new DragDrop.ModListRowDropHandler(
            () => SortActive,
            () => _roots is null ? Array.Empty<TreeNodeViewModel>() : _roots,
            () => Model
        );
        SortingModel.SortingChanged += (_, args) =>
        {
            if (Model is { } model)
            {
                model.ApplySiblingComparer(BuildComparer(args.NewDescriptors), recursive: true);
                if (args.NewDescriptors.Count == 0)
                {
                    model.Refresh();
                }
            }
            this.RaisePropertyChanged(nameof(SortActive));
        };
    }

    private static IComparer<TreeNodeViewModel>? BuildComparer(
        IReadOnlyList<SortingDescriptor> descriptors
    )
    {
        if (descriptors.Count == 0)
        {
            return null;
        }
        return Comparer<TreeNodeViewModel>.Create(
            (left, right) =>
            {
                foreach (var descriptor in descriptors)
                {
                    var result = Compare(left, right, descriptor.PropertyPath);
                    if (result != 0)
                    {
                        return descriptor.Direction == ListSortDirection.Descending
                            ? -result
                            : result;
                    }
                }
                return 0;

                static int Compare(TreeNodeViewModel left, TreeNodeViewModel right, string? path) =>
                    path switch
                    {
                        "Item.DisplayName" or "DisplayName" => string.Compare(
                            left.DisplayName,
                            right.DisplayName,
                            StringComparison.OrdinalIgnoreCase
                        ),
                        "Item.PriorityValue" or "PriorityValue" => Nullable.Compare(
                            left.PriorityValue,
                            right.PriorityValue
                        ),
                        "Item.VersionText" or "VersionText" => string.Compare(
                            left.VersionText,
                            right.VersionText,
                            StringComparison.OrdinalIgnoreCase
                        ),
                        "Item.SizeBytes" or "SizeBytes" => left.SizeBytes.CompareTo(
                            right.SizeBytes
                        ),
                        _ => 0,
                    };
            }
        );
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
                Model = BuildModel(profile.ModList);
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
        Model = BuildModel(profile.ModList);
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
                    (uint)(ActiveProfile!.Model.ModList.Count + 1),
                    false
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
                var vm = new ModEntryNodeViewModel(mod);
                int insertAt = _roots!.TakeWhile(n => n is ModEntryNodeViewModel).Count();
                _roots!.Insert(insertAt, vm);
                HookModNode(vm);
                LoadInsertedRow.Execute(vm).Subscribe();
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
        var group = new ModGroup(name, [], RandomHeaderColor());
        ActiveProfile.AddGroup(group);
        _profileSerializer.Save(ActiveProfile.Model);
        var groupVm = new GroupHeaderNodeViewModel(group);
        _roots!.Add(groupVm);
        HookGroupNode(groupVm);
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

    private static IEnumerable<ModEntryNodeViewModel> FlattenEntries(TreeNodeViewModel node)
    {
        if (node is ModEntryNodeViewModel entry)
        {
            yield return entry;
        }
        else if (node is GroupHeaderNodeViewModel group)
        {
            foreach (var child in group.ObservableChildren)
            {
                foreach (var entry2 in FlattenEntries(child))
                {
                    yield return entry2;
                }
            }
        }
    }

    private Task LoadInsertedRowAsync(ModEntryNodeViewModel node)
    {
        return LoadInsertedRowCoreAsync(node, _loadContentCts?.Token ?? default);
    }

    private async Task LoadInsertedRowCoreAsync(ModEntryNodeViewModel node, CancellationToken token)
    {
        node.ConnectContentTree(token);
        try
        {
            await node.WhenAnyValue(x => x.ContentTree).WhereNotNull().FirstAsync().ToTask(token);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        Avalonia.Threading.Dispatcher.UIThread.Post(() => Model?.Refresh());
    }

    private Task RefreshGroupSizesAsync()
    {
        if (_roots is null)
            return Task.CompletedTask;
        foreach (var group in _roots.OfType<GroupHeaderNodeViewModel>())
        {
            group.RefreshSizeText();
        }
        return Task.CompletedTask;
    }

    private async Task LoadAllContentTreesAsync(
        IReadOnlyList<ModEntryNodeViewModel> entries,
        HierarchicalModel<TreeNodeViewModel> model,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            await Parallel.ForEachAsync(
                entries,
                new ParallelOptions
                {
                    MaxDegreeOfParallelism = Environment.ProcessorCount,
                    CancellationToken = cancellationToken,
                },
                async (vm, token) =>
                {
                    vm.ConnectContentTree(token);
                    await vm.WhenAnyValue(x => x.ContentTree)
                        .WhereNotNull()
                        .FirstAsync()
                        .ToTask(token);
                }
            );
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
            return;
        }
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }
        await RefreshGroupSizesAsync();
        await model.RefreshAsync();
    }

    private HierarchicalModel<TreeNodeViewModel> BuildModel(IModList modList)
    {
        var expandedPaths = _roots is null ? null : CollectExpandedPaths(_roots);
        _loadContentCts?.Cancel();
        _loadContentCts?.Dispose();
        _loadContentCts = new CancellationTokenSource();
        var roots = BuildRoots(modList);
        var mods = roots.SelectMany(FlattenEntries).ToList();

        HookDomainSync(roots);
        if (expandedPaths is not null)
        {
            ApplyExpansionPaths(roots, expandedPaths);
        }
        var model = new HierarchicalModel<TreeNodeViewModel>(
            new HierarchicalOptions<TreeNodeViewModel>
            {
                ChildrenSelector = node => node.Children,
                IsLeafSelector = node => !node.HasChildren,
                VirtualizeChildren = true,
                ExpandedStateKeyMode = ExpandedStateKeyMode.Item,
                IsExpandedSelector = node => node.IsExpanded,
                IsExpandedSetter = (node, expanded) => node.IsExpanded = expanded,
            }
        );
        model.SetRoots(roots);
        model.ApplySiblingComparer(BuildComparer(SortingModel.Descriptors), recursive: true);
        _ = LoadAllContentTreesAsync(mods, model, _loadContentCts.Token);
        return model;
    }

    private static ObservableCollection<TreeNodeViewModel> BuildRoots(IModList modList) =>
        new(
            modList
                .LooseMods.Select(mod => (TreeNodeViewModel)new ModEntryNodeViewModel(mod))
                .Concat(modList.ModGroups.Select(group => new GroupHeaderNodeViewModel(group)))
        );

    private void HookDomainSync(ObservableCollection<TreeNodeViewModel> roots)
    {
        _roots = roots;
        _syncHooks.Dispose();
        _syncHooks = new CompositeDisposable();
        _nodeActionHooks.Dispose();
        _nodeActionHooks = new CompositeDisposable();
        _syncHooks.Add(StreamOf(roots).Subscribe(_ => _structureChanged.OnNext(Unit.Default)));
        foreach (var node in roots)
        {
            switch (node)
            {
                case GroupHeaderNodeViewModel group:
                    HookGroupNode(group);
                    break;
                case ModEntryNodeViewModel entry:
                    HookModNode(entry);
                    break;
            }
        }
    }

    private static IObservable<EventPattern<NotifyCollectionChangedEventArgs>> StreamOf(
        ObservableCollection<TreeNodeViewModel> collection
    ) =>
        Observable.FromEventPattern<
            NotifyCollectionChangedEventHandler,
            NotifyCollectionChangedEventArgs
        >(
            handler => collection.CollectionChanged += handler,
            handler => collection.CollectionChanged -= handler
        );

    private void HookModNode(ModEntryNodeViewModel node)
    {
        var uiContext = _uiContext ?? SynchronizationContext.Current!;
        _syncHooks.Add(node.RenameCommitted.Subscribe(_ => _structureChanged.OnNext(Unit.Default)));
        _nodeActionHooks.Add(
            node.RemoveRequested.Select(_ => node)
                .ObserveOn(uiContext)
                .Subscribe(target => _ = RemoveModNodeAsync(target))
        );
        _nodeActionHooks.Add(
            node.WhenAnyValue(n => n.Active)
                .Skip(1)
                .ObserveOn(TaskPoolScheduler.Default)
                .Subscribe(_ => PersistMod(node))
        );
        _nodeActionHooks.Add(
            node.WhenAnyValue(n => n.PriorityValue)
                .Skip(1)
                .ObserveOn(TaskPoolScheduler.Default)
                .Subscribe(_ => PersistMod(node))
        );
    }

    private void HookGroupNode(GroupHeaderNodeViewModel group)
    {
        var uiContext = _uiContext ?? SynchronizationContext.Current!;
        _syncHooks.Add(
            StreamOf(group.ObservableChildren)
                .Subscribe(_ => _structureChanged.OnNext(Unit.Default))
        );
        _nodeActionHooks.Add(
            group
                .DissolveRequested.Select(_ => group)
                .ObserveOn(uiContext)
                .Subscribe(target => _ = DissolveGroupNodeAsync(target))
        );
        foreach (var child in group.ObservableChildren.OfType<ModEntryNodeViewModel>())
        {
            HookModNode(child);
        }
    }

    private void PersistMod(ModEntryNodeViewModel node)
    {
        try
        {
            _modSerializer.Save(node.Model);
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }
    }

    private async Task OpenFileNode(FileLeafNodeViewModel node)
    {
        try
        {
            var processStartInfo = new ProcessStartInfo
            {
                FileName = node.AbsolutePath,
                UseShellExecute = true,
                WorkingDirectory = Path.GetDirectoryName(node.AbsolutePath),
            };
            Process.Start(processStartInfo);
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }
        await Task.CompletedTask;
    }

    private async Task SyncDomainFromTreeAsync()
    {
        if (ActiveProfile is null || _roots is null)
        {
            return;
        }

        var profile = ActiveProfile.Model;
        var roots = _roots;
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
        if (ActiveProfile is null || _roots is null)
        {
            return;
        }
        var targets =
            SelectedNodes.Contains(node) && SelectedNodes.Count > 1
                ? SelectedNodes.OfType<ModEntryNodeViewModel>().ToList()
                : [node];
        try
        {
            if (!await ConfirmRemoveMod.Handle(targets[0]))
            {
                return;
            }
            foreach (var target in targets)
            {
                await RemoveSingleModNodeAsync(target);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }
    }

    private async Task RemoveSingleModNodeAsync(ModEntryNodeViewModel node)
    {
        await _editor.RemoveModAsync(ActiveProfile!.Model, node.Model);
        if (_roots!.Remove(node))
        {
            return;
        }
        foreach (var group in _roots.OfType<GroupHeaderNodeViewModel>())
        {
            if (group.ObservableChildren.Remove(node))
            {
                return;
            }
        }
    }

    private async Task DissolveGroupNodeAsync(GroupHeaderNodeViewModel node)
    {
        if (ActiveProfile is null || _roots is null)
        {
            return;
        }
        try
        {
            await _editor.DissolveGroupAsync(ActiveProfile.Model, node.Group);
            var index = _roots.IndexOf(node);
            if (index < 0)
            {
                return;
            }
            var children = node.ObservableChildren.ToList();
            _roots.RemoveAt(index);
            if (index < _roots.Count && _roots[index] is GroupHeaderNodeViewModel nextGroup)
            {
                foreach (var child in children)
                {
                    nextGroup.ObservableChildren.Add(child);
                }
                Model?.Refresh();
                Model?.Expand([nextGroup]);
            }
            else
            {
                foreach (var child in children)
                {
                    _roots.Insert(index++, child);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }
    }

    private static HashSet<string> CollectExpandedPaths(
        IEnumerable<TreeNodeViewModel> nodes,
        string prefix = ""
    )
    {
        var paths = new HashSet<string>();
        CollectInto(nodes, paths);
        return paths;

        static void CollectInto(
            IEnumerable<TreeNodeViewModel> level,
            HashSet<string> paths,
            string prefix = ""
        )
        {
            foreach (var node in level)
            {
                var path = prefix.Length == 0 ? node.DisplayName : $"{prefix}/{node.DisplayName}";
                if (!node.IsExpanded)
                {
                    continue;
                }
                paths.Add(path);
                CollectInto(node.Children, paths, path);
            }
        }
    }

    private static void ApplyExpansionPaths(
        IEnumerable<TreeNodeViewModel> nodes,
        HashSet<string> expanded,
        string prefix = ""
    )
    {
        foreach (var node in nodes)
        {
            var path = prefix.Length == 0 ? node.DisplayName : $"{prefix}/{node.DisplayName}";
            if (!expanded.Contains(path))
            {
                continue;
            }
            node.IsExpanded = true;
            ApplyExpansionPaths(node.Children, expanded, path);
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
}
