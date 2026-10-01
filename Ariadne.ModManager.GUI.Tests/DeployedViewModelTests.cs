using Ariadne.Contracts.Games;
using Ariadne.Contracts.ModManager;
using Ariadne.Games;
using Ariadne.ModManager.GUI.ViewModels;
using Xunit;

namespace Ariadne.ModManager.GUI.Tests;

public class DeployedViewModelTests
{
    private sealed class FakeGameConfiguration(IReadOnlyList<IGamePath> installTargets)
        : ISupportedGame
    {
        public string Name => "Test Game";
        public IVendorInfo Vendors => null!;
        public IReadOnlyDictionary<string, string> ProtocolGameIds =>
            new Dictionary<string, string>();
        public IGamePath Root => new GamePath("_root", "", [], []);
        public IReadOnlyList<IGamePath> Deployments => [];
        public IReadOnlyList<IGamePath> InstallTargets => installTargets;
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

    private sealed class FakeMod(string name, string target) : ILibraryMod
    {
        public IModInfo Info { get; } =
            new ModManager.ModInfo(0, SourceType.Local, "1.0", [], target);
        public string Name { get; } = name;
        public DirectoryInfo Directory => new(".");
        public Ariadne.VFS.VirtualNode<ModFileEntry> Content { get; } =
            new(name, Ariadne.VFS.NodeFlags.Directory, null, default);

        public void RefreshContent() { }

        public void RenameTo(string newName) { }

        public bool Equals(ILibraryMod? x, ILibraryMod? y) => ReferenceEquals(x, y);

        public int GetHashCode(ILibraryMod obj) => obj.GetHashCode();

        public bool Equals(ILibraryMod? other) => ReferenceEquals(this, other);
    }

    private sealed class FakeProfile(IModList modList) : IModProfile
    {
        public string Name { get; set; } = "Default";
        public IModList ModList { get; set; } = modList;
        public Version Version { get; set; } = new(1, 0);
        public DirectoryInfo ProfileFolder => new(".");
        public DirectoryInfo OverwriteFolder => new(".");
    }

    private static readonly IReadOnlyList<IGamePath> Targets =
    [
        new GamePath("Data", "Data", [], []),
        new GamePath("Root", "", [], []),
    ];

    private static DeployedViewModel CreateViewModel(params FakeMod[] mods)
    {
        return CreateViewModelCore(
            new ModManager.ModList(
                mods.Select(mod => (IModListEntry)new ModManager.ModListEntry(mod, true)).ToList(),
                []
            )
        );
    }

    private static DeployedViewModel CreateViewModelCore(IModList mods)
    {
        return new DeployedViewModel(
            new FakeProfile(mods),
            new FakeInstances(new FakeGame(new FakeGameConfiguration(Targets)))
        );
    }

    [Fact]
    public async Task UntargetedMod_DisplaysRootInstallTargetKey()
    {
        var viewModel = CreateViewModel([new FakeMod("A", "")]);

        await viewModel.EnsureInitializedAsync();

        Assert.Equal(["Root"], viewModel.TargetRoots.ToArray());
        Assert.Equal("Test Game/Root", viewModel.SummaryTitle);
    }

    [Fact]
    public async Task TargetedMod_GroupsUnderTargetKey()
    {
        var viewModel = CreateViewModel([new FakeMod("A", "Data")]);

        await viewModel.EnsureInitializedAsync();

        Assert.Equal(["Data"], viewModel.TargetRoots.ToArray());
    }

    [Fact]
    public async Task RefreshAsync_RegroupsAfterTargetChange()
    {
        var mod = new FakeMod("A", "");
        var viewModel = CreateViewModel([mod]);
        await viewModel.EnsureInitializedAsync();
        Assert.Equal(["Root"], viewModel.TargetRoots.ToArray());

        mod.Info.Target = "Data";
        await viewModel.RefreshAsync();

        Assert.Equal(["Data"], viewModel.TargetRoots.ToArray());
        Assert.Equal("Test Game/Data", viewModel.SummaryTitle);
    }
}
