using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Ariadne.Contracts.ModManager;
using Ariadne.Downloads;
using Ariadne.VFS;
using Avalonia.Threading;
using CP.Reactive.Collections;
using ReactiveUI;
using ReactiveUI.Avalonia;

namespace Ariadne.ModManager.GUI.ViewModels;

public enum SelectedModVerdict
{
    LosesToSelected,
    BeatsSelected,
}

public interface IWorkspaceTab
{
    string Title { get; }
    Task EnsureInitializedAsync();
}

public sealed class WorkspaceViewModel : ViewModelBase, IDisposable
{
    private readonly IModProfile _profile;
    private readonly IDeploymentService? _deploymentService;
    private readonly IDisposable[] _tabLifetime;
    private readonly LoadOrderViewModel _loadOrderTab;
    private readonly DeployedViewModel _deployedTab;
    private readonly DownloadListViewModel? _downloadTab;
    private readonly CompositeDisposable _subscriptions = new();
    private IReadOnlyDictionary<
        IModInfo,
        IReadOnlyList<LoadOrderInfoViewModel>
    >? _pluginRowsByOrigin;
    private IReadOnlyDictionary<IModInfo, ModEntryNodeViewModel>? _modRowByInfo;
    private IWorkspaceTab? _selectedTab;
    private bool _applyingSelection;
    private ILibraryMod? _selectedMod;
    private bool _isDeployed;
    private string? _deploymentBusyText;
    private readonly Subject<(string Title, string Text)> _deploymentFailed = new();
    private IReadOnlyDictionary<ILibraryMod, SelectedModVerdict> _verdicts =
        new Dictionary<ILibraryMod, SelectedModVerdict>();

    public ModListViewModel ModList { get; }
    public ObservableCollection<IWorkspaceTab> SidePanelTabs { get; } = [];

    public IWorkspaceTab? SelectedTab
    {
        get => _selectedTab;
        set => this.RaiseAndSetIfChanged(ref _selectedTab, value);
    }

    public ILibraryMod? SelectedMod
    {
        get => _selectedMod;
        set => this.RaiseAndSetIfChanged(ref _selectedMod, value);
    }

    public IReadOnlyDictionary<ILibraryMod, SelectedModVerdict> Verdicts
    {
        get => _verdicts;
        private set => this.RaiseAndSetIfChanged(ref _verdicts, value);
    }

    public WorkspaceViewModel(
        IModProfile profile,
        ILibraryModFactory modFactory,
        IModProfileSerializer profileSerializer,
        ILibraryModSerializer modSerializer,
        IModManagerPaths paths,
        IModProfileEditor editor,
        ILoadOrderBuilder loadOrderBuilder,
        IInstanceService instances,
        IDownloadQueue? downloads = null,
        IModInstallService? installService = null,
        IDeploymentService? deploymentService = null,
        IScheduler? notifyScheduler = null
    )
    {
        _profile = profile;
        _deploymentService = deploymentService;
        ModList = new ModListViewModel(
            profile,
            modFactory,
            profileSerializer,
            modSerializer,
            paths,
            editor,
            instances,
            notifyScheduler
        );
        _loadOrderTab = new LoadOrderViewModel(profile, loadOrderBuilder, instances);
        _deployedTab = new DeployedViewModel(profile, instances);
        if (downloads is not null)
        {
            _downloadTab = new DownloadListViewModel(
                downloads,
                paths,
                instances,
                installService,
                mod => ModList.RegisterMod(mod)
            );
        }
        SidePanelTabs.Add(_loadOrderTab);
        SidePanelTabs.Add(_deployedTab);
        if (_downloadTab is not null)
        {
            SidePanelTabs.Add(_downloadTab);
        }
        _tabLifetime = [_loadOrderTab];

        ModList.SelectedNodes.CollectionChanged += OnModSelectionChanged;
        _loadOrderTab.SelectedNodes.CollectionChanged += OnPluginSelectionChanged;
        _subscriptions.Add(ModList.StructureChanged.Subscribe(_ => _modRowByInfo = null));
        _subscriptions.Add(
            _loadOrderTab.LoadOrder.Stream.Subscribe(_ => _pluginRowsByOrigin = null)
        );
        _subscriptions.Add(
            ModList
                .DomainSynchronized.ObserveOn(notifyScheduler ?? AvaloniaScheduler.Instance)
                .Subscribe(signal =>
                {
                    RefreshDeployed();
                    _ = _loadOrderTab.RefreshAsync();
                })
        );
        _subscriptions.Add(
            ModList
                .ActiveChanged.ObserveOn(notifyScheduler ?? AvaloniaScheduler.Instance)
                .Subscribe(signal =>
                {
                    RefreshDeployed();
                    _ = _loadOrderTab.RefreshAsync();
                })
        );
        _subscriptions.Add(
            ModList
                .TargetChanged.ObserveOn(notifyScheduler ?? AvaloniaScheduler.Instance)
                .Subscribe(_ => RefreshDeployed())
        );
        _subscriptions.Add(
            ModList
                .Renamed.ObserveOn(notifyScheduler ?? AvaloniaScheduler.Instance)
                .Subscribe(_ => RefreshDeployed())
        );
        _subscriptions.Add(
            this.WhenAnyValue(x => x.SelectedMod)
                .Select(mod => Observable.FromAsync(() => ComputeVerdictsAsync(mod)))
                .Switch()
                .Subscribe(verdicts => Verdicts = verdicts)
        );
        _subscriptions.Add(this.WhenAnyValue(x => x.Verdicts).Subscribe(ModList.ApplyVerdicts));

        _subscriptions.Add(
            this.WhenAnyValue(x => x.SelectedTab)
                .WhereNotNull()
                .Subscribe(tab => _ = tab.EnsureInitializedAsync())
        );
        SelectedTab = _loadOrderTab;

        if (deploymentService is not null)
        {
            _isDeployed = deploymentService.IsDeployed;
            ModList.IsDeployed = deploymentService.IsDeployed;
            ToggleDeploymentCommand = ReactiveCommand.CreateFromTask(
                ToggleDeploymentAsync,
                this.WhenAnyValue(x => x.IsDeploymentBusy).Select(busy => !busy)
            );
            _subscriptions.Add(
                Observable
                    .FromEventPattern(
                        handler => deploymentService.DeploymentChanged += handler,
                        handler => deploymentService.DeploymentChanged -= handler
                    )
                    .ObserveOn(notifyScheduler ?? AvaloniaScheduler.Instance)
                    .Subscribe(_ =>
                    {
                        IsDeployed = deploymentService.IsDeployed;
                        ModList.IsDeployed = IsDeployed;
                    })
            );
        }
        else
        {
            ToggleDeploymentCommand = ReactiveCommand.Create(() => { });
        }
    }

    public bool IsDeployed
    {
        get => _isDeployed;
        private set
        {
            this.RaiseAndSetIfChanged(ref _isDeployed, value);
            this.RaisePropertyChanged(nameof(DeployText));
        }
    }

    public bool IsDeploymentBusy => _deploymentBusyText is not null;

    private string? DeploymentBusyText
    {
        get => _deploymentBusyText;
        set
        {
            this.RaiseAndSetIfChanged(ref _deploymentBusyText, value);
            this.RaisePropertyChanged(nameof(IsDeploymentBusy));
            this.RaisePropertyChanged(nameof(DeployText));
        }
    }

    public string DeployText => _deploymentBusyText ?? (IsDeployed ? "Undeploy" : "Deploy");

    public ReactiveCommand<Unit, Unit> ToggleDeploymentCommand { get; }

    public IObservable<(string Title, string Text)> DeploymentFailed => _deploymentFailed;

    private async Task ToggleDeploymentAsync()
    {
        if (_deploymentService is null)
        {
            return;
        }
        var undeploying = _deploymentService.IsDeployed;
        DeploymentBusyText = undeploying ? "Undeploying…" : "Deploying…";
        try
        {
            if (undeploying)
            {
                await _deploymentService.UndeployAsync();
            }
            else
            {
                var loadOrder = _loadOrderTab.LoadOrder.Select(row => row.Model).ToList();
                await _deploymentService.DeployAsync(_profile, loadOrder);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
            _deploymentFailed.OnNext(
                (undeploying ? "Undeploy failed" : "Deployment failed", ex.Message)
            );
        }
        finally
        {
            DeploymentBusyText = null;
        }
    }

    public async Task InitializeAsync()
    {
        await ModList.InitializeAsync();
    }

    public void Dispose()
    {
        if (_deploymentService?.IsDeployed == true)
        {
            _deploymentService.UndeployAsync().GetAwaiter().GetResult();
        }
        ModList.SelectedNodes.CollectionChanged -= OnModSelectionChanged;
        _loadOrderTab.SelectedNodes.CollectionChanged -= OnPluginSelectionChanged;
        _subscriptions.Dispose();
        ModList.Dispose();
        foreach (var disposable in _tabLifetime)
        {
            disposable.Dispose();
        }
    }

    private void OnModSelectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        var selected = ModList.SelectedNodes.OfType<ModEntryNodeViewModel>().ToList();
        SelectedMod = selected.Count == 1 ? selected[0].Model : null;
        if (_applyingSelection)
        {
            return;
        }
        var linkByOrigin = _pluginRowsByOrigin ??= WorkspaceLinkMaps.BuildPluginRowsByOrigin(
            _loadOrderTab.LoadOrder
        );
        var linked = selected
            .SelectMany(mod =>
                linkByOrigin.TryGetValue(mod.Model.Info, out var rows)
                    ? rows
                    : Enumerable.Empty<LoadOrderInfoViewModel>()
            )
            .Distinct()
            .ToList();
        if (SameSet(_loadOrderTab.SelectedNodes, linked))
        {
            return;
        }
        PropagateSelection(() => _loadOrderTab.RequestSelection(linked));
    }

    private void OnPluginSelectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_applyingSelection)
        {
            return;
        }
        var selected = _loadOrderTab.SelectedNodes.OfType<LoadOrderInfoViewModel>().ToList();
        var modByInfo = _modRowByInfo ??= WorkspaceLinkMaps.BuildModRowByInfo(
            ModList.EnumerateModEntries()
        );
        var linked = selected
            .Select(row => modByInfo.TryGetValue(row.Model.Origin, out var modRow) ? modRow : null)
            .Where(row => row != null)
            .Cast<TreeNodeViewModel>()
            .Distinct()
            .ToList();
        if (SameSet(ModList.SelectedNodes, linked))
        {
            return;
        }
        PropagateSelection(() => ModList.RequestSelection(linked));
    }

    private static bool SameSet<T>(IEnumerable<T> current, IEnumerable<T> targets)
        where T : class
    {
        if (
            current is IReadOnlyCollection<T> c
            && targets is IReadOnlyCollection<T> t
            && c.Count != t.Count
        )
        {
            return false;
        }
        return new HashSet<T>(current, ReferenceEqualityComparer.Instance).SetEquals(targets);
    }

    private void RefreshDeployed()
    {
        _ = _deployedTab.RefreshAsync();
    }

    private void PropagateSelection(Action apply)
    {
        _applyingSelection = true;
        try
        {
            apply();
        }
        finally
        {
            _applyingSelection = false;
        }
    }

    private static readonly IReadOnlyDictionary<ILibraryMod, SelectedModVerdict> EmptyVerdicts =
        new Dictionary<ILibraryMod, SelectedModVerdict>();

    private Task<IReadOnlyDictionary<ILibraryMod, SelectedModVerdict>> ComputeVerdictsAsync(
        ILibraryMod? selected
    )
    {
        if (selected is null)
        {
            return Task.FromResult(EmptyVerdicts);
        }
        var mods = _profile
            .ModList.Where(entry => entry.Active)
            .Select(entry => entry.Mod)
            .ToList();
        int focusIndex = mods.IndexOf(selected);
        if (focusIndex < 0 || mods.Count < 2)
        {
            return Task.FromResult(EmptyVerdicts);
        }
        return Task.Run<IReadOnlyDictionary<ILibraryMod, SelectedModVerdict>>(() =>
        {
            try
            {
                var trees = mods.Select(m => m.Content).ToList();
                var conflicts = ConflictMapper<ModFileEntry>.MapConflictsFor(trees, focusIndex);
                var result = new Dictionary<ILibraryMod, SelectedModVerdict>();
                foreach (var conflict in conflicts)
                {
                    foreach (var provider in conflict.Providers)
                    {
                        if (provider.Index == focusIndex)
                        {
                            continue;
                        }
                        result[mods[provider.Index]] =
                            provider.Index < focusIndex
                                ? SelectedModVerdict.LosesToSelected
                                : SelectedModVerdict.BeatsSelected;
                    }
                }
                return result;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                return EmptyVerdicts;
            }
        });
    }
}
