using System.Collections.ObjectModel;
using Daedalus.Contracts.ModManager;
using Daedalus.Contracts.Mods;
using ReactiveUI;

namespace Daedalus.ModManager.GUI.ViewModels;

public interface IWorkspaceTab
{
    string Title { get; }
    Task EnsureInitializedAsync();
}

public sealed class WorkspaceViewModel : ViewModelBase, IDisposable
{
    private readonly IDisposable[] _tabLifetime;
    private IWorkspaceTab? _selectedTab;

    public ModListViewModel ModList { get; }
    public ObservableCollection<IWorkspaceTab> SidePanelTabs { get; } = [];

    public IWorkspaceTab? SelectedTab
    {
        get => _selectedTab;
        set => this.RaiseAndSetIfChanged(ref _selectedTab, value);
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
        ModList = new ModListViewModel(profile, profileSerializer, modSerializer, paths, editor);
        var loadOrder = new LoadOrderViewModel(profile, loadOrderBuilder, instances);
        SidePanelTabs.Add(loadOrder);
        _tabLifetime = [loadOrder];

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
        ModList.Dispose();
        foreach (var disposable in _tabLifetime)
        {
            disposable.Dispose();
        }
    }
}
