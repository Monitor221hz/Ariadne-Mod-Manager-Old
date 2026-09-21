using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Reactive.Linq;
using Avalonia.Threading;
using CP.Reactive.Collections;
using Daedalus.Contracts.ModManager;
using Daedalus.Contracts.Mods;
using Daedalus.VFS;
using ReactiveUI;
using ReactiveUI.Avalonia;

namespace Daedalus.ModManager.GUI.ViewModels;

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
    private readonly IDisposable[] _tabLifetime;
    private IWorkspaceTab? _selectedTab;
    private ILibraryMod? _selectedMod;
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
        IModProfileSerializer profileSerializer,
        ILibraryModSerializer modSerializer,
        IModManagerPaths paths,
        IModProfileEditor editor,
        ILoadOrderBuilder loadOrderBuilder,
        IInstanceService instances
    )
    {
        _profile = profile;
        ModList = new ModListViewModel(profile, profileSerializer, modSerializer, paths, editor);
        var loadOrder = new LoadOrderViewModel(profile, loadOrderBuilder, instances);
        SidePanelTabs.Add(loadOrder);
        _tabLifetime = [loadOrder];

        ModList.SelectedNodes.CollectionChanged += OnModSelectionChanged;
        this.WhenAnyValue(x => x.SelectedMod)
            .ObserveOn(AvaloniaScheduler.Instance)
            .Subscribe(mod => _ = ComputeVerdictsAsync(mod));
        this.WhenAnyValue(x => x.Verdicts).Subscribe(ModList.ApplyVerdicts);

        this.WhenAnyValue(x => x.SelectedTab)
            .WhereNotNull()
            .Subscribe(tab => _ = tab.EnsureInitializedAsync());
        SelectedTab = loadOrder;
    }

    public async Task InitializeAsync()
    {
        await ModList.InitializeAsync();
    }

    public void Dispose()
    {
        ModList.SelectedNodes.CollectionChanged -= OnModSelectionChanged;
        ModList.Dispose();
        foreach (var disposable in _tabLifetime)
        {
            disposable.Dispose();
        }
    }

    private void OnModSelectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        var modEntries = ModList.SelectedNodes.OfType<ModEntryNodeViewModel>().Take(2).ToList();
        SelectedMod = modEntries.Count == 1 ? modEntries[0].Model : null;
    }

    private async Task ComputeVerdictsAsync(ILibraryMod? selected)
    {
        if (selected is null || !selected.Info.Active)
        {
            Verdicts = new Dictionary<ILibraryMod, SelectedModVerdict>();
            return;
        }
        var mods = _profile
            .ModList.Where(m => m.Info.Active)
            .OrderBy(m => m.Info.Priority)
            .ToList();
        int focusIndex = mods.IndexOf(selected);
        if (focusIndex < 0 || mods.Count < 2)
        {
            Verdicts = new Dictionary<ILibraryMod, SelectedModVerdict>();
            return;
        }
        try
        {
            var verdicts = await Task.Run(() =>
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
            });
            if (!ReferenceEquals(SelectedMod, selected))
            {
                return;
            }
            Verdicts = verdicts;
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }
    }
}
