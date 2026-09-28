using System.Reactive.Concurrency;
using System.Reactive.Threading.Tasks;
using Daedalus.Contracts.ModManager;
using Daedalus.Downloads;
using Daedalus.ModManager.GUI.ViewModels;
using Daedalus.VFS;
using Microsoft.Reactive.Testing;
using Xunit;

namespace Daedalus.ModManager.GUI.Tests;

public class DownloadListViewModelTests : IDisposable
{
    private sealed class FakePaths : IModManagerPaths
    {
        public DirectoryInfo AssemblyFolder => new(".");
        public DirectoryInfo InstanceFolder => new(".");
        public DirectoryInfo StagingFolder => new(".");
        public DirectoryInfo ModsFolder => new(".");
        public DirectoryInfo ProfilesFolder => new(".");
        public DirectoryInfo TemporaryFolder => new(".");
        public DirectoryInfo DownloadsFolder { get; } =
            new(Path.Combine(Path.GetTempPath(), $"daedalus-download-vm-tests-{Guid.NewGuid():N}"));
    }

    private sealed class FakeQueue : IDownloadQueue
    {
        private readonly List<DownloadJob> _jobs = [];

        public IReadOnlyList<DownloadJob> Jobs => _jobs.ToList();
        public List<Guid> CancelledIds { get; } = [];

        public event EventHandler<DownloadJob>? JobQueued;
        public event EventHandler<DownloadJob>? JobStarted;
        public event EventHandler<DownloadProgress>? JobProgress;
        public event EventHandler<DownloadCompletion>? JobCompleted;

        public Guid Enqueue(DownloadRequest request)
        {
            var job = CreateJob(request.Id ?? Guid.NewGuid(), request);
            _jobs.Add(job);
            JobQueued?.Invoke(this, job);
            return job.Id;
        }

        public bool Cancel(Guid id)
        {
            CancelledIds.Add(id);
            return true;
        }

        public void MarkRunning(Guid id)
        {
            Replace(id, job => job with { Status = DownloadJobStatus.Running });
            JobStarted?.Invoke(this, _jobs.Single(job => job.Id == id));
        }

        public void EmitProgress(DownloadProgress progress)
        {
            JobProgress?.Invoke(this, progress);
        }

        public void MarkCompleted(Guid id, long totalBytes)
        {
            Replace(
                id,
                job =>
                    job with
                    {
                        Status = DownloadJobStatus.Completed,
                        TotalBytes = totalBytes,
                        FinishedUtc = DateTimeOffset.UtcNow,
                    }
            );
            JobCompleted?.Invoke(
                this,
                new DownloadCompletion(
                    _jobs.Single(job => job.Id == id),
                    new DownloadResult(
                        id,
                        DownloadState.Completed,
                        null,
                        null,
                        totalBytes,
                        totalBytes
                    )
                )
            );
        }

        public void Seed(DownloadJob job)
        {
            _jobs.Add(job);
        }

        private void Replace(Guid id, Func<DownloadJob, DownloadJob> transform)
        {
            var job = _jobs.Single(job => job.Id == id);
            _jobs.RemoveAll(candidate => candidate.Id == id);
            _jobs.Add(transform(job));
        }

        private static DownloadJob CreateJob(Guid id, DownloadRequest request)
        {
            return new DownloadJob
            {
                Id = id,
                Source = request.Source,
                Destination = request.Destination,
                DisplayName = request.Destination.Name,
                QueuedUtc = DateTimeOffset.UtcNow,
                Status = DownloadJobStatus.Queued,
            };
        }
    }

    private readonly FakePaths _paths = new();

    public void Dispose()
    {
        _paths.DownloadsFolder.Refresh();
        if (_paths.DownloadsFolder.Exists)
        {
            _paths.DownloadsFolder.Delete(recursive: true);
        }
    }

    private (FakeQueue Queue, DownloadListViewModel ViewModel) CreateViewModel(
        IScheduler? scheduler = null
    )
    {
        var queue = new FakeQueue();
        var effective = scheduler ?? Scheduler.Immediate;
        return (queue, new DownloadListViewModel(queue, _paths, null, null, effective, effective));
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
    public void Enqueue_CreatesQueuedRow()
    {
        var (queue, viewModel) = CreateViewModel();
        using (viewModel)
        {
            queue.Enqueue(Request());

            var row = Assert.Single(viewModel.Rows);
            Assert.Equal("mod.7z", row.DisplayName);
            Assert.True(row.IsQueued);
            Assert.False(row.IsFinished);
            Assert.Equal("Queued", row.StatusText);
            Assert.Equal("1 active", viewModel.SummaryText);
        }
    }

    [Fact]
    public void Progress_Sampled_UntilClockAdvances()
    {
        var scheduler = new TestScheduler();
        var (queue, viewModel) = CreateViewModel(scheduler);
        using (viewModel)
        {
            var id = queue.Enqueue(Request());
            queue.MarkRunning(id);
            scheduler.AdvanceBy(TimeSpan.FromMilliseconds(100).Ticks);

            queue.EmitProgress(new DownloadProgress(id, 25, 100, 25, 100));
            queue.EmitProgress(new DownloadProgress(id, 60, 100, 60, 100));
            Assert.Equal(0, viewModel.Rows[0].Percentage);

            scheduler.AdvanceBy(TimeSpan.FromMilliseconds(250).Ticks);

            var row = viewModel.Rows[0];
            Assert.Equal(60, row.Percentage);
            Assert.Equal("100 B/s", row.SpeedText);
            Assert.Equal("1s", row.EtaText);
            Assert.Contains("60 B of 100 B", row.DetailText);
            Assert.Contains("~1s left", row.DetailText);
        }
    }

    [Fact]
    public void Completion_TransitionsRowAndMarksFinished()
    {
        var (queue, viewModel) = CreateViewModel();
        using (viewModel)
        {
            var id = queue.Enqueue(Request());
            queue.MarkRunning(id);

            queue.MarkCompleted(id, 1234);

            var row = Assert.Single(viewModel.Rows);
            Assert.True(row.IsFinished);
            Assert.Equal("Completed", row.StatusText);
            Assert.Equal(100, row.Percentage);
            Assert.Equal(1234, row.ReceivedBytes);
            Assert.Equal("1 finished", viewModel.SummaryText);
        }
    }

    [Fact]
    public void SummaryText_ReRaisesOnRowStateTransition()
    {
        var (queue, viewModel) = CreateViewModel();
        using (viewModel)
        {
            var notifications = new List<string>();
            viewModel.PropertyChanged += (_, args) => notifications.Add(args.PropertyName ?? "");
            var id = queue.Enqueue(Request());
            notifications.Clear();

            queue.MarkRunning(id);
            Assert.DoesNotContain("SummaryText", notifications);

            queue.MarkCompleted(id, 100);
            Assert.Contains("SummaryText", notifications);
            Assert.Equal("1 finished", viewModel.SummaryText);
        }
    }

    [Fact]
    public void FolderWatcher_Rename_MovesRowWithoutGhost()
    {
        _paths.DownloadsFolder.Create();
        var original = Path.Combine(_paths.DownloadsFolder.FullName, "Old.7z");
        var renamed = Path.Combine(_paths.DownloadsFolder.FullName, "New.7z");
        File.WriteAllBytes(original, new byte[8]);
        var (queue, viewModel) = CreateViewModel();
        using (viewModel)
        {
            Assert.True(WaitUntil(() => viewModel.Rows.Count == 1));
            Assert.Equal("Old.7z", viewModel.Rows[0].DisplayName);

            File.Move(original, renamed);

            Assert.True(
                WaitUntil(() =>
                    viewModel.Rows.Count == 1 && viewModel.Rows[0].DisplayName == "New.7z"
                )
            );
        }
    }

    [Fact]
    public void CancelCommand_ForwardsToQueue()
    {
        var (queue, viewModel) = CreateViewModel();
        using (viewModel)
        {
            var id = queue.Enqueue(Request());

            viewModel.Rows[0].CancelCommand.Execute().Subscribe();

            Assert.Equal(id, Assert.Single(queue.CancelledIds));
        }
    }

    [Fact]
    public void Constructor_SeedsExistingQueueJobs()
    {
        var queue = new FakeQueue();
        queue.Seed(
            new DownloadJob
            {
                Id = Guid.NewGuid(),
                Source = new Uri("https://example.com/old.7z"),
                Destination = new FileInfo(Path.Combine(Path.GetTempPath(), "old.7z")),
                DisplayName = "old.7z",
                QueuedUtc = DateTimeOffset.UtcNow.AddMinutes(-1),
                Status = DownloadJobStatus.Completed,
                TotalBytes = 10,
                FinishedUtc = DateTimeOffset.UtcNow,
            }
        );

        using var viewModel = new DownloadListViewModel(
            queue,
            _paths,
            null,
            null,
            Scheduler.Immediate,
            Scheduler.Immediate
        );

        var row = Assert.Single(viewModel.Rows);
        Assert.Equal("old.7z", row.DisplayName);
        Assert.True(row.IsFinished);
        Assert.Equal("10 B of 10 B", row.ReceivedText);
    }

    [Fact]
    public void Constructor_SeedsExistingDownloadsFromDisk()
    {
        _paths.DownloadsFolder.Create();
        var existing = Path.Combine(_paths.DownloadsFolder.FullName, "SkyUI.7z");
        File.WriteAllBytes(existing, new byte[64]);
        var (queue, viewModel) = CreateViewModel();
        using (viewModel)
        {
            var row = Assert.Single(viewModel.Rows);
            Assert.Equal("SkyUI.7z", row.DisplayName);
            Assert.True(row.IsFinished);
            Assert.Equal(64, row.Job.TotalBytes);
        }
    }

    [Fact]
    public void FolderWatcher_FileAdded_CreatesRow()
    {
        var (queue, viewModel) = CreateViewModel();
        using (viewModel)
        {
            var target = Path.Combine(_paths.DownloadsFolder.FullName, "FreshMod.7z");

            File.WriteAllBytes(target, new byte[32]);

            Assert.True(WaitUntil(() => viewModel.Rows.Count == 1));
            Assert.Equal("FreshMod.7z", viewModel.Rows[0].DisplayName);
            Assert.True(viewModel.Rows[0].IsFinished);
        }
    }

    [Fact]
    public void FolderWatcher_FileDeleted_RemovesRow()
    {
        _paths.DownloadsFolder.Create();
        var existing = Path.Combine(_paths.DownloadsFolder.FullName, "SkyUI.7z");
        File.WriteAllBytes(existing, new byte[64]);
        var (queue, viewModel) = CreateViewModel();
        using (viewModel)
        {
            Assert.Single(viewModel.Rows);

            File.Delete(existing);

            Assert.True(WaitUntil(() => viewModel.Rows.Count == 0));
        }
    }

    [Fact]
    public void FolderWatcher_PackageBookkeepingFile_Ignored()
    {
        var (queue, viewModel) = CreateViewModel();
        using (viewModel)
        {
            var bookkeeping = Path.Combine(_paths.DownloadsFolder.FullName, "mod.7z.download");
            File.WriteAllText(bookkeeping, "{}");
            Thread.Sleep(150);

            Assert.Empty(viewModel.Rows);
        }
    }

    [Fact]
    public void Enqueue_MatchesDiskRowByDestination_AdoptsInsteadOfDuplicating()
    {
        _paths.DownloadsFolder.Create();
        var destination = Path.Combine(_paths.DownloadsFolder.FullName, "SkyUI.7z");
        File.WriteAllBytes(destination, new byte[1]);
        var (queue, viewModel) = CreateViewModel();
        using (viewModel)
        {
            Assert.Single(viewModel.Rows);

            queue.Enqueue(
                new DownloadRequest(new Uri("https://example.com/skyui"), new FileInfo(destination))
            );

            var row = Assert.Single(viewModel.Rows);
            Assert.True(row.IsQueued);
            Assert.Equal("1 active", viewModel.SummaryText);
        }
    }

    private sealed class FakeInstallService : IModInstallService
    {
        public event EventHandler<InstallProgress>? InstallProgressChanged;

        public int Calls { get; private set; }
        public string? LastName { get; private set; }
        public string? LastVersion { get; private set; }
        public ModID? LastProvenanceId { get; private set; }
        public bool Succeed = true;
        public TaskCompletionSource? Gate;

        public Task<ILibraryMod?> InstallAsync(
            string name,
            string? version,
            FileInfo archive,
            ModID? provenanceId = null,
            CancellationToken cancellationToken = default
        )
        {
            Calls++;
            LastName = name;
            LastVersion = version;
            LastProvenanceId = provenanceId;
            var gate = Gate;
            if (gate is not null)
            {
                return AwaitGate(gate, name);
            }
            return Task.FromResult<ILibraryMod?>(
                Succeed ? new StubLibraryMod(name) : null
            );
        }

        private async Task<ILibraryMod?> AwaitGate(TaskCompletionSource gate, string name)
        {
            await gate.Task;
            return Succeed ? new StubLibraryMod(name) : null;
        }

        public void EmitProgress(InstallProgress progress)
        {
            InstallProgressChanged?.Invoke(this, progress);
        }
    }

    private sealed class StubLibraryMod(string name) : ILibraryMod
    {
        public IModInfo Info => throw new NotSupportedException();
        public string Name => name;
        public DirectoryInfo Directory => new(".");
        public VirtualNode<ModFileEntry> Content => throw new NotSupportedException();

        public void RefreshContent() { }
        public void RenameTo(string newName) { }
        public bool Equals(ILibraryMod? x, ILibraryMod? y) => ReferenceEquals(x, y);
        public int GetHashCode(ILibraryMod obj) => 0;
        public bool Equals(ILibraryMod? other) => ReferenceEquals(this, other);
    }

    private (FakeQueue Queue, DownloadListViewModel ViewModel, FakeInstallService Installer, List<ILibraryMod> Installed)
        CreateInstallViewModel(IScheduler? sample = null, IScheduler? notify = null)
    {
        var queue = new FakeQueue();
        var installer = new FakeInstallService();
        var installed = new List<ILibraryMod>();
        return (
            queue,
            new DownloadListViewModel(
                queue,
                _paths,
                installer,
                installed.Add,
                sample ?? Scheduler.Default,
                notify ?? Scheduler.Immediate
            ),
            installer,
            installed
        );
    }

    private DownloadRowViewModel AddCompletedRowViaQueue(DownloadListViewModel viewModel, FakeQueue queue, string name)
    {
        var id = queue.Enqueue(new DownloadRequest(
            new Uri($"https://example.com/{name}"),
            new FileInfo(Path.Combine(_paths.DownloadsFolder.FullName, name))
        ));
        queue.MarkRunning(id);
        queue.MarkCompleted(id, 100);
        return viewModel.Rows.Single(row => row.Job.Id == id);
    }

    [Fact]
    public async Task InstallProgress_UpdatesRowPercentageAndDetail()
    {
        var (queue, viewModel, installer, installed) = CreateInstallViewModel(
            sample: Scheduler.Default,
            notify: Scheduler.Immediate
        );
        using (viewModel)
        {
            var row = AddCompletedRowViaQueue(viewModel, queue, "SkyUI.7z");

            installer.Gate = new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously
            );
            var installTask = row.InstallCommand.Execute().ToTask();
            Assert.True(WaitUntil(() => row.IsInstalling));

            installer.EmitProgress(
                new InstallProgress(row.Job.Destination, "Data/SKSE/plugin.dll", 50, 100, 50.0)
            );

            Assert.True(WaitUntil(() => row.InstallPercentage == 50.0));
            Assert.Equal(50.0, row.ProgressValue);
            Assert.True(WaitUntil(() => row.DetailText == "Installing Data/SKSE/plugin.dll"));

            installer.Gate.SetResult();
            await installTask;
            Assert.True(row.DidInstall);
        }
    }

    [Fact]
    public async Task InstallCommand_OnCompletedRow_InstallsAndRegisters()
    {
        var (queue, viewModel, installer, installed) = CreateInstallViewModel();
        using (viewModel)
        {
            var row = AddCompletedRowViaQueue(viewModel, queue, "SkyUI.7z");

            Assert.True(row.InstallVisible);
            await row.InstallCommand.Execute().ToTask();

            Assert.Equal(1, installer.Calls);
            Assert.Equal("SkyUI", installer.LastName);
            Assert.True(row.DidInstall);
            Assert.Equal("Installed", row.StatusText);
            var mod = Assert.Single(installed);
            Assert.Equal("SkyUI", mod.Name);
            Assert.False(row.InstallVisible);
        }
    }

    [Fact]
    public async Task InstallCommand_ServiceFailure_MarksFailed()
    {
        var (queue, viewModel, installer, installed) = CreateInstallViewModel();
        using (viewModel)
        {
            installer.Succeed = false;
            var row = AddCompletedRowViaQueue(viewModel, queue, "SkyUI.7z");

            await row.InstallCommand.Execute().ToTask();

            Assert.False(row.DidInstall);
            Assert.True(row.InstallFailed);
            Assert.Equal("Install failed", row.StatusText);
            Assert.Empty(installed);
        }
    }

    [Fact]
    public async Task InstallCommand_UsesManifestNameAndVersion()
    {
        var (queue, viewModel, installer, installed) = CreateInstallViewModel();
        using (viewModel)
        {
            var row = AddCompletedRowViaQueue(viewModel, queue, "archive.7z");
            DownloadManifestStore.Write(
                row.Job.Destination,
                new DownloadManifest
                {
                    Repository = "nxm",
                    ModId = 12604,
                    FileId = 360415,
                    Version = "6.1",
                    FileName = "SkyUI-12604-6-11-1778020881.zip",
                    DownloadedUtc = DateTimeOffset.UtcNow,
                }
            );

            await row.InstallCommand.Execute().ToTask();

            Assert.Equal("SkyUI-12604-6-11-1778020881", installer.LastName);
            Assert.Equal("6.1", installer.LastVersion);
            Assert.Equal(new ModID(12604, SourceType.NexusMods), installer.LastProvenanceId);
        }
    }

    [Fact]
    public async Task InstallCommand_ModlManifest_MapsToModPubSource()
    {
        var (queue, viewModel, installer, installed) = CreateInstallViewModel();
        using (viewModel)
        {
            var row = AddCompletedRowViaQueue(viewModel, queue, "My Mod.7z");
            DownloadManifestStore.Write(
                row.Job.Destination,
                new DownloadManifest
                {
                    Repository = "modl",
                    ModId = null,
                    DownloadedUtc = DateTimeOffset.UtcNow,
                }
            );

            await row.InstallCommand.Execute().ToTask();

            Assert.Null(installer.LastProvenanceId);
        }
    }

    private static DownloadRequest Request(string name = "mod.7z")
    {
        return new DownloadRequest(
            new Uri($"https://example.com/{name}"),
            new FileInfo(Path.Combine(Path.GetTempPath(), name))
        );
    }
}
