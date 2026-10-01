using System.Reactive.Linq;
using Ariadne.Contracts.Games;
using Ariadne.Contracts.ModManager;
using Ariadne.ModManager.GUI.ViewModels;
using Xunit;

namespace Ariadne.ModManager.GUI.Tests;

public class LoadOrderViewModelTests
{
    private sealed class FakeLoadOrderBuilder(IReadOnlyList<ILoadOrderInfo> rows)
        : ILoadOrderBuilder
    {
        public int SaveCalls { get; private set; }
        public int FetchCalls { get; private set; }
        public IReadOnlyList<ILoadOrderInfo>? LastSaved { get; private set; }
        public IReadOnlyList<ILoadOrderInfo> Rows { get; set; } = rows;
        public TaskCompletionSource SaveRequested { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public IEnumerable<ILoadOrderInfo> Fetch(IInstalledGame game, IModList mods)
        {
            FetchCalls++;
            return Rows;
        }

        public void Deploy(
            IInstalledGame game,
            IModDeploymentMethod deploymentMethod,
            IReadOnlyList<ILoadOrderInfo> loadOrderInfos
        ) { }

        public void Save(IModProfile currentProfile, IReadOnlyList<ILoadOrderInfo> loadOrderInfos)
        {
            SaveCalls++;
            LastSaved = loadOrderInfos;
            SaveRequested.TrySetResult();
        }

        public IEnumerable<ILoadOrderInfo> Sort(
            IModProfile currentProfile,
            IEnumerable<ILoadOrderInfo> loadOrderInfos
        ) => loadOrderInfos;
    }

    private sealed record FakeLoadOrderInfo(string Name) : ILoadOrderInfo
    {
        public IModInfo Origin { get; } = null!;
        public IReadOnlyList<FileInfo> Artifacts { get; } = [];
        public IReadOnlyList<ILoadOrderInfo> Dependencies { get; } = [];
        public bool Active { get; set; } = true;
    }

    private sealed class FakeInstanceService(IInstalledGame? game) : IInstanceService
    {
        public IReadOnlyDictionary<string, DirectoryInfo> Instances =>
            new Dictionary<string, DirectoryInfo>();
        public CurrentInstance? Current => new("default", new DirectoryInfo("."), game);

        public DirectoryInfo Create(string name, DirectoryInfo folder, IInstalledGame g) => folder;

        public void Switch(string name) { }

        public void Remove(string name, bool deleteFolder) { }

        public IInstalledGame? ResolveGame(string instanceName) => game;
    }

    private sealed class FakeGameConfiguration(string name) : ISupportedGame
    {
        public string Name => name;
        public IVendorInfo Vendors => null!;
        public IReadOnlyDictionary<string, string> ProtocolGameIds =>
            new Dictionary<string, string>();
        public IGamePath Root => null!;
        public IReadOnlyList<IGamePath> Deployments => [];
        public IReadOnlyList<IGamePath> InstallTargets => [];
        public IReadOnlyDictionary<string, int> LaunchTargets => new Dictionary<string, int>();
        public IGamePath this[string key] => throw new KeyNotFoundException();
        public IEnumerable<string> Keys => [];
        public IEnumerable<IGamePath> Values => [];
        public int Count => 0;

        public bool ContainsKey(string key) => false;

        public bool TryGetValue(string key, out IGamePath value)
        {
            value = null!;
            return false;
        }

        public IEnumerator<KeyValuePair<string, IGamePath>> GetEnumerator() =>
            Enumerable.Empty<KeyValuePair<string, IGamePath>>().GetEnumerator();

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() =>
            GetEnumerator();
    }

    private sealed class FakeGame(ISupportedGame configuration) : IInstalledGame
    {
        public DirectoryInfo InstallPath => new(".");
        public ISupportedGame Configuration => configuration;

        public string LookupAbsolutePath(IGamePath path) => path.Key;

        public string UpdateAbsolutePath(IGamePath path) => path.Key;
    }

    private static ModProfile FakeProfile() =>
        new("Default", new ModList([], []), new Version(1, 0), new DirectoryInfo("."));

    [Fact]
    public async Task NoGame_ShowsError_AndDoesNotSave()
    {
        var builder = new FakeLoadOrderBuilder([]);
        var vm = new LoadOrderViewModel(FakeProfile(), builder, new FakeInstanceService(null));

        await vm.EnsureInitializedAsync();

        Assert.Equal("Game installation could not be located.", vm.StatusText);
        Assert.Empty(vm.LoadOrder);
        Assert.Equal(0, builder.SaveCalls);
    }

    [Fact]
    public async Task Initialized_LoadsRows_InFetchOrder()
    {
        var builder = new FakeLoadOrderBuilder(
            [new FakeLoadOrderInfo("b.esp"), new FakeLoadOrderInfo("a.esp")]
        );
        var vm = new LoadOrderViewModel(
            FakeProfile(),
            builder,
            new FakeInstanceService(new FakeGame(new FakeGameConfiguration("Test Game")))
        );

        await vm.EnsureInitializedAsync();

        Assert.Null(vm.StatusText);
        Assert.Equal(new[] { "b.esp", "a.esp" }, vm.LoadOrder.Select(i => i.Name).ToList());
    }

    [Fact]
    public async Task TogglingActive_SavesAfterDebounce()
    {
        var builder = new FakeLoadOrderBuilder([new FakeLoadOrderInfo("a.esp")]);
        var vm = new LoadOrderViewModel(
            FakeProfile(),
            builder,
            new FakeInstanceService(new FakeGame(new FakeGameConfiguration("Test Game")))
        );

        await vm.EnsureInitializedAsync();
        vm.LoadOrder[0].Active = false;

        await builder.SaveRequested.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(1, builder.SaveCalls);
        Assert.False(builder.LastSaved![0].Active);
    }

    [Fact]
    public async Task RefreshAsync_RefetchesAndRebuildsRows()
    {
        var builder = new FakeLoadOrderBuilder([new FakeLoadOrderInfo("a.esp")]);
        var vm = new LoadOrderViewModel(
            FakeProfile(),
            builder,
            new FakeInstanceService(new FakeGame(new FakeGameConfiguration("Test Game")))
        );
        await vm.EnsureInitializedAsync();
        Assert.Equal(["a.esp"], vm.LoadOrder.Select(i => i.Name).ToArray());

        builder.Rows = [new FakeLoadOrderInfo("a.esp"), new FakeLoadOrderInfo("b.esp")];
        await vm.RefreshAsync();

        Assert.Equal(2, builder.FetchCalls);
        Assert.Equal(new[] { "a.esp", "b.esp" }, vm.LoadOrder.Select(i => i.Name).ToArray());
    }
}
