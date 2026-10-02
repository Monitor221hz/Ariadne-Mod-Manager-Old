using Ariadne.Contracts.Games;
using Ariadne.Contracts.ModManager;
using Ariadne.Games;
using Ariadne.ModManager;
using Ariadne.VFS;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Skyrim;
using Xunit;

namespace Ariadne.ModManager.Bethesda.Tests;

public class SkyrimSELoadOrderBuilderTests : IDisposable
{
    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } =
            System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "AriadneTests-" + Guid.NewGuid().ToString("N")
            );

        public TempDirectory() => Directory.CreateDirectory(Path);

        public string Combine(params string[] parts) =>
            System.IO.Path.Combine(new[] { Path }.Concat(parts).ToArray());

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, true);
            }
        }
    }

    private readonly TempDirectory _temp = new();
    private readonly DirectoryInfo _installDir;
    private readonly DirectoryInfo _appDataDir;
    private readonly InstalledGame _game;

    public SkyrimSELoadOrderBuilderTests()
    {
        _installDir = Directory.CreateDirectory(_temp.Combine("game"));
        _appDataDir = Directory.CreateDirectory(_temp.Combine("appdata"));
        var config = new SupportedGame(
            "Skyrim Special Edition",
            [],
            new VendorInfo(489830, 0),
            new GamePath("Root", "", []),
            [new GamePath("_appData", _appDataDir.FullName, [])],
            [new GamePath("Data", "Data", [], basedOn: "Root")]
        );
        _game = new InstalledGame(_installDir, config);
    }

    public void Dispose() => _temp.Dispose();

    private LibraryMod CreateMod(string name, out DirectoryInfo modDir)
    {
        modDir = Directory.CreateDirectory(_temp.Combine("mods", name));
        return new LibraryMod(new ModInfo(1, SourceType.Local, "1.0", [], "Data"), modDir, []);
    }

    private static FileInfo WritePlugin(DirectoryInfo dir, string fileName)
    {
        var modKey = ModKey.FromNameAndExtension(fileName);
        var mod = new SkyrimMod(modKey, SkyrimRelease.SkyrimSE);
        var path = System.IO.Path.Combine(dir.FullName, fileName);
        mod.WriteToBinary(path);
        return new FileInfo(path);
    }

    private static FileInfo WriteMasterPlugin(DirectoryInfo dir, string fileName, out Npc baseNpc)
    {
        var modKey = ModKey.FromNameAndExtension(fileName);
        var mod = new SkyrimMod(modKey, SkyrimRelease.SkyrimSE);
        baseNpc = mod.Npcs.AddNew();
        var path = System.IO.Path.Combine(dir.FullName, fileName);
        mod.WriteToBinary(path);
        return new FileInfo(path);
    }

    private static FileInfo WriteDependentPlugin(DirectoryInfo dir, string fileName, Npc masterNpc)
    {
        var modKey = ModKey.FromNameAndExtension(fileName);
        var mod = new SkyrimMod(modKey, SkyrimRelease.SkyrimSE);
        mod.Npcs.GetOrAddAsOverride(masterNpc);
        var path = System.IO.Path.Combine(dir.FullName, fileName);
        mod.WriteToBinary(path);
        return new FileInfo(path);
    }

    [Fact]
    public void Fetch_ReturnsPluginInfoForEachPlugin()
    {
        var modA = CreateMod("ModA", out var dirA);
        var modB = CreateMod("ModB", out var dirB);
        var baseFile = WriteMasterPlugin(dirA, "Base.esm", out var baseNpc);
        var depKey = ModKey.FromNameAndExtension("Dependent.esp");
        var depFile = WriteDependentPlugin(dirB, "Dependent.esp", baseNpc);
        File.WriteAllText(System.IO.Path.Combine(dirB.FullName, "readme.txt"), "not a plugin");

        var builder = new SkyrimSELoadOrderBuilder();

        var results = builder
            .Fetch(
                _game,
                new ModList([new ModListEntry(modA, true), new ModListEntry(modB, true)], [])
            )
            .ToList();

        Assert.Equal(2, results.Count);
        var baseInfo = Assert.Single(results, r => ((IModKeyed)r).ModKey.FileName == "Base.esm");
        var depInfo = Assert.Single(results, r => ((IModKeyed)r).ModKey == depKey);

        Assert.Same(modA.Info, baseInfo.Origin);
        Assert.Same(modB.Info, depInfo.Origin);
        Assert.Equal(baseFile.FullName, Assert.Single(baseInfo.Artifacts).FullName);
        Assert.Equal(depFile.FullName, Assert.Single(depInfo.Artifacts).FullName);
        Assert.False(baseInfo.Active);
    }

    [Fact]
    public void Fetch_LinksMasterDependenciesBetweenPlugins()
    {
        var modA = CreateMod("ModA", out var dirA);
        var modB = CreateMod("ModB", out var dirB);
        var baseKey = ModKey.FromNameAndExtension("Base.esm");
        WriteMasterPlugin(dirA, "Base.esm", out var baseNpc);
        WriteDependentPlugin(dirB, "Dependent.esp", baseNpc);

        var builder = new SkyrimSELoadOrderBuilder();

        var results = builder
            .Fetch(
                _game,
                new ModList([new ModListEntry(modA, true), new ModListEntry(modB, true)], [])
            )
            .ToList();

        var baseInfo = results.Single(r => ((IModKeyed)r).ModKey == baseKey);
        var depInfo = results.Single(r => ((IModKeyed)r).ModKey.FileName == "Dependent.esp");

        Assert.Empty(baseInfo.Dependencies);
        Assert.Same(baseInfo, Assert.Single(depInfo.Dependencies));
    }

    [Fact]
    public void Fetch_IgnoresMastersMissingFromModList()
    {
        var modB = CreateMod("ModB", out var dirB);
        var unlistedDir = Directory.CreateDirectory(_temp.Combine("not-a-mod"));
        WriteMasterPlugin(unlistedDir, "Unlisted.esm", out var unlistedNpc);
        WriteDependentPlugin(dirB, "Dependent.esp", unlistedNpc);

        var builder = new SkyrimSELoadOrderBuilder();

        var results = builder
            .Fetch(_game, new ModList([new ModListEntry(modB, true)], []))
            .ToList();

        var depInfo = Assert.Single(results);
        Assert.Empty(depInfo.Dependencies);
    }

    [Fact]
    public void Fetch_ModWithoutPlugins_YieldsNothing()
    {
        var mod = CreateMod("TexturesOnly", out var dir);
        Directory.CreateDirectory(System.IO.Path.Combine(dir.FullName, "textures"));
        File.WriteAllText(System.IO.Path.Combine(dir.FullName, "notes.txt"), "nope");

        var builder = new SkyrimSELoadOrderBuilder();

        Assert.Empty(builder.Fetch(_game, new ModList([new ModListEntry(mod, true)], [])));
    }

    [Fact]
    public void Sort_AppendsPluginsMissingFromPluginsTxt_AsActive()
    {
        var modA = CreateMod("ModA", out var dirA);
        var modB = CreateMod("ModB", out var dirB);
        WritePlugin(dirA, "Known.esp");
        WritePlugin(dirB, "New.esp");
        var profileDir = Directory.CreateDirectory(_temp.Combine("profile"));
        File.WriteAllText(
            System.IO.Path.Combine(profileDir.FullName, "plugins.txt"),
            "*Known.esp\n"
        );
        var profile = new ModProfile("P", new ModList([], []), new Version(1, 0), profileDir);
        var builder = new SkyrimSELoadOrderBuilder();
        var infos = builder
            .Fetch(
                _game,
                new ModList([new ModListEntry(modA, true), new ModListEntry(modB, true)], [])
            )
            .ToList();

        var sorted = builder.Sort(profile, infos).ToList();

        Assert.Equal(
            new[] { "Known.esp", "New.esp" },
            sorted.Select(i => ((IModKeyed)i).ModKey.FileName.ToString()).ToArray()
        );
        Assert.All(sorted, info => Assert.True(info.Active));
    }

    [Fact]
    public void Sort_EmptyPluginsTxt_YieldsAllActive()
    {
        var modA = CreateMod("ModA", out var dirA);
        var modB = CreateMod("ModB", out var dirB);
        WritePlugin(dirA, "A.esp");
        WritePlugin(dirB, "B.esp");
        var profileDir = Directory.CreateDirectory(_temp.Combine("profile"));
        File.WriteAllText(System.IO.Path.Combine(profileDir.FullName, "plugins.txt"), "");
        var profile = new ModProfile("P", new ModList([], []), new Version(1, 0), profileDir);
        var builder = new SkyrimSELoadOrderBuilder();
        var infos = builder
            .Fetch(
                _game,
                new ModList([new ModListEntry(modA, true), new ModListEntry(modB, true)], [])
            )
            .ToList();

        var sorted = builder.Sort(profile, infos).ToList();

        Assert.Equal(2, sorted.Count);
        Assert.All(sorted, info => Assert.True(info.Active));
    }

    [SkippableFact]
    public void Deploy_WritesPluginsTxtToAppData()
    {
        Skip.IfNot(
            new Mutagen.Bethesda.Installs.GameLocator().TryGetDataDirectory(
                GameRelease.SkyrimSE,
                out _
            ),
            "Skyrim Special Edition installation not found"
        );

        var modA = CreateMod("ModA", out var dirA);
        var modB = CreateMod("ModB", out var dirB);
        var activeKey = ModKey.FromNameAndExtension("Active.esp");
        var inactiveKey = ModKey.FromNameAndExtension("Inactive.esp");
        var activeFile = WritePlugin(dirA, "Active.esp");
        var inactiveFile = WritePlugin(dirB, "Inactive.esp");

        var builder = new SkyrimSELoadOrderBuilder();
        var infos = builder
            .Fetch(
                _game,
                new ModList([new ModListEntry(modA, true), new ModListEntry(modB, true)], [])
            )
            .ToList();
        infos.Single(i => ((IModKeyed)i).ModKey == activeKey).Active = true;
        infos.Single(i => ((IModKeyed)i).ModKey == inactiveKey).Active = false;

        builder.Deploy(_game, new NoopDeploymentMethod(), infos);

        var pluginsTxt = System.IO.Path.Combine(_appDataDir.FullName, "plugins.txt");
        Assert.True(File.Exists(pluginsTxt));
        var lines = File.ReadAllLines(pluginsTxt);

        Assert.Contains("*Active.esp", lines);
        Assert.Contains("Inactive.esp", lines);
        Assert.DoesNotContain("readme.txt", lines);
    }

    [Fact]
    public void Deploy_IgnoresNonBethesdaLoadOrderInfos()
    {
        var mod = CreateMod("ModA", out _);

        var builder = new SkyrimSELoadOrderBuilder();
        var foreign = new LoadOrderInfoStub(mod);

        builder.Deploy(_game, new NoopDeploymentMethod(), [foreign]);

        var pluginsTxt = System.IO.Path.Combine(_appDataDir.FullName, "plugins.txt");
        Assert.True(File.Exists(pluginsTxt));
        Assert.DoesNotContain(File.ReadAllLines(pluginsTxt), l => !l.StartsWith('#'));
    }

    private sealed class NoopDeploymentMethod : IModDeploymentMethod
    {
        public ModDeploymentFlags Flags => ModDeploymentFlags.EmptyMountPoints;

        public void Deploy(IInstalledGame game, IReadOnlyList<ILibraryMod> mods) { }

        public void Revert(IInstalledGame game) { }

        public void Dispose() { }

        public void SetOutputRules(List<OutputRule> outputRules) { }
    }

    private sealed class LoadOrderInfoStub(ILibraryMod origin) : ILoadOrderInfo
    {
        public string Name => origin.Name;
        public IModInfo Origin { get; } = origin.Info;
        public IReadOnlyList<FileInfo> Artifacts { get; } = [];
        public IReadOnlyList<ILoadOrderInfo> Dependencies { get; } = [];
        public bool Active { get; set; }
    }
}
