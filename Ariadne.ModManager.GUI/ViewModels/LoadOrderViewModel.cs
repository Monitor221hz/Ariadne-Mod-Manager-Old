using System.Diagnostics;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Ariadne.Contracts.ModManager;
using Ariadne.ModManager.GUI.DragDrop;
using Avalonia.Controls.DataGridDragDrop;
using CP.Reactive.Collections;
using ReactiveUI;
using ReactiveUI.Avalonia;

namespace Ariadne.ModManager.GUI.ViewModels;

public sealed class LoadOrderViewModel : ViewModelBase, IWorkspaceTab, IDisposable
{
    private static readonly TimeSpan SyncDelay = TimeSpan.FromMilliseconds(300);
    private readonly IModProfile _profile;
    private readonly ILoadOrderBuilderResolver _loadOrderBuilders;
    private readonly IInstanceService _instanceService;
    private ILoadOrderBuilder? _builder;
    private readonly CompositeDisposable _syncHooks = new();
    private CompositeDisposable _rowHooks = new();
    private readonly ReactiveList<LoadOrderInfoViewModel> _loadOrder = new();
    private Task? _initTask;
    private bool _initialized;
    private string? _statusText;

    public string Title => "Load Order";
    public ReactiveList<LoadOrderInfoViewModel> LoadOrder => _loadOrder;
    public IDataGridRowDropHandler DropHandler { get; }
    public event Action<IReadOnlyList<LoadOrderInfoViewModel>>? SelectionRequested;

    public void RequestSelection(IReadOnlyList<LoadOrderInfoViewModel> rows) =>
        SelectionRequested?.Invoke(rows);

    public ReactiveList<LoadOrderInfoViewModel> SelectedNodes { get; } = [];
    public string? StatusText
    {
        get => _statusText;
        private set => this.RaiseAndSetIfChanged(ref _statusText, value);
    }

    public LoadOrderViewModel(
        IModProfile profile,
        ILoadOrderBuilderResolver loadOrderBuilders,
        IInstanceService instanceService
    )
    {
        _loadOrderBuilders = loadOrderBuilders;
        _profile = profile;
        _instanceService = instanceService;
        var orderSubscription = _loadOrder
            .Stream.Throttle(SyncDelay)
            .ObserveOn(TaskPoolScheduler.Default)
            .Subscribe(signal => _ = SyncLoadOrderAsync());

        _syncHooks.Add(orderSubscription);
        DropHandler = new LoadOrderRowDropHandler(() => _loadOrder);
    }

    public Task EnsureInitializedAsync() => _initTask ??= InitializeAsync();

    public Task RefreshAsync() => _initialized ? InitializeAsync() : Task.CompletedTask;

    private void HookLoadOrderInfo(LoadOrderInfoViewModel vm)
    {
        var activeSubscription = vm.WhenAnyValue(v => v.Active)
            .Skip(1)
            .Throttle(SyncDelay)
            .ObserveOn(TaskPoolScheduler.Default)
            .Subscribe(signal => _ = SyncLoadOrderAsync());
        _rowHooks.Add(activeSubscription);
    }

    private async Task InitializeAsync()
    {
        var game = _instanceService.Current?.Game;
        if (game == null)
        {
            StatusText = "Game installation could not be located.";
            return;
        }
        var builder = _loadOrderBuilders.GetFor(game.Configuration);
        if (builder is null)
        {
            StatusText = "This game does not use a plugin load order.";
            return;
        }
        _builder = builder;
        try
        {
            var infos = await Task.Run(() => builder.Fetch(game, _profile.ModList).ToList());
            var sorted = builder.Sort(_profile, infos);
            _rowHooks.Dispose();
            _rowHooks = new CompositeDisposable();
            _loadOrder.Clear();
            _loadOrder.AddRange(sorted.Select(info => new LoadOrderInfoViewModel(info)));
            foreach (var loadOrderInfoViewModel in _loadOrder)
            {
                HookLoadOrderInfo(loadOrderInfoViewModel);
            }
            StatusText = null;
            _initialized = true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
            StatusText = "Failed to compute the plugin load order.";
        }
    }

    private async Task SyncLoadOrderAsync()
    {
        if (!_initialized || _builder is null)
        {
            return;
        }
        var builder = _builder;
        await Task.Run(() =>
        {
            try
            {
                builder.Save(_profile, _loadOrder.Select(vm => vm.Model).ToList());
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                StatusText = "Failed to save the plugin load order.";
            }
        });
    }

    public void Dispose()
    {
        _syncHooks.Dispose();
        _rowHooks.Dispose();
    }
}
