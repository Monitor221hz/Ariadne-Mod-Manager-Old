using System.Reactive.Threading.Tasks;
using Ariadne.Contracts.Games;
using Ariadne.Contracts.ModManager;
using Ariadne.ModManager;
using Ariadne.ModManager.GUI.ViewModels;
using Xunit;

namespace Ariadne.ModManager.GUI.Tests;

public sealed class WorkspaceViewModelTests
{
    private sealed class FakeMod(string name, uint priority, bool active = true) : ILibraryMod
    {
        public IModInfo Info { get; } =
            new ModManager.ModInfo(0, SourceType.Local, "1.0", [], "", priority, active);
        public string Name { get; private set; } = name;
        public DirectoryInfo Directory => new(".");
        public Ariadne.VFS.VirtualNode<ModFileEntry> Content { get; set; } =
            new("", Ariadne.VFS.NodeFlags.Directory, null, default);

        public void RefreshContent() { }

        public void RenameTo(string newName) => Name = newName;

        public bool Equals(ILibraryMod? x, ILibraryMod? y) => ReferenceEquals(x, y);

        public int GetHashCode(ILibraryMod obj) => obj.GetHashCode();

        public bool Equals(ILibraryMod? other) => ReferenceEquals(this, other);
    }

    private sealed record FakePlugin(string Name, IModInfo Origin) : ILoadOrderInfo
    {
        public IReadOnlyList<FileInfo> Artifacts { get; } = [];
        public IReadOnlyList<ILoadOrderInfo> Dependencies { get; } = [];
        public bool Active { get; set; } = true;
    }

    private sealed class FakeBuilder(IReadOnlyList<ILoadOrderInfo> rows) : ILoadOrderBuilder
    {
        public IEnumerable<ILoadOrderInfo> Fetch(IInstalledGame game, IModList mods) => rows;

        public void Deploy(
            IInstalledGame g,
            IModDeploymentMethod d,
            IReadOnlyList<ILoadOrderInfo> l
        ) { }

        public void Save(IModProfile p, IReadOnlyList<ILoadOrderInfo> rows) { }

        public IEnumerable<ILoadOrderInfo> Sort(IModProfile p, IEnumerable<ILoadOrderInfo> rows) =>
            rows;
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

    private sealed class FakeGame(ISupportedGame config) : IInstalledGame
    {
        public DirectoryInfo InstallPath => new(".");
        public ISupportedGame Configuration => config;

        public string LookupAbsolutePath(IGamePath path) => path.Key;

        public string UpdateAbsolutePath(IGamePath path) => path.Key;
    }

    private sealed class FakeInstances(IInstalledGame? game) : IInstanceService
    {
        public IReadOnlyDictionary<string, DirectoryInfo> Instances =>
            new Dictionary<string, DirectoryInfo>();
        public CurrentInstance? Current => new("default", new DirectoryInfo("."), game);

        public DirectoryInfo Create(string name, DirectoryInfo folder, IInstalledGame g) => folder;

        public void Switch(string name) { }

        public void Remove(string name, bool deleteFolder) { }

        public IInstalledGame? ResolveGame(string instanceName) => game;
    }

    private sealed class NullProfileSerializer : IModProfileSerializer
    {
        public IModProfile Load(FileInfo file) => throw new InvalidOperationException();

        public IModProfile Load(DirectoryInfo folder) => throw new InvalidOperationException();

        public void Save(IModProfile profile) { }
    }

    private sealed class NullModSerializer : ILibraryModSerializer
    {
        public ILibraryMod Load(FileInfo file) => throw new InvalidOperationException();

        public ILibraryMod Load(DirectoryInfo folder) => throw new InvalidOperationException();

        public void Save(ILibraryMod mod) { }
    }

    private sealed class NoEditor : IModProfileEditor
    {
        public Task AddModAsync(IModProfile profile, DirectoryInfo folder) => Task.CompletedTask;

        public Task RemoveModAsync(IModProfile profile, ILibraryMod mod) => Task.CompletedTask;

        public Task AddGroupAsync(IModProfile profile, IModGroup group) => Task.CompletedTask;

        public Task DissolveGroupAsync(IModProfile profile, IModGroup group) => Task.CompletedTask;
    }

    private sealed class FakeModFactory : ILibraryModFactory
    {
        public ILibraryMod Create(string name, IModInfo info) =>
            throw new InvalidOperationException();

        public ILibraryMod Create(IModInfo info) => throw new InvalidOperationException();

        public bool TryCreate(string name, IModInfo info, out ILibraryMod? mod)
        {
            mod = null;
            return false;
        }
    }

    private sealed class FakePaths : IModManagerPaths
    {
        public DirectoryInfo AssemblyFolder => new(".");
        public DirectoryInfo InstanceFolder => new(".");
        public DirectoryInfo StagingFolder => new(".");
        public DirectoryInfo ModsFolder => new(".");
        public DirectoryInfo ProfilesFolder => new(".");
        public DirectoryInfo TemporaryFolder => new(".");
        public DirectoryInfo DownloadsFolder => new(".");
    }

    private sealed class FakeDeploymentService : IDeploymentService
    {
        public bool IsDeployed { get; private set; }
        public IReadOnlyList<DirectoryInfo> DeployedPaths => [];
        public int DeployCalls { get; private set; }
        public int UndeployCalls { get; private set; }
        public IModProfile? LastProfile { get; private set; }
        public TaskCompletionSource? DeployGate { get; set; }
        public ManualResetEventSlim? UndeployGate { get; set; }
        public Exception? DeployError { get; set; }

        public event EventHandler? DeploymentChanged;

        public async Task DeployAsync(
            IModProfile profile,
            IReadOnlyList<ILoadOrderInfo> loadOrder,
            CancellationToken cancellationToken = default
        )
        {
            DeployCalls++;
            LastProfile = profile;
            if (DeployError is not null)
            {
                throw DeployError;
            }
            if (DeployGate is { } gate)
            {
                await gate.Task;
            }
            IsDeployed = true;
            DeploymentChanged?.Invoke(this, EventArgs.Empty);
        }

        public async Task UndeployAsync()
        {
            UndeployCalls++;
            if (UndeployGate is { } gate)
            {
                await Task.Run(() => gate.Wait(TimeSpan.FromSeconds(10)));
            }
            IsDeployed = false;
            DeploymentChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private sealed record Harness(
        WorkspaceViewModel Workspace,
        LoadOrderViewModel LoadOrder,
        List<IReadOnlyList<LoadOrderInfoViewModel>> PluginSelectionLog,
        List<IReadOnlyList<TreeNodeViewModel>> ModSelectionLog
    ) : IDisposable
    {
        public void Dispose() => Workspace.Dispose();
    }

    private static async Task<Harness> NewHarness(
        FakeMod[] mods,
        IReadOnlyList<ILoadOrderInfo> plugins,
        IDeploymentService? deployment = null
    )
    {
        var profile = new ModProfile(
            "P",
            new ModList([.. mods], []),
            new Version(1, 0),
            new DirectoryInfo(".")
        );
        var pluginLog = new List<IReadOnlyList<LoadOrderInfoViewModel>>();
        var modLog = new List<IReadOnlyList<TreeNodeViewModel>>();
        var ws = new WorkspaceViewModel(
            profile,
            new FakeModFactory(),
            new NullProfileSerializer(),
            new NullModSerializer(),
            new FakePaths(),
            new NoEditor(),
            new FakeBuilder(plugins),
            new FakeInstances(new FakeGame(new FakeGameConfiguration("G"))),
            deploymentService: deployment,
            notifyScheduler: System.Reactive.Concurrency.Scheduler.Immediate
        );
        var lo = ws.SidePanelTabs.OfType<LoadOrderViewModel>().Single();
        lo.SelectionRequested += rows => pluginLog.Add(rows);
        ws.ModList.SelectionRequested += rows => modLog.Add(rows);
        await ws.InitializeAsync();
        await via(lo).EnsureInitializedAsync();
        return new Harness(ws, lo, pluginLog, modLog);

        static LoadOrderViewModel via(LoadOrderViewModel vm) => vm;
    }

    [Fact]
    public async Task RegisterMod_SameDirectory_DoesNotDuplicate()
    {
        var mod = new FakeMod("WithPlugin", 1);
        using var h = await NewHarness([mod], []);

        var before = h.Workspace.ModList.EnumerateModEntries().Count();
        h.Workspace.ModList.RegisterMod(mod);
        var after = h.Workspace.ModList.EnumerateModEntries().Count();

        Assert.Equal(before, after);
    }

    [Fact]
    public async Task TickingMod_Active_RaisesActiveChanged_NotStructureChanged()
    {
        var mod = new FakeMod("Tickable", 1);
        using var h = await NewHarness([mod], []);

        var activeFired = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        var structureFired = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        h.Workspace.ModList.ActiveChanged.Subscribe(_ => activeFired.TrySetResult());
        h.Workspace.ModList.StructureChanged.Subscribe(_ => structureFired.TrySetResult());

        var entry = h
            .Workspace.ModList.EnumerateModEntries()
            .Single(e => e.DisplayName == "Tickable");
        entry.Active = false;

        Assert.True(activeFired.Task.Wait(TimeSpan.FromSeconds(5)));
        Assert.False(structureFired.Task.Wait(TimeSpan.FromMilliseconds(500)));
    }

    [Fact]
    public async Task Selecting_Mod_Raises_Linked_Plugins()
    {
        var mod = new FakeMod("WithPlugin", 1);
        var plugin = new FakePlugin("plugin.esp", mod.Info);
        using var h = await NewHarness([mod], [plugin]);

        h.Workspace.ModList.SelectedNodes.Clear();
        h.Workspace.ModList.SelectedNodes.AddRange([new ModEntryNodeViewModel(mod)]);

        var target = Assert.Single(h.PluginSelectionLog);
        Assert.Same(plugin.Name, Assert.Single(target).Name);
    }

    [Fact]
    public async Task Selecting_Plugin_Raises_Origin_Mod_And_Does_Not_Bounce()
    {
        var mod = new FakeMod("OriginMod", 1);
        var plugin = new FakePlugin("plugin.esp", mod.Info);
        using var h = await NewHarness([mod], [plugin]);
        var row = h.LoadOrder.LoadOrder.Single(r => r.Name == "plugin.esp");

        h.LoadOrder.SelectedNodes.Clear();
        h.LoadOrder.SelectedNodes.AddRange([row]);

        var target = Assert.Single(h.ModSelectionLog);
        Assert.Same(mod, Assert.IsType<ModEntryNodeViewModel>(Assert.Single(target)).Model);
        Assert.Empty(h.PluginSelectionLog);
    }

    [Fact]
    public async Task Unlinked_Mod_Selection_Propagates_Nothing()
    {
        var modA = new FakeMod("Linked", 1);
        var modB = new FakeMod("Orphan", 2);
        var plugin = new FakePlugin("plugin.esp", modA.Info);
        using var h = await NewHarness([modA, modB], [plugin]);

        var rowB = new ModEntryNodeViewModel(modB);
        h.Workspace.ModList.SelectedNodes.Clear();
        h.Workspace.ModList.SelectedNodes.AddRange([rowB]);

        Assert.Empty(h.PluginSelectionLog);
    }

    [Fact]
    public async Task MultiSelect_Collects_All_Linked_Plugins()
    {
        var modA = new FakeMod("ModA", 1);
        var modB = new FakeMod("ModB", 2);
        var p1 = new FakePlugin("a.esp", modA.Info);
        var p2 = new FakePlugin("b.esp", modB.Info);
        using var h = await NewHarness([modA, modB], [p1, p2]);

        h.Workspace.ModList.SelectedNodes.Clear();
        h.Workspace.ModList.SelectedNodes.AddRange(
            new TreeNodeViewModel[]
            {
                new ModEntryNodeViewModel(modA),
                new ModEntryNodeViewModel(modB),
            }
        );

        var target = Assert.Single(h.PluginSelectionLog);
        Assert.Equal(new[] { "a.esp", "b.esp" }, target.Select(r => r.Name).ToArray());
    }

    [Fact]
    public async Task Inactive_Mod_Produces_No_Conflict_Verdicts()
    {
        var mod = new FakeMod("Inactive", 1, active: false);
        using var h = await NewHarness([mod], []);

        h.Workspace.ModList.SelectedNodes.Clear();
        h.Workspace.ModList.SelectedNodes.AddRange([new ModEntryNodeViewModel(mod)]);
        await Task.Delay(100);

        Assert.Empty(h.Workspace.Verdicts);
    }

    [Fact]
    public async Task ToggleDeployment_DelegatesToService_And_TracksState()
    {
        var deployment = new FakeDeploymentService();
        using var h = await NewHarness([new FakeMod("A", 1)], [], deployment);

        Assert.False(h.Workspace.IsDeployed);
        Assert.Equal("Deploy", h.Workspace.DeployText);

        await h.Workspace.ToggleDeploymentCommand.Execute().ToTask();

        Assert.Equal(1, deployment.DeployCalls);
        Assert.True(h.Workspace.IsDeployed);
        Assert.Equal("Undeploy", h.Workspace.DeployText);

        await h.Workspace.ToggleDeploymentCommand.Execute().ToTask();

        Assert.Equal(1, deployment.UndeployCalls);
        Assert.False(h.Workspace.IsDeployed);
        Assert.Equal("Deploy", h.Workspace.DeployText);
    }

    [Fact]
    public async Task Deploy_Failure_Surfaces_And_Resets()
    {
        var deployment = new FakeDeploymentService
        {
            DeployError = new IOException("mount failed"),
        };
        using var h = await NewHarness([new FakeMod("A", 1)], [], deployment);
        var failures = new List<(string Title, string Text)>();
        h.Workspace.DeploymentFailed.Subscribe(failures.Add);

        await h.Workspace.ToggleDeploymentCommand.Execute().ToTask();

        var failure = Assert.Single(failures);
        Assert.Equal("Deployment failed", failure.Title);
        Assert.Equal("mount failed", failure.Text);
        Assert.False(h.Workspace.IsDeploymentBusy);
        Assert.Equal("Deploy", h.Workspace.DeployText);
        Assert.False(h.Workspace.IsDeployed);
    }

    [Fact]
    public async Task Dispose_WhileDeployed_Undeploys()
    {
        var deployment = new FakeDeploymentService();
        var h = await NewHarness([new FakeMod("A", 1)], [], deployment);
        await h.Workspace.ToggleDeploymentCommand.Execute().ToTask();

        h.Workspace.Dispose();

        Assert.Equal(1, deployment.UndeployCalls);
    }

    [Fact]
    public async Task Deploy_WhileBusy_DisablesCommand_And_ShowsProgressText()
    {
        var deployment = new FakeDeploymentService
        {
            DeployGate = new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously
            ),
        };
        using var h = await NewHarness([new FakeMod("A", 1)], [], deployment);

        var execute = h.Workspace.ToggleDeploymentCommand.Execute().ToTask();

        Assert.True(
            SpinWait.SpinUntil(() => h.Workspace.IsDeploymentBusy, TimeSpan.FromSeconds(5))
        );
        Assert.Equal("Deploying…", h.Workspace.DeployText);
        Assert.False(
            ((System.Windows.Input.ICommand)h.Workspace.ToggleDeploymentCommand).CanExecute(null)
        );

        deployment.DeployGate.SetResult();
        await execute;

        Assert.False(h.Workspace.IsDeploymentBusy);
        Assert.Equal("Undeploy", h.Workspace.DeployText);
    }

    [Fact]
    public async Task Undeploy_WhileBusy_DisablesCommand_And_ShowsProgressText()
    {
        var deployment = new FakeDeploymentService();
        using var h = await NewHarness([new FakeMod("A", 1)], [], deployment);
        await h.Workspace.ToggleDeploymentCommand.Execute().ToTask();
        deployment.UndeployGate = new ManualResetEventSlim(false);

        var execute = h.Workspace.ToggleDeploymentCommand.Execute().ToTask();

        Assert.True(
            SpinWait.SpinUntil(() => h.Workspace.IsDeploymentBusy, TimeSpan.FromSeconds(5))
        );
        Assert.Equal("Undeploying…", h.Workspace.DeployText);
        Assert.False(
            ((System.Windows.Input.ICommand)h.Workspace.ToggleDeploymentCommand).CanExecute(null)
        );

        deployment.UndeployGate.Set();
        await execute;

        Assert.False(h.Workspace.IsDeploymentBusy);
        Assert.Equal("Deploy", h.Workspace.DeployText);
    }
}
