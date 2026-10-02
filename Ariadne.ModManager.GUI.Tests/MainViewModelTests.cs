using System.Reactive.Threading.Tasks;
using Ariadne.Contracts.Games;
using Ariadne.Contracts.ModManager;
using Ariadne.Games;
using Ariadne.ModManager;
using Ariadne.ModManager.GUI.ViewModels;
using Ariadne.ModManager.Serialization;
using Xunit;

namespace Ariadne.ModManager.GUI.Tests;

public class MainViewModelTests : IDisposable
{
    private sealed class FakePaths : IModManagerPaths
    {
        private readonly DirectoryInfo _root = new(
            Path.Combine(Path.GetTempPath(), $"Ariadne-mainvm-tests-{Guid.NewGuid():N}")
        );

        public DirectoryInfo AssemblyFolder => _root;
        public DirectoryInfo InstanceFolder => _root;
        public DirectoryInfo StagingFolder => new(Path.Join(_root.FullName, "Staging"));
        public DirectoryInfo ModsFolder => new(Path.Join(_root.FullName, "Mods"));
        public DirectoryInfo ProfilesFolder => new(Path.Join(_root.FullName, "Profiles"));
        public DirectoryInfo TemporaryFolder => new(Path.Join(_root.FullName, "Temp"));
        public DirectoryInfo DownloadsFolder => new(Path.Join(_root.FullName, "Downloads"));

        public void Cleanup()
        {
            _root.Refresh();
            if (_root.Exists)
            {
                _root.Delete(true);
            }
        }
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

    private sealed class NoEditor : IModProfileEditor
    {
        public Task RemoveModAsync(IModProfile profile, ILibraryMod mod) => Task.CompletedTask;

        public Task DissolveGroupAsync(IModProfile profile, IModGroup group) => Task.CompletedTask;

        public bool TryAddMod(IModProfile profile, ILibraryMod mod)
        {
            var exists = profile.ModList.Any(entry =>
                string.Equals(
                    entry.Mod.Directory.FullName,
                    mod.Directory.FullName,
                    StringComparison.OrdinalIgnoreCase
                )
            );
            if (exists)
            {
                return false;
            }
            profile.ModList.Add(new ModListEntry(mod, active: false));
            return true;
        }

        public IModGroup CreateGroup(IModProfile profile)
        {
            var group = new ModGroup("New Group", []);
            profile.ModList.ModGroups.Add(group);
            return group;
        }
    }

    private sealed class FakeBuilder : ILoadOrderBuilder
    {
        public IEnumerable<ILoadOrderInfo> Fetch(IInstalledGame game, IModList mods) => [];

        public void Deploy(
            IInstalledGame game,
            IModDeploymentMethod deploymentMethod,
            IReadOnlyList<ILoadOrderInfo> loadOrderInfos
        ) { }

        public void Save(
            IModProfile currentProfile,
            IReadOnlyList<ILoadOrderInfo> loadOrderInfos
        ) { }

        public IEnumerable<ILoadOrderInfo> Sort(
            IModProfile currentProfile,
            IEnumerable<ILoadOrderInfo> loadOrderInfos
        ) => loadOrderInfos;
    }

    private readonly FakePaths _paths = new();

    public void Dispose() => _paths.Cleanup();

    private sealed class StubDeploymentService(bool deployed) : IDeploymentService
    {
        public bool IsDeployed { get; } = deployed;
        public IReadOnlyList<DirectoryInfo> DeployedPaths => [];
        public event EventHandler? DeploymentChanged;

        public Task DeployAsync(
            IModProfile profile,
            IReadOnlyList<ILoadOrderInfo> loadOrder,
            CancellationToken cancellationToken = default
        ) => Task.CompletedTask;

        public Task UndeployAsync() => Task.CompletedTask;
    }

    private sealed class FakeModFactory : ILibraryModFactory
    {
        public ILibraryMod Create(string name, IModInfo info) =>
            throw new InvalidOperationException();

        public ILibraryMod Create(IModInfo info) => throw new InvalidOperationException();

        public ILibraryMod Open(DirectoryInfo folder) =>
            new LibraryMod(new ModInfo(0, SourceType.Local, "", [], ""), folder, []);

        public bool TryCreate(string name, IModInfo info, out ILibraryMod? mod)
        {
            mod = null;
            return false;
        }
    }

    private MainViewModel CreateViewModel(
        IInstalledGame? game = null,
        IDeploymentService? deployment = null
    )
    {
        var serializer = new ModProfileSerializer(new LibraryModSerializer([]), _paths);
        return new MainViewModel(
            serializer,
            new LibraryModSerializer([]),
            _paths,
            new FakeInstances(game),
            new NoEditor(),
            new GameCatalog([]),
            null!,
            new FakeBuilder(),
            modFactory: new FakeModFactory(),
            deploymentService: deployment,
            profiles: new ProfileService(serializer, _paths)
        );
    }

    private static IInstalledGame GameAt(DirectoryInfo installDir)
    {
        var config = new SupportedGame(
            "Test Game",
            [],
            new VendorInfo(0, 0),
            new GamePath("_root", "", []),
            [],
            []
        );
        installDir.Create();
        return new InstalledGame(installDir, config);
    }

    private void SeedMod(string name)
    {
        var modDir = Directory.CreateDirectory(Path.Join(_paths.ModsFolder.FullName, name));
        new LibraryModSerializer([]).Save(
            new LibraryMod(new ModInfo(1, SourceType.Local, "1.0", [], "Data"), modDir, [])
        );
    }

    private void SeedProfile(string folderName, string modelName, params string[] modNames)
    {
        var serializer = new ModProfileSerializer(new LibraryModSerializer([]), _paths);
        var folder = new DirectoryInfo(Path.Join(_paths.ProfilesFolder.FullName, folderName));
        var mods = modNames
            .Select(name =>
                (IModListEntry)
                    new ModListEntry(
                        new LibraryMod(
                            new ModInfo(1, SourceType.Local, "1.0", [], "Data"),
                            new DirectoryInfo(Path.Join(_paths.ModsFolder.FullName, name)),
                            []
                        ),
                        true
                    )
            )
            .ToList();
        serializer.Save(
            new ModProfile(modelName, new ModList(mods, []), new Version(1, 0), folder)
        );
    }

    [Fact]
    public async Task CreateProfile_Prompts_Creates_And_Selects()
    {
        var viewModel = CreateViewModel();
        viewModel.AskProfileName.RegisterHandler(context => context.SetOutput("NewProfile"));

        await viewModel.CreateProfileCommand.Execute().ToTask();

        Assert.Contains("NewProfile", viewModel.ProfileNames);
        Assert.Equal("NewProfile", viewModel.SelectedProfileName);
        Assert.True(
            File.Exists(
                Path.Join(
                    _paths.ProfilesFolder.FullName,
                    "NewProfile",
                    ModProfileSerializer.FileName
                )
            )
        );
    }

    [Fact]
    public async Task CreateProfile_InvalidName_CreatesNothing()
    {
        var viewModel = CreateViewModel();
        viewModel.AskProfileName.RegisterHandler(context => context.SetOutput("bad/name"));

        await viewModel.CreateProfileCommand.Execute().ToTask();

        Assert.Empty(viewModel.ProfileNames);
        Assert.Null(viewModel.SelectedProfileName);
    }

    [Fact]
    public async Task CreateProfile_CancelledPrompt_CreatesNothing()
    {
        var viewModel = CreateViewModel();
        viewModel.AskProfileName.RegisterHandler(context => context.SetOutput(null));

        await viewModel.CreateProfileCommand.Execute().ToTask();

        Assert.Empty(viewModel.ProfileNames);
        Assert.False(Directory.Exists(Path.Join(_paths.ProfilesFolder.FullName, "NewProfile")));
    }

    [Fact]
    public async Task ImportFromProfile_AddsMods_WithoutDuplicates()
    {
        var game = GameAt(new DirectoryInfo(Path.Join(_paths.TemporaryFolder.FullName, "game")));
        SeedMod("ModA");
        SeedMod("ModB");
        SeedProfile("Other", "Other", "ModA", "ModB");
        SeedProfile("Default", "Default", "ModA");
        File.SetLastWriteTimeUtc(
            Path.Join(_paths.ProfilesFolder.FullName, "Default", ModProfileSerializer.FileName),
            DateTime.UtcNow
        );
        var viewModel = CreateViewModel(game);
        await viewModel.InitializeCommand.Execute().ToTask();

        await viewModel.ImportFromProfileCommand.Execute("Other").ToTask();

        var workspace = Assert.IsType<WorkspaceViewModel>(viewModel.CurrentViewModel);
        var entries = workspace.ModList.EnumerateModEntries().ToList();
        Assert.Equal(
            new[] { "ModA", "ModB" },
            entries.Select(entry => entry.DisplayName).Order().ToArray()
        );
        Assert.True(entries.Single(entry => entry.DisplayName == "ModA").Active);
        Assert.False(entries.Single(entry => entry.DisplayName == "ModB").Active);
    }

    [Fact]
    public async Task ImportFromAllProfiles_ImportsEveryOtherProfile_SkippingActive()
    {
        var game = GameAt(new DirectoryInfo(Path.Join(_paths.TemporaryFolder.FullName, "game")));
        SeedMod("ModA");
        SeedMod("ModB");
        SeedMod("ModC");
        SeedProfile("Other", "Other", "ModB");
        SeedProfile("Third", "Third", "ModC");
        SeedProfile("Default", "Default", "ModA");
        File.SetLastWriteTimeUtc(
            Path.Join(_paths.ProfilesFolder.FullName, "Default", ModProfileSerializer.FileName),
            DateTime.UtcNow
        );
        var viewModel = CreateViewModel(game);
        await viewModel.InitializeCommand.Execute().ToTask();

        await viewModel.ImportFromAllProfilesCommand.Execute().ToTask();

        var workspace = Assert.IsType<WorkspaceViewModel>(viewModel.CurrentViewModel);
        var names = workspace
            .ModList.EnumerateModEntries()
            .Select(entry => entry.DisplayName)
            .Order()
            .ToArray();
        Assert.Equal(new[] { "ModA", "ModB", "ModC" }, names);
    }

    [Fact]
    public async Task ImportItems_ListsAllFirst_ThenOtherProfiles_ExcludingActive()
    {
        var game = GameAt(new DirectoryInfo(Path.Join(_paths.TemporaryFolder.FullName, "game")));
        SeedProfile("Other", "Other");
        SeedProfile("Third", "Third");
        SeedProfile("Default", "Default");
        File.SetLastWriteTimeUtc(
            Path.Join(_paths.ProfilesFolder.FullName, "Default", ModProfileSerializer.FileName),
            DateTime.UtcNow
        );
        var viewModel = CreateViewModel(game);
        await viewModel.InitializeCommand.Execute().ToTask();

        Assert.Equal(
            new[] { "All", "Other", "Third" },
            viewModel.ImportItems.Select(item => item.Header).ToArray()
        );
        Assert.Equal("Ctrl+I", viewModel.ImportItems[0].Gesture?.ToString());

        viewModel.SelectedProfileName = "Other";

        Assert.Equal(
            new[] { "All", "Default", "Third" },
            viewModel.ImportItems.Select(item => item.Header).ToArray()
        );
    }

    [Fact]
    public async Task SwitchingProfiles_PreservesSelectedSidePanelTab()
    {
        var game = GameAt(new DirectoryInfo(Path.Join(_paths.TemporaryFolder.FullName, "game")));
        SeedProfile("Default", "Default");
        SeedProfile("Other", "Other");
        File.SetLastWriteTimeUtc(
            Path.Join(_paths.ProfilesFolder.FullName, "Default", ModProfileSerializer.FileName),
            DateTime.UtcNow
        );
        var viewModel = CreateViewModel(game);
        await viewModel.InitializeCommand.Execute().ToTask();
        var workspace = Assert.IsType<WorkspaceViewModel>(viewModel.CurrentViewModel);
        workspace.SelectedTab = workspace.SidePanelTabs[1];

        viewModel.SelectedProfileName = "Other";

        var next = Assert.IsType<WorkspaceViewModel>(viewModel.CurrentViewModel);
        Assert.Equal("Deployed", next.SelectedTab?.Title);
    }

    [Fact]
    public async Task Deployed_ProfileSwitch_IsBlocked()
    {
        var game = GameAt(new DirectoryInfo(Path.Join(_paths.TemporaryFolder.FullName, "game")));
        SeedProfile("Default", "Default");
        SeedProfile("Other", "Other");
        File.SetLastWriteTimeUtc(
            Path.Join(_paths.ProfilesFolder.FullName, "Default", ModProfileSerializer.FileName),
            DateTime.UtcNow
        );
        var viewModel = CreateViewModel(game, new StubDeploymentService(deployed: true));
        await viewModel.InitializeCommand.Execute().ToTask();
        Assert.Equal("Default", viewModel.SelectedProfileName);

        viewModel.SelectedProfileName = "Other";

        Assert.Equal("Default", viewModel.ActiveProfile!.Model.ProfileFolder.Name);
    }

    [Fact]
    public async Task ImportFromDisk_RegistersLibraryMods_AndSkipsUnknownFolders()
    {
        var game = GameAt(new DirectoryInfo(Path.Join(_paths.TemporaryFolder.FullName, "game")));
        SeedMod("ModA");
        SeedMod("ModB");
        Directory.CreateDirectory(Path.Join(_paths.ModsFolder.FullName, "Stray"));
        SeedProfile("Default", "Default", "ModA");
        var viewModel = CreateViewModel(game);
        await viewModel.InitializeCommand.Execute().ToTask();

        await viewModel.ImportFromDiskCommand.Execute().ToTask();

        var workspace = Assert.IsType<WorkspaceViewModel>(viewModel.CurrentViewModel);
        var entries = workspace.ModList.EnumerateModEntries().ToList();
        Assert.Equal(
            new[] { "ModA", "ModB" },
            entries.Select(entry => entry.DisplayName).Order().ToArray()
        );
        Assert.True(entries.Single(entry => entry.DisplayName == "ModA").Active);
        Assert.False(entries.Single(entry => entry.DisplayName == "ModB").Active);
    }

    [Fact]
    public async Task ImportFromAllProfiles_SkipsCorruptProfileFiles()
    {
        var game = GameAt(new DirectoryInfo(Path.Join(_paths.TemporaryFolder.FullName, "game")));
        SeedMod("ModA");
        SeedMod("ModB");
        SeedProfile("Other", "Other", "ModB");
        var corrupt = Directory.CreateDirectory(
            Path.Join(_paths.ProfilesFolder.FullName, "Corrupt")
        );
        File.WriteAllText(Path.Join(corrupt.FullName, ModProfileSerializer.FileName), "{ not json");
        SeedProfile("Default", "Default", "ModA");
        File.SetLastWriteTimeUtc(
            Path.Join(_paths.ProfilesFolder.FullName, "Default", ModProfileSerializer.FileName),
            DateTime.UtcNow
        );
        var viewModel = CreateViewModel(game);
        await viewModel.InitializeCommand.Execute().ToTask();

        await viewModel.ImportFromAllProfilesCommand.Execute().ToTask();

        var workspace = Assert.IsType<WorkspaceViewModel>(viewModel.CurrentViewModel);
        Assert.Equal(
            new[] { "ModA", "ModB" },
            workspace
                .ModList.EnumerateModEntries()
                .Select(entry => entry.DisplayName)
                .Order()
                .ToArray()
        );
    }

    [Fact]
    public async Task SwitchingProfiles_ComparesFolderNames_NotModelNames()
    {
        var game = GameAt(new DirectoryInfo(Path.Join(_paths.TemporaryFolder.FullName, "game")));
        SeedMod("ModA");
        SeedProfile("Default", "Default", "ModA");
        SeedProfile("Vanilla", "Default");
        File.SetLastWriteTimeUtc(
            Path.Join(_paths.ProfilesFolder.FullName, "Vanilla", ModProfileSerializer.FileName),
            DateTime.UtcNow
        );
        var viewModel = CreateViewModel(game);
        await viewModel.InitializeCommand.Execute().ToTask();
        Assert.Equal("Vanilla", viewModel.SelectedProfileName);

        viewModel.SelectedProfileName = "Default";

        Assert.Equal("Default", viewModel.ActiveProfile!.Model.ProfileFolder.Name);
        var workspace = Assert.IsType<WorkspaceViewModel>(viewModel.CurrentViewModel);
        Assert.Contains(
            workspace.ModList.EnumerateModEntries(),
            entry => entry.DisplayName == "ModA"
        );
    }
}
