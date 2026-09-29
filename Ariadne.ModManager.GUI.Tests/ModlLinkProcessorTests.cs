using Ariadne.Contracts.Games;
using Ariadne.Contracts.ModManager;
using Ariadne.Downloads;
using Ariadne.Games;
using Ariadne.ModManager.GUI;
using Ariadne.WebProtocol.Modl;
using Ariadne.WebProtocol.Nexus;
using Xunit;

namespace Ariadne.ModManager.GUI.Tests;

public class ModlLinkProcessorTests
{
    private const string SkyrimModlId = "skyrimse";

    private sealed class FakeGame : IInstalledGame
    {
        public DirectoryInfo InstallPath => new(".");
        public ISupportedGame Configuration { get; }

        public FakeGame(ISupportedGame configuration) => Configuration = configuration;

        public string LookupAbsolutePath(IGamePath path) => path.DirectoryPath;

        public string UpdateAbsolutePath(IGamePath path) => path.DirectoryPath;
    }

    private sealed class FakeInstances : IInstanceService
    {
        public FakeInstances(ISupportedGame game, string instanceName = "main")
        {
            Current = new CurrentInstance(instanceName, new DirectoryInfo("."), new FakeGame(game));
        }

        public IReadOnlyDictionary<string, DirectoryInfo> Instances { get; } =
            new Dictionary<string, DirectoryInfo>();
        public CurrentInstance? Current { get; }

        public DirectoryInfo Create(string name, DirectoryInfo folder, IInstalledGame game) =>
            folder;

        public void Switch(string name) { }

        public void Remove(string name, bool deleteFolder) { }

        public IInstalledGame? ResolveGame(string instanceName) => Current?.Game;
    }

    private sealed class FakeCatalog : IGameCatalog
    {
        public FakeCatalog(params ISupportedGame[] games) => Games = games;

        public IReadOnlyList<ISupportedGame> Games { get; }
    }

    private sealed class FakePaths : IModManagerPaths
    {
        public DirectoryInfo AssemblyFolder => new(".");
        public DirectoryInfo InstanceFolder => new(".");
        public DirectoryInfo StagingFolder => new(".");
        public DirectoryInfo ModsFolder => new(".");
        public DirectoryInfo ProfilesFolder => new(".");
        public DirectoryInfo TemporaryFolder => new(".");
        public DirectoryInfo DownloadsFolder { get; } =
            new(Path.Combine(Path.GetTempPath(), $"Ariadne-modl-tests-{Guid.NewGuid():N}"));
    }

#pragma warning disable CS0067
    private sealed class FakeQueue : IDownloadQueue
    {
        private readonly List<DownloadJob> _jobs = [];

        public List<DownloadRequest> Enqueued { get; } = [];
        public IReadOnlyList<DownloadJob> Jobs => _jobs.ToList();

        public event EventHandler<DownloadJob>? JobQueued;
        public event EventHandler<DownloadJob>? JobStarted;
        public event EventHandler<DownloadProgress>? JobProgress;
        public event EventHandler<DownloadCompletion>? JobCompleted;

        public Guid Enqueue(DownloadRequest request)
        {
            Enqueued.Add(request);
            var job = new DownloadJob
            {
                Id = request.Id ?? Guid.NewGuid(),
                Source = request.Source,
                Destination = request.Destination,
                DisplayName = request.Destination.Name,
                QueuedUtc = DateTimeOffset.UtcNow,
                Status = DownloadJobStatus.Queued,
            };
            _jobs.Add(job);
            return job.Id;
        }

        public bool Cancel(Guid id) => false;

        public void MarkCompleted(Guid id, long totalBytes)
        {
            var job = _jobs.Single(job => job.Id == id) with
            {
                Status = DownloadJobStatus.Completed,
                TotalBytes = totalBytes,
                FinishedUtc = DateTimeOffset.UtcNow,
            };
            _jobs.RemoveAll(candidate => candidate.Id == id);
            _jobs.Add(job);
            JobCompleted?.Invoke(
                this,
                new DownloadCompletion(
                    job,
                    new DownloadResult(
                        id,
                        DownloadState.Completed,
                        job.Destination,
                        null,
                        totalBytes,
                        totalBytes
                    )
                )
            );
        }
    }
#pragma warning restore CS0067

    private sealed class Fixture : IDisposable
    {
        public WebLinkBuffer Buffer { get; } = new();
        public ISupportedGame Skyrim { get; }
        public FakeCatalog Catalog { get; }
        public FakeInstances Instances { get; }
        public FakePaths Paths { get; } = new();
        public FakeQueue Queue { get; } = new();
        public List<string> Notifications { get; } = [];
        public ModlLinkProcessor Processor { get; }

        public Fixture(ISupportedGame? currentInstanceGame = null)
        {
            Skyrim = new SupportedGame(
                "Skyrim Special Edition",
                [],
                new VendorInfo(0, 0),
                new GamePath("Root", "", [], []),
                [],
                [],
                new Dictionary<string, string> { [ProtocolSchemes.Modl] = SkyrimModlId }
            );
            Catalog = new FakeCatalog(Skyrim);
            Instances = new FakeInstances(currentInstanceGame ?? Skyrim);
            Processor = new ModlLinkProcessor(Buffer, Catalog, Instances, Paths, Queue);
            Processor.Notification += (_, message) => Notifications.Add(message);
        }

        public void Dispose()
        {
            Processor.Dispose();
            Paths.DownloadsFolder.Refresh();
            if (Paths.DownloadsFolder.Exists)
            {
                Paths.DownloadsFolder.Delete(recursive: true);
            }
        }
    }

    private static ModlLink Link(string gameId = SkyrimModlId)
    {
        return ModlLink.Parse($"modl://{gameId}/?url=https%3A%2F%2Fexample.com%2FMy%20Mod.7z");
    }

    private static bool WaitUntil(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
            {
                return true;
            }
            Thread.Sleep(10);
        }
        return condition();
    }

    [Fact]
    public void ModlLink_MatchingActiveGame_EnqueuesDirectDownload()
    {
        using var fixture = new Fixture();

        fixture.Buffer.Enqueue(Link());

        Assert.True(WaitUntil(() => fixture.Queue.Enqueued.Count == 1));
        var request = fixture.Queue.Enqueued.Single();
        Assert.Equal("https://example.com/My%20Mod.7z", request.Source.AbsoluteUri);
        Assert.Equal("My Mod.7z", request.Destination.Name);
        Assert.Equal(
            fixture.Paths.DownloadsFolder.FullName,
            request.Destination.Directory?.FullName
        );
        Assert.Empty(fixture.Notifications);
    }

    [Fact]
    public void ModlLink_UnknownGameId_SkipsWithNotification()
    {
        using var fixture = new Fixture();

        fixture.Buffer.Enqueue(Link("falloutnv"));

        Assert.True(WaitUntil(() => fixture.Notifications.Count == 1));
        Assert.Contains("no game handles", fixture.Notifications[0]);
        Assert.Empty(fixture.Queue.Enqueued);
    }

    [Fact]
    public void ModlLink_InstanceMismatch_SkipsWithNotification()
    {
        var other = new SupportedGame(
            "Oblivion",
            [],
            new VendorInfo(0, 0),
            new GamePath("Root", "", [], []),
            [],
            [],
            new Dictionary<string, string> { [ProtocolSchemes.Modl] = SkyrimModlId }
        );
        using var fixture = new Fixture(other);

        fixture.Buffer.Enqueue(Link());

        Assert.True(WaitUntil(() => fixture.Notifications.Count == 1));
        Assert.Contains("active instance", fixture.Notifications[0]);
        Assert.Empty(fixture.Queue.Enqueued);
    }

    [Fact]
    public void ModlLink_RoutesThroughNxmLinksAreIgnored()
    {
        using var fixture = new Fixture();

        fixture.Buffer.Enqueue(
            NxmLink.Parse("nxm://skyrimspecialedition/mods/1/files/2?key=a&expires=1&user_id=1")
        );

        Thread.Sleep(100);
        Assert.Empty(fixture.Queue.Enqueued);
        Assert.Empty(fixture.Notifications);
    }

    [Fact]
    public void ModlLink_CompletedDownload_WritesManifest()
    {
        using var fixture = new Fixture();

        fixture.Buffer.Enqueue(Link());
        Assert.True(WaitUntil(() => fixture.Queue.Enqueued.Count == 1));

        var job = fixture.Queue.Jobs.Single();
        fixture.Queue.MarkCompleted(job.Id, 42);

        var manifestPath = new FileInfo(job.Destination.FullName + ".manifest.json");
        Assert.True(WaitUntil(() => manifestPath.Exists));

        var manifest = DownloadManifestStore.TryRead(job.Destination);
        Assert.NotNull(manifest);
        Assert.Equal("modl", manifest.Repository);
        Assert.Equal(SkyrimModlId, manifest.Game);
        Assert.Equal("My Mod.7z", manifest.FileName);
        Assert.Equal("https://example.com/My%20Mod.7z", manifest.ResolvedUrl);
        Assert.StartsWith("modl://skyrimse/", manifest.SourceLink);
        Assert.Null(manifest.ModId);
        Assert.Null(manifest.FileId);
    }

    [Fact]
    public void ModlLink_NonNbmFilename_SynthesizesDeterministicFallback()
    {
        using var fixture = new Fixture();
        var link = ModlLink.Parse(
            $"modl://{SkyrimModlId}/?url=https%3A%2F%2Fexample.com%2Fdownload-endpoint"
        );

        fixture.Buffer.Enqueue(link);

        Assert.True(WaitUntil(() => fixture.Queue.Enqueued.Count == 1));
        var name = fixture.Queue.Enqueued[0].Destination.Name;
        Assert.StartsWith("modl-skyrimse-", name);
        Assert.EndsWith(".bin", name);
    }
}
