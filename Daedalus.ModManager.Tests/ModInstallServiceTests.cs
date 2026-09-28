using System.Diagnostics.CodeAnalysis;
using Daedalus.Contracts.Games;
using Daedalus.Contracts.ModManager;
using Daedalus.Games;
using Daedalus.VFS;
using Xunit;

namespace Daedalus.ModManager.Tests;

public class ModInstallServiceTests : IDisposable
{
    private sealed class FakeExtractor : IArchiveExtractor
    {
        private EventHandler<ExtractionProgressEventArgs>? _handler;

        public event EventHandler<ExtractionProgressEventArgs>? OnExtractionProgress
        {
            add { _handler += value; }
            remove { _handler -= value; }
        }

        public IReadOnlyCollection<string> SupportedExtensions { get; } = [".zip"];
        public bool ThrowOnExtract { get; set; }

        public void Extract(DirectoryInfo outputDirectory, FileInfo archiveFile)
        {
            if (ThrowOnExtract)
            {
                throw new IOException("corrupt");
            }
            if (!outputDirectory.Exists)
            {
                throw new IOException("destination missing");
            }
            File.WriteAllText(
                Path.Combine(outputDirectory.FullName, "plugin.txt"),
                "content"
            );
            _handler?.Invoke(
                archiveFile,
                new ExtractionProgressEventArgs("plugin.txt", 7, 7, 42.0)
            );
        }
    }

    private sealed class ScriptedInstaller : IModInstaller
    {
        private readonly DirectoryInfo _modsRoot;
        private readonly bool _accepts;
        private readonly bool _succeeds;

        public ScriptedInstaller(DirectoryInfo modsRoot, bool accepts, bool succeeds)
        {
            _modsRoot = modsRoot;
            _accepts = accepts;
            _succeeds = succeeds;
        }

        public int CanInstallCalls { get; private set; }
        public int TryInstallCalls { get; private set; }

        public bool CanInstall(ISupportedGame game, DirectoryInfo content)
        {
            CanInstallCalls++;
            return _accepts;
        }

        public bool TryInstall(
            string name,
            IModInfo modInfo,
            DirectoryInfo content,
            [NotNullWhen(true)] out ILibraryMod? mod
        )
        {
            TryInstallCalls++;
            mod = null;
            if (!_succeeds)
            {
                return false;
            }

            var dir = new DirectoryInfo(Path.Combine(_modsRoot.FullName, name));
            dir.Create();
            mod = new FakeLibraryMod(dir, modInfo);
            return true;
        }
    }

    private sealed class FakeLibraryMod : ILibraryMod
    {
        public FakeLibraryMod(DirectoryInfo directory, IModInfo info)
        {
            Directory = directory;
            Info = info;
        }

        public IModInfo Info { get; }
        public string Name => Directory.Name;
        public DirectoryInfo Directory { get; }
        public VirtualNode<ModFileEntry> Content { get; } =
            new VirtualNode<ModFileEntry>("", NodeFlags.Directory, null, null);

        public void RefreshContent() { }
        public void RenameTo(string newName) { }

        public bool Equals(ILibraryMod? x, ILibraryMod? y) => ReferenceEquals(x, y);
        public int GetHashCode(ILibraryMod obj) => 0;
        public bool Equals(ILibraryMod? other) => ReferenceEquals(this, other);
    }

    private sealed class FakeTargeter : IModTargeter
    {
        private readonly string _targetKey;
        public FakeTargeter(string targetKey) => _targetKey = targetKey;

        public void ApplyAliases(ISupportedGame game, ILibraryMod mod) { }

        public IGamePath GetTarget(ISupportedGame game, ILibraryMod mod)
        {
            return new GamePath(_targetKey, _targetKey, [], []);
        }
    }

    private sealed class FakeSerializer : ILibraryModSerializer
    {
        public int SaveCalls { get; private set; }
        public ILibraryMod Load(FileInfo file) => throw new InvalidOperationException();
        public ILibraryMod Load(DirectoryInfo folder) => throw new InvalidOperationException();
        public void Save(ILibraryMod mod) => SaveCalls++;
    }

    private sealed class FakeInstances : IInstanceService
    {
        private sealed class FakeInstalled(ISupportedGame g) : IInstalledGame
        {
            public DirectoryInfo InstallPath => new(".");
            public ISupportedGame Configuration => g;
            public string LookupAbsolutePath(IGamePath path) => path.Key;
            public string UpdateAbsolutePath(IGamePath path) => path.Key;
        }

        public FakeInstances(ISupportedGame game)
        {
            Current = new CurrentInstance(
                "main",
                new DirectoryInfo("."),
                new FakeInstalled(game)
            );
        }

        public IReadOnlyDictionary<string, DirectoryInfo> Instances { get; } =
            new Dictionary<string, DirectoryInfo>();
        public CurrentInstance? Current { get; }

        public DirectoryInfo Create(string name, DirectoryInfo folder, IInstalledGame g) => folder;
        public void Switch(string name) { }
        public void Remove(string name, bool deleteFolder) { }
        public IInstalledGame? ResolveGame(string instanceName) => Current?.Game;
    }

    private sealed class PathsProxy(DirectoryInfo mods, DirectoryInfo temp) : IModManagerPaths
    {
        public DirectoryInfo AssemblyFolder => mods;
        public DirectoryInfo InstanceFolder => mods;
        public DirectoryInfo StagingFolder => mods;
        public DirectoryInfo ModsFolder => mods;
        public DirectoryInfo ProfilesFolder => mods;
        public DirectoryInfo DownloadsFolder => mods;
        public DirectoryInfo TemporaryFolder => temp;
    }

    private readonly TempDirectory _temp = new();
    private readonly DirectoryInfo _modsRoot;
    private readonly DirectoryInfo _tempRoot;

    public ModInstallServiceTests()
    {
        _modsRoot = new DirectoryInfo(Path.Combine(_temp.Path, "Mods"));
        _tempRoot = new DirectoryInfo(Path.Combine(_temp.Path, "Temp"));
    }

    public void Dispose() => _temp.Dispose();

    private ModInstallService CreateService(
        FakeExtractor extractor,
        IReadOnlyList<IModInstaller> installers,
        FakeTargeter targeter,
        FakeSerializer serializer
    )
    {
        return new ModInstallService(
            extractor,
            installers,
            targeter,
            new FakeInstances(TestAssets.SupportedGame),
            new PathsProxy(_modsRoot, _tempRoot),
            serializer
        );
    }

    private FileInfo WriteFakeArchive()
    {
        var archive = new FileInfo(Path.Combine(_temp.Path, "mod.zip"));
        File.WriteAllText(archive.FullName, "fake");
        return archive;
    }

    [Fact]
    public async Task AcceptingInstaller_InstallsAndAssignsTarget()
    {
        var extractor = new FakeExtractor();
        var installer = new ScriptedInstaller(_modsRoot, accepts: true, succeeds: true);
        var targeter = new FakeTargeter("Data");
        var serializer = new FakeSerializer();
        var service = CreateService(extractor, [installer], targeter, serializer);

        var mod = await service.InstallAsync("SkyUI", "6.1", WriteFakeArchive());

        Assert.NotNull(mod);
        Assert.Equal(1, installer.TryInstallCalls);
        Assert.Equal("Data", mod.Info.Target);
        Assert.Equal(1, serializer.SaveCalls);
        Assert.True(new DirectoryInfo(Path.Combine(_modsRoot.FullName, "SkyUI")).Exists);
    }

    [Fact]
    public async Task DecliningInstaller_SkippedAndInstallsNothing()
    {
        var extractor = new FakeExtractor();
        var installer = new ScriptedInstaller(_modsRoot, accepts: false, succeeds: true);
        var service = CreateService(extractor, [installer], new FakeTargeter("Data"), new FakeSerializer());

        var mod = await service.InstallAsync("SkyUI", null, WriteFakeArchive());

        Assert.Null(mod);
        Assert.Equal(1, installer.CanInstallCalls);
        Assert.Equal(0, installer.TryInstallCalls);
    }

    [Fact]
    public async Task AcceptingButFailingInstaller_ReturnsNull()
    {
        var extractor = new FakeExtractor();
        var installer = new ScriptedInstaller(_modsRoot, accepts: true, succeeds: false);
        var service = CreateService(extractor, [installer], new FakeTargeter("Data"), new FakeSerializer());

        var mod = await service.InstallAsync("SkyUI", null, WriteFakeArchive());

        Assert.Null(mod);
        Assert.Equal(1, installer.TryInstallCalls);
    }

    [Fact]
    public async Task ProvenanceId_FlowsIntoModInfo()
    {
        var extractor = new FakeExtractor();
        var installer = new ScriptedInstaller(_modsRoot, accepts: true, succeeds: true);
        var service = CreateService(extractor, [installer], new FakeTargeter("Data"), new FakeSerializer());

        var mod = await service.InstallAsync(
            "SkyUI",
            null,
            WriteFakeArchive(),
            new ModID(12604, SourceType.NexusMods)
        );

        Assert.NotNull(mod);
        Assert.Equal(new ModID(12604, SourceType.NexusMods), mod.Info.ID);
    }

    [Fact]
    public async Task ExtractionProgress_IsForwardedWithArchive()
    {
        var extractor = new FakeExtractor();
        var installer = new ScriptedInstaller(_modsRoot, accepts: true, succeeds: true);
        var service = CreateService(extractor, [installer], new FakeTargeter("Data"), new FakeSerializer());

        var progress = new List<InstallProgress>();
        service.InstallProgressChanged += (_, p) => progress.Add(p);

        var archive = WriteFakeArchive();
        var mod = await service.InstallAsync("SkyUI", null, archive);

        Assert.NotNull(mod);
        var update = Assert.Single(progress);
        Assert.Equal(archive.FullName, update.Archive.FullName);
        Assert.Equal("plugin.txt", update.EntryPath);
        Assert.Equal(42.0, update.ProgressPercentage);
    }

    [Fact]
    public async Task ExtractionFailure_ReturnsNull()
    {
        var extractor = new FakeExtractor { ThrowOnExtract = true };
        var installer = new ScriptedInstaller(_modsRoot, accepts: true, succeeds: true);
        var service = CreateService(extractor, [installer], new FakeTargeter("Data"), new FakeSerializer());

        var mod = await service.InstallAsync("SkyUI", null, WriteFakeArchive());

        Assert.Null(mod);
        Assert.Equal(0, installer.TryInstallCalls);
    }
}
