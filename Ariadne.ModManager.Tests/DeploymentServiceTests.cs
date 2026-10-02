using Ariadne.Contracts.Games;
using Ariadne.Contracts.ModManager;
using Ariadne.VFS;
using Xunit;

namespace Ariadne.ModManager.Tests;

public class DeploymentServiceTests : IDisposable
{
    private sealed class FakeDeploymentMethod : IModDeploymentMethod
    {
        public int DeployCalls { get; private set; }
        public IInstalledGame? DeployedGame { get; private set; }
        public IReadOnlyList<ILibraryMod>? DeployedMods { get; private set; }
        public int RevertCalls { get; private set; }
        public bool Disposed { get; private set; }
        public bool ThrowOnDeploy { get; set; }

        public void SetOutputRules(List<OutputRule> outputRules) { }

        public void Deploy(IInstalledGame game, IReadOnlyList<ILibraryMod> mods)
        {
            DeployCalls++;
            if (ThrowOnDeploy)
            {
                throw new IOException("mount failed");
            }
            DeployedGame = game;
            DeployedMods = mods;
        }

        public void Revert(IInstalledGame game) => RevertCalls++;

        public void Dispose() => Disposed = true;
    }

    private sealed class FakeLoadOrderBuilder : ILoadOrderBuilder
    {
        public IReadOnlyList<ILoadOrderInfo>? DeployedLoadOrder { get; private set; }

        public IEnumerable<ILoadOrderInfo> Fetch(IInstalledGame game, IModList mods) => [];

        public void Deploy(
            IInstalledGame game,
            IModDeploymentMethod deploymentMethod,
            IReadOnlyList<ILoadOrderInfo> loadOrderInfos
        ) => DeployedLoadOrder = loadOrderInfos;

        public void Save(
            IModProfile currentProfile,
            IReadOnlyList<ILoadOrderInfo> loadOrderInfos
        ) { }

        public IEnumerable<ILoadOrderInfo> Sort(
            IModProfile currentProfile,
            IEnumerable<ILoadOrderInfo> loadOrderInfos
        ) => loadOrderInfos;
    }

    private sealed class FakeMod(string name, bool active) : ILibraryMod
    {
        public bool IntendedActive { get; } = active;
        public IModInfo Info { get; } = new ModInfo(0, SourceType.Local, "1.0", [], "Data");
        public string Name { get; } = name;
        public DirectoryInfo Directory => new(".");
        public VirtualNode<ModFileEntry> Content { get; } =
            new(name, NodeFlags.Directory, null, default);

        public void RefreshContent() { }

        public void ReplaceInfo(IModInfo info) { }

        public void RenameTo(string newName) { }

        public bool Equals(ILibraryMod? x, ILibraryMod? y) => ReferenceEquals(x, y);

        public int GetHashCode(ILibraryMod obj) => 0;

        public bool Equals(ILibraryMod? other) => ReferenceEquals(this, other);
    }

    private sealed class FakeInstances(IInstalledGame? game) : IInstanceService
    {
        public IReadOnlyDictionary<string, DirectoryInfo> Instances =>
            new Dictionary<string, DirectoryInfo>();
        public CurrentInstance? Current { get; } =
            game is null ? null : new("main", new DirectoryInfo("."), game);

        public DirectoryInfo Create(string name, DirectoryInfo folder, IInstalledGame g) => folder;

        public void Switch(string name) { }

        public void Remove(string name, bool deleteFolder) { }

        public IInstalledGame? ResolveGame(string instanceName) => game;
    }

    private sealed class Harness
    {
        public FakeDeploymentMethod Method { get; } = new();
        public FakeLoadOrderBuilder LoadOrderBuilder { get; } = new();
        public DeploymentService Service { get; }
        public ModProfile Profile { get; }

        public Harness(IInstalledGame? game, params FakeMod[] mods)
        {
            Profile = new ModProfile(
                "P",
                new ModList(
                    mods.Select(m => (IModListEntry)new ModListEntry(m, m.IntendedActive)).ToList(),
                    []
                ),
                new Version(1, 0),
                new DirectoryInfo(".")
            );
            Service = new DeploymentService(
                new FakeInstances(game),
                new ModDeploymentMethodFactory(_ => Method),
                LoadOrderBuilder
            );
        }
    }

    private readonly List<TempDirectory> _temps = [];

    public void Dispose()
    {
        foreach (var temp in _temps)
        {
            temp.Dispose();
        }
    }

    private IInstalledGame Game()
    {
        var temp = new TempDirectory();
        _temps.Add(temp);
        return TestAssets.GameAt(new DirectoryInfo(temp.Path));
    }

    [Fact]
    public async Task DeployAsync_DeploysActiveModsAndLoadOrder()
    {
        var active = new FakeMod("Active", true);
        var inactive = new FakeMod("Inactive", false);
        var harness = new Harness(Game(), active, inactive);
        var changed = 0;
        harness.Service.DeploymentChanged += (_, _) => changed++;
        ILoadOrderInfo[] loadOrder = [];

        await harness.Service.DeployAsync(harness.Profile, loadOrder);

        Assert.True(harness.Service.IsDeployed);
        Assert.Equal(1, changed);
        var deployed = Assert.Single(harness.Method.DeployedMods!);
        Assert.Same(active, deployed);
        Assert.Same(loadOrder, harness.LoadOrderBuilder.DeployedLoadOrder);
    }

    [Fact]
    public async Task DeployAsync_WithoutInstanceGame_IsNoOp()
    {
        var harness = new Harness(null, new FakeMod("A", true));

        await harness.Service.DeployAsync(harness.Profile, []);

        Assert.False(harness.Service.IsDeployed);
        Assert.Null(harness.Method.DeployedMods);
    }

    [Fact]
    public async Task DeployAsync_WhenAlreadyDeployed_IsNoOp()
    {
        var harness = new Harness(Game(), new FakeMod("A", true));
        await harness.Service.DeployAsync(harness.Profile, []);

        await harness.Service.DeployAsync(harness.Profile, []);

        Assert.Equal(1, harness.Method.DeployCalls);
        Assert.True(harness.Service.IsDeployed);
    }

    [Fact]
    public async Task DeployAsync_Failure_DisposesMethodAndRethrows()
    {
        var harness = new Harness(Game(), new FakeMod("A", true));
        harness.Method.ThrowOnDeploy = true;

        await Assert.ThrowsAsync<IOException>(() =>
            harness.Service.DeployAsync(harness.Profile, [])
        );

        Assert.True(harness.Method.Disposed);
        Assert.False(harness.Service.IsDeployed);
    }

    [Fact]
    public async Task Undeploy_RevertsDisposesAndSignals()
    {
        var harness = new Harness(Game(), new FakeMod("A", true));
        await harness.Service.DeployAsync(harness.Profile, []);
        var changed = 0;
        harness.Service.DeploymentChanged += (_, _) => changed++;

        await harness.Service.UndeployAsync();

        Assert.False(harness.Service.IsDeployed);
        Assert.Equal(1, harness.Method.RevertCalls);
        Assert.True(harness.Method.Disposed);
        Assert.Equal(1, changed);
    }

    [Fact]
    public async Task Undeploy_WithoutDeploy_IsNoOp()
    {
        var harness = new Harness(Game());

        await harness.Service.UndeployAsync();

        Assert.False(harness.Service.IsDeployed);
        Assert.False(harness.Method.Disposed);
    }
}
