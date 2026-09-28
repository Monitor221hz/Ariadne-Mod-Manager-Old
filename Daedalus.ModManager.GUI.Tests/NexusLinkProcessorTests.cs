using Daedalus.Contracts.Games;
using Daedalus.Contracts.ModManager;
using Daedalus.Downloads;
using Daedalus.Games;
using Daedalus.ModManager.GUI;
using Daedalus.ModManager.GUI.ViewModels;
using Daedalus.Security;
using Daedalus.WebProtocol.Nexus;
using Xunit;

namespace Daedalus.ModManager.GUI.Tests;

[Collection("EnvironmentSensitive")]
public class NexusLinkProcessorTests
{
    private const string SkyrimNexusDomain = "skyrimspecialedition";

    private sealed class InMemorySecrets : ISecretStore
    {
        private readonly Dictionary<string, string> _values = new();

        public Task<string?> GetAsync(string key, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_values.TryGetValue(key, out var value) ? value : null);
        }

        public Task SetAsync(
            string key,
            string value,
            CancellationToken cancellationToken = default
        )
        {
            _values[key] = value;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
        {
            _values.Remove(key);
            return Task.CompletedTask;
        }
    }

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

        public void Remove(string name, bool deleteFolderIndex) { }

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
            new(Path.Combine(Path.GetTempPath(), $"daedalus-processor-tests-{Guid.NewGuid():N}"));
    }

    private sealed class ThrowingPaths : IModManagerPaths
    {
        public DirectoryInfo AssemblyFolder => new(".");
        public DirectoryInfo InstanceFolder => new(".");
        public DirectoryInfo StagingFolder => new(".");
        public DirectoryInfo ModsFolder => new(".");
        public DirectoryInfo ProfilesFolder => new(".");
        public DirectoryInfo TemporaryFolder => new(".");
        public DirectoryInfo DownloadsFolder =>
            throw new InvalidOperationException("No active instance.");
    }

#pragma warning disable CS0067
    private sealed class FakeAccountCache : INexusAccountCache
    {
        private readonly Dictionary<string, NexusAccount> _accounts = new();

        public NexusAccount? Read(string apiKey)
        {
            return _accounts.TryGetValue(apiKey, out var account) ? account : null;
        }

        public void Write(string apiKey, NexusAccount account)
        {
            _accounts[apiKey] = account;
        }

        public void Clear()
        {
            _accounts.Clear();
        }
    }
#pragma warning restore CS0067

    private sealed class FakeFiles : INexusFileClient
    {
        public int Calls { get; private set; }
        public NexusFileMetadata? Metadata = new(
            360415,
            "SkyUI",
            "SkyUI_5_2_SE-12604-5-2SE.7z",
            "5.2SE",
            2635045
        );

        public Task<NexusFileMetadata?> GetFileAsync(
            NxmModLink link,
            string apiKey,
            CancellationToken cancellationToken = default
        )
        {
            Calls++;
            return Task.FromResult(Metadata);
        }
    }

    private sealed class FakeResolver : INexusDownloadResolver
    {
        public int? DailyRequestsLimit => null;
        public int? DailyRequestsRemaining => null;
        public int Calls { get; private set; }
        public string? LastApiKey { get; private set; }
        public bool LastPremium { get; private set; }
        public NexusDownloadLink? Result = new(
            "Nexus CDN",
            "Nexus CDN",
            new Uri("https://cdn.nexusmods.com/SkyUI_5_2_SE.7z")
        );

        public Task<NexusDownloadLink?> ResolveDownloadAsync(
            NxmModLink link,
            string apiKey,
            bool isPremium,
            string? serverName = null,
            CancellationToken cancellationToken = default
        )
        {
            Calls++;
            LastApiKey = apiKey;
            LastPremium = isPremium;
            return Task.FromResult(Result);
        }
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
        public InMemorySecrets Secrets { get; } = new();
        public FakeCatalog Catalog { get; }
        public FakeInstances Instances { get; }
        public IModManagerPaths Paths { get; } = new FakePaths();
        public FakeAccountCache AccountCache { get; } = new();
        public FakeFiles Files { get; } = new();
        public FakeResolver Resolver { get; } = new();
        public FakeQueue Queue { get; } = new();
        public List<string> Notifications { get; } = [];
        public NexusLinkProcessor Processor { get; }

        public Fixture(ISupportedGame? currentInstanceGame = null, IModManagerPaths? paths = null)
        {
#if DEBUG
            _priorDevKey = Environment.GetEnvironmentVariable(
                SourcesMenuViewModel.DevKeyEnvironmentVariable
            );
            Environment.SetEnvironmentVariable(
                SourcesMenuViewModel.DevKeyEnvironmentVariable,
                null
            );
#endif
            Skyrim = new SupportedGame(
                "Skyrim Special Edition",
                [],
                new VendorInfo(0, 0),
                new GamePath("Root", "", [], []),
                [],
                [],
                new Dictionary<string, string> { [ProtocolSchemes.Nxm] = SkyrimNexusDomain }
            );
            Catalog = new FakeCatalog(Skyrim);
            Instances = new FakeInstances(currentInstanceGame ?? Skyrim);
            if (paths is not null)
            {
                Paths = paths;
            }
            _ = Secrets.SetAsync("nexus-api-key", "stored-key");
            AccountCache.Write("stored-key", new NexusAccount("Tester", true, null));
            Processor = new NexusLinkProcessor(
                Buffer,
                Catalog,
                Instances,
                Paths,
                Secrets,
                AccountCache,
                Files,
                Resolver,
                Queue
            );
            Processor.Notification += (_, message) => Notifications.Add(message);
        }

#if DEBUG
        private readonly string? _priorDevKey;
#endif

        public void Dispose()
        {
            Processor.Dispose();
#if DEBUG
            Environment.SetEnvironmentVariable(
                SourcesMenuViewModel.DevKeyEnvironmentVariable,
                _priorDevKey
            );
#endif
            try
            {
                Paths.DownloadsFolder.Refresh();
                if (Paths.DownloadsFolder.Exists)
                {
                    Paths.DownloadsFolder.Delete(recursive: true);
                }
            }
            catch (InvalidOperationException) { }
        }
    }

    private static NxmModLink ModLink(string domain = SkyrimNexusDomain)
    {
        return (NxmModLink)
            NxmLink.Parse(
                $"nxm://{domain}/mods/12604/files/360415?key=abc&expires=1893456000&user_id=1"
            );
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
    public void ModLink_MatchingActiveGame_ResolvesAndEnqueues()
    {
        using var fixture = new Fixture();

        fixture.Buffer.Enqueue(ModLink());

        Assert.True(WaitUntil(() => fixture.Queue.Enqueued.Count == 1));
        var request = fixture.Queue.Enqueued.Single();
        Assert.Equal("https://cdn.nexusmods.com/SkyUI_5_2_SE.7z", request.Source.AbsoluteUri);
        Assert.Equal("SkyUI_5_2_SE-12604-5-2SE.7z", request.Destination.Name);
        Assert.Equal(
            fixture.Paths.DownloadsFolder.FullName,
            request.Destination.Directory?.FullName
        );
        Assert.Equal("stored-key", fixture.Resolver.LastApiKey);
        Assert.True(fixture.Resolver.LastPremium);
        Assert.Empty(fixture.Notifications);
    }

    [Fact]
    public void CollectionLink_IsIgnored()
    {
        using var fixture = new Fixture();

        fixture.Buffer.Enqueue(
            NxmLink.Parse("nxm://skyrimspecialedition/collections/slug/revisions/1")
        );

        Thread.Sleep(100);
        Assert.Empty(fixture.Queue.Enqueued);
        Assert.Empty(fixture.Notifications);
    }

    [Fact]
    public void ModLink_WithUnknownDomain_SkipsWithNotification()
    {
        using var fixture = new Fixture();

        fixture.Buffer.Enqueue(ModLink("stardewvalley"));

        Assert.True(WaitUntil(() => fixture.Notifications.Count == 1));
        Assert.Contains("no game handles", fixture.Notifications[0]);
        Assert.Empty(fixture.Queue.Enqueued);
    }

    [Fact]
    public void ModLink_GameMismatchedToInstance_SkipsWithNotification()
    {
        var otherGame = new SupportedGame(
            "Oblivion",
            [],
            new VendorInfo(0, 0),
            new GamePath("Root", "", [], []),
            [],
            [],
            new Dictionary<string, string> { [ProtocolSchemes.Nxm] = "oblivion" }
        );
        using var fixture = new Fixture(otherGame);

        fixture.Buffer.Enqueue(ModLink());

        Assert.True(WaitUntil(() => fixture.Notifications.Count == 1));
        Assert.Contains("active instance", fixture.Notifications[0]);
        Assert.Equal(0, fixture.Resolver.Calls);
        Assert.Empty(fixture.Queue.Enqueued);
    }

    [Fact]
    public void ModLink_WithoutKey_SkipsWithNotification()
    {
        using var fixture = new Fixture();
        _ = fixture.Secrets.DeleteAsync("nexus-api-key");

        fixture.Buffer.Enqueue(ModLink());

        Assert.True(WaitUntil(() => fixture.Notifications.Count == 1));
        Assert.Contains("not signed in", fixture.Notifications[0]);
        Assert.Equal(0, fixture.Resolver.Calls);
    }

    [Fact]
    public void ModLink_CompletedDownload_WritesManifestBesidesArchive()
    {
        using var fixture = new Fixture();

        fixture.Buffer.Enqueue(ModLink());
        Assert.True(WaitUntil(() => fixture.Queue.Enqueued.Count == 1));

        var job = fixture.Queue.Jobs.Single();
        fixture.Queue.MarkCompleted(job.Id, 100);

        var manifestPath = new FileInfo(job.Destination.FullName + ".manifest.json");
        Assert.True(WaitUntil(() => manifestPath.Exists));

        var manifest = DownloadManifestStore.TryRead(job.Destination);
        Assert.NotNull(manifest);
        Assert.Equal("nxm", manifest.Repository);
        Assert.Equal(SkyrimNexusDomain, manifest.Game);
        Assert.Equal(12604, manifest.ModId);
        Assert.Equal(360415, manifest.FileId);
        Assert.Equal("SkyUI", manifest.ModName);
        Assert.Equal("5.2SE", manifest.Version);
        Assert.Equal("SkyUI_5_2_SE-12604-5-2SE.7z", manifest.FileName);
        Assert.Equal(2635045, manifest.SizeInBytes);
        Assert.Equal("https://cdn.nexusmods.com/SkyUI_5_2_SE.7z", manifest.ResolvedUrl);
        Assert.StartsWith("nxm://", manifest.SourceLink);
        Assert.Contains("mods/12604/files/360415", manifest.SourceLink);
    }

    [Fact]
    public void ModLink_WithoutCachedAccount_SkipsWithSignInNotification()
    {
        using var fixture = new Fixture();
        fixture.AccountCache.Clear();

        fixture.Buffer.Enqueue(ModLink());

        Assert.True(WaitUntil(() => fixture.Notifications.Count == 1));
        Assert.Contains("sign in", fixture.Notifications[0]);
        Assert.Empty(fixture.Queue.Enqueued);
    }

    [Fact]
    public void ModLink_Unresolvable_NotifiesWithoutEnqueue()
    {
        using var fixture = new Fixture();
        fixture.Resolver.Result = null;

        fixture.Buffer.Enqueue(ModLink());

        Assert.True(WaitUntil(() => fixture.Notifications.Count == 1));
        Assert.Contains("Could not resolve", fixture.Notifications[0]);
        Assert.Empty(fixture.Queue.Enqueued);
    }

    [Fact]
    public void ModLink_UnresolvableUrlAndNoMetadata_UsesSynthesizedFallbackName()
    {
        using var fixture = new Fixture();
        fixture.Files.Metadata = null;
        fixture.Resolver.Result = new NexusDownloadLink(
            "Nexus CDN",
            "Nexus CDN",
            new Uri("https://cdn.nexusmods.com/blob/12345")
        );

        fixture.Buffer.Enqueue(ModLink());

        Assert.True(WaitUntil(() => fixture.Queue.Enqueued.Count == 1));
        Assert.Equal("nexus-mod-12604-file-360415.bin", fixture.Queue.Enqueued[0].Destination.Name);
    }

    [Fact]
    public void ModLink_MetadataUnavailable_UsesUrlFileName()
    {
        using var fixture = new Fixture();
        fixture.Files.Metadata = null;

        fixture.Buffer.Enqueue(ModLink());

        Assert.True(WaitUntil(() => fixture.Queue.Enqueued.Count == 1));
        Assert.Equal("SkyUI_5_2_SE.7z", fixture.Queue.Enqueued[0].Destination.Name);
    }

    [Fact]
    public void ModLink_NonPremium_PassesPremiumFalse()
    {
        using var fixture = new Fixture();
        fixture.AccountCache.Write("stored-key", new NexusAccount("Tester", false, null));

        fixture.Buffer.Enqueue(ModLink());

        Assert.True(WaitUntil(() => fixture.Queue.Enqueued.Count == 1));
        Assert.False(fixture.Resolver.LastPremium);
    }

    [Fact]
    public void ModLink_PathsBroken_NotifiesInsteadOfCrashing()
    {
        using var fixture = new Fixture(paths: new ThrowingPaths());

        fixture.Buffer.Enqueue(ModLink());

        Assert.True(WaitUntil(() => fixture.Notifications.Count == 1));
        Assert.Contains("failed to start", fixture.Notifications[0]);
        Assert.Empty(fixture.Queue.Enqueued);
    }

#if DEBUG
    [Fact]
    public void ModLink_WithDevKey_UsesEnvironmentKeyOverStore()
    {
        using var fixture = new Fixture();
        fixture.AccountCache.Write("dev-key", new NexusAccount("Tester", true, null));
        Environment.SetEnvironmentVariable(
            SourcesMenuViewModel.DevKeyEnvironmentVariable,
            "dev-key"
        );
        try
        {
            fixture.Buffer.Enqueue(ModLink());

            Assert.True(WaitUntil(() => fixture.Queue.Enqueued.Count == 1));
            Assert.Equal("dev-key", fixture.Resolver.LastApiKey);
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                SourcesMenuViewModel.DevKeyEnvironmentVariable,
                null
            );
        }
    }
#endif
}
