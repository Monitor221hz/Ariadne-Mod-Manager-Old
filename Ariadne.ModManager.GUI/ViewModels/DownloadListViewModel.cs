using System.Collections.Specialized;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Ariadne.Contracts.Games;
using Ariadne.Contracts.ModManager;
using Ariadne.Downloads;
using CP.Reactive.Collections;
using ReactiveUI;
using ReactiveUI.Avalonia;

namespace Ariadne.ModManager.GUI.ViewModels;

public sealed class DownloadListViewModel : ViewModelBase, IWorkspaceTab, IDisposable
{
    private static readonly TimeSpan ProgressSampleInterval = TimeSpan.FromMilliseconds(200);

    private readonly IDownloadQueue _queue;
    private readonly DirectoryInfo _downloadsFolder;
    private readonly IModInstallService? _installService;
    private readonly Action<ILibraryMod>? _onInstalled;
    private readonly IReadOnlyList<IGamePath> _installTargets;
    private readonly Dictionary<Guid, DownloadRowViewModel> _rowsById = new();
    private readonly Dictionary<string, DownloadRowViewModel> _rowsByPath = new(
        StringComparer.OrdinalIgnoreCase
    );
    private readonly Dictionary<Guid, IDisposable> _rowSubscriptions = new();
    private readonly CompositeDisposable _subscriptions = new();
    private FileSystemWatcher? _watcher;

    public DownloadListViewModel(
        IDownloadQueue queue,
        IModManagerPaths paths,
        IInstanceService? instances = null,
        IModInstallService? installService = null,
        Action<ILibraryMod>? onInstalled = null,
        IScheduler? sampleScheduler = null,
        IScheduler? notifyScheduler = null
    )
    {
        _queue = queue;
        _downloadsFolder = paths.DownloadsFolder;
        _installService = installService;
        _onInstalled = onInstalled;
        _installTargets =
            installService is not null
            && instances?.Current?.Game?.Configuration.InstallTargets is { } targets
                ? targets
                : [];
        sampleScheduler ??= Scheduler.Default;
        notifyScheduler ??= AvaloniaScheduler.Instance;

        var queued = EventStream<DownloadJob>(
            handler => queue.JobQueued += handler,
            handler => queue.JobQueued -= handler
        );
        var started = EventStream<DownloadJob>(
            handler => queue.JobStarted += handler,
            handler => queue.JobStarted -= handler
        );
        var completed = EventStream<DownloadCompletion>(
            handler => queue.JobCompleted += handler,
            handler => queue.JobCompleted -= handler
        );
        var progress = EventStream<DownloadProgress>(
            handler => queue.JobProgress += handler,
            handler => queue.JobProgress -= handler
        );

        _subscriptions.Add(queued.ObserveOn(notifyScheduler).Subscribe(OnJobQueued));
        _subscriptions.Add(started.ObserveOn(notifyScheduler).Subscribe(OnJobChanged));
        _subscriptions.Add(
            completed
                .ObserveOn(notifyScheduler)
                .Subscribe(completion => OnJobChanged(completion.Job))
        );
        if (installService is not null)
        {
            var installProgress = EventStream<InstallProgress>(
                handler => installService.InstallProgressChanged += handler,
                handler => installService.InstallProgressChanged -= handler
            );
            _subscriptions.Add(
                installProgress
                    .GroupBy(p => p.Archive.FullName)
                    .SelectMany(group => group.Sample(ProgressSampleInterval, sampleScheduler))
                    .ObserveOn(notifyScheduler)
                    .Subscribe(ApplyInstallProgress)
            );
        }
        _subscriptions.Add(
            progress
                .GroupBy(p => p.Id)
                .SelectMany(group => group.Sample(ProgressSampleInterval, sampleScheduler))
                .ObserveOn(notifyScheduler)
                .Subscribe(ApplyProgress)
        );

        foreach (var job in _queue.Jobs.OrderBy(job => job.QueuedUtc))
        {
            OnJobQueued(job);
        }
        SeedFromFolder();

        _subscriptions.Add(WatchFolder(notifyScheduler));

        NotifyCollectionChangedEventHandler rowsChangedHandler = (_, _) =>
        {
            this.RaisePropertyChanged(nameof(HasRows));
            this.RaisePropertyChanged(nameof(SummaryText));
        };
        Rows.CollectionChanged += rowsChangedHandler;
        _subscriptions.Add(
            System.Reactive.Disposables.Disposable.Create(() =>
            {
                Rows.CollectionChanged -= rowsChangedHandler;
            })
        );
    }

    public string Title => "Downloads";

    public ReactiveList<DownloadRowViewModel> Rows { get; } = [];

    public bool HasRows => Rows.Count > 0;

    public string SummaryText
    {
        get
        {
            var active = Rows.Count(row => !row.IsFinished);
            var finished = Rows.Count(row => row.IsFinished);
            return (active, finished) switch
            {
                (0, 0) => "No downloads",
                (> 0, 0) => $"{active} active",
                (0, > 0) => $"{finished} finished",
                _ => $"{active} active · {finished} finished",
            };
        }
    }

    public Task EnsureInitializedAsync()
    {
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _subscriptions.Dispose();
        foreach (var subscription in _rowSubscriptions.Values)
        {
            subscription.Dispose();
        }
        _rowSubscriptions.Clear();
        _watcher?.Dispose();
    }

    private static IObservable<T> EventStream<T>(
        Action<EventHandler<T>> add,
        Action<EventHandler<T>> remove
    )
    {
        return Observable
            .FromEventPattern<EventHandler<T>, T>(add, remove)
            .Select(pattern => pattern.EventArgs);
    }

    private void OnJobQueued(DownloadJob job)
    {
        if (_rowsById.TryGetValue(job.Id, out var existing))
        {
            existing.Apply(job);
            return;
        }

        if (_rowsByPath.TryGetValue(job.Destination.FullName, out var seeded))
        {
            _rowsById.Remove(seeded.Job.Id);
            _rowsById[job.Id] = seeded;
            if (_rowSubscriptions.Remove(seeded.Job.Id, out var subscription))
            {
                _rowSubscriptions[job.Id] = subscription;
            }
            seeded.Apply(job);
            return;
        }

        AddRow(job);
    }

    private void OnJobChanged(DownloadJob job)
    {
        if (_rowsById.ContainsKey(job.Id))
        {
            _rowsById[job.Id].Apply(job);
            return;
        }
        OnJobQueued(job);
    }

    private void ApplyProgress(DownloadProgress progress)
    {
        if (_rowsById.TryGetValue(progress.Id, out var row))
        {
            row.ApplyProgress(progress);
        }
    }

    private void ApplyInstallProgress(InstallProgress progress)
    {
        if (!_rowsByPath.TryGetValue(progress.Archive.FullName, out var row))
        {
            return;
        }

        row.SetLiveText(
            progress.EntryPath.Length > 0 ? $"Installing {progress.EntryPath}" : "Installing"
        );
    }

    private async Task InstallAsync(DownloadJob job, IGamePath? target = null)
    {
        if (_installService is null)
        {
            return;
        }
        if (!_rowsById.TryGetValue(job.Id, out var row))
        {
            return;
        }

        row.IsInstalling = true;
        try
        {
            var manifest = DownloadManifestStore.TryRead(row.Job.Destination);
            var name = manifest?.ModFileName is { Length: > 0 } modFileName
                ? modFileName
                : Path.GetFileNameWithoutExtension(manifest?.FileName ?? row.Job.Destination.Name);
            var mod = await _installService.InstallAsync(
                name,
                manifest?.Version,
                row.Job.Destination,
                ManifestProvenance(manifest),
                InstallType.Replace,
                target
            );
            if (mod is null)
            {
                row.InstallFailed = true;
                return;
            }
            _onInstalled?.Invoke(mod);
        }
        finally
        {
            row.IsInstalling = false;
        }
    }

    private static ModID? ManifestProvenance(DownloadManifest? manifest)
    {
        if (manifest?.ModId is not { } modId)
        {
            return null;
        }

        var source = manifest.Repository switch
        {
            ProtocolSchemes.Nxm => SourceType.NexusMods,
            ProtocolSchemes.Modl => SourceType.ModPub,
            _ => SourceType.Local,
        };
        return new ModID((ulong)modId, source);
    }

    private void AddRow(DownloadJob job)
    {
        Func<IGamePath?, Task>? installHandler = _installService is not null
            ? target => InstallAsync(job, target)
            : null;
        var row = new DownloadRowViewModel(
            job,
            id => _queue.Cancel(id),
            installHandler,
            _installTargets
        );
        row.InstallCommand.ThrownExceptions.Subscribe(_ => row.InstallFailed = true);
        row.InstallToCommand.ThrownExceptions.Subscribe(_ => row.InstallFailed = true);
        _rowsById[job.Id] = row;
        _rowsByPath[job.Destination.FullName] = row;
        _rowSubscriptions[job.Id] = row.WhenAnyValue(x => x.IsFinished)
            .Subscribe(_ =>
            {
                this.RaisePropertyChanged(nameof(SummaryText));
            });
        Rows.Add(row);
    }

    private void RemoveRow(DownloadRowViewModel row)
    {
        Rows.Remove(row);
        _rowsById.Remove(row.Job.Id);
        _rowsByPath.Remove(row.Job.Destination.FullName);
        if (_rowSubscriptions.Remove(row.Job.Id, out var subscription))
        {
            subscription.Dispose();
        }
    }

    private void SeedFromFolder()
    {
        _downloadsFolder.Refresh();
        if (!_downloadsFolder.Exists)
        {
            _downloadsFolder.Create();
        }

        foreach (var file in _downloadsFolder.EnumerateFiles())
        {
            if (!IsModArchive(file))
            {
                continue;
            }
            if (_rowsByPath.ContainsKey(file.FullName))
            {
                continue;
            }
            AddRow(DiskJob(file));
        }
    }

    private IDisposable WatchFolder(IScheduler notifyScheduler)
    {
        var watcher = new FileSystemWatcher(_downloadsFolder.FullName)
        {
            NotifyFilter =
                NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime,
            IncludeSubdirectories = false,
            EnableRaisingEvents = true,
        };
        _watcher = watcher;

        var recreated = Observable
            .FromEventPattern<FileSystemEventHandler, FileSystemEventArgs>(
                handler => _watcher.Created += handler,
                handler => _watcher.Created -= handler
            )
            .Select(pattern => pattern.EventArgs.FullPath);
        var removed = Observable
            .FromEventPattern<FileSystemEventHandler, FileSystemEventArgs>(
                handler => _watcher.Deleted += handler,
                handler => _watcher.Deleted -= handler
            )
            .Select(pattern => pattern.EventArgs.FullPath);
        var renamed = Observable
            .FromEventPattern<RenamedEventHandler, RenamedEventArgs>(
                handler => watcher.Renamed += handler,
                handler => watcher.Renamed -= handler
            )
            .SelectMany(pattern =>
                new[] { pattern.EventArgs.OldFullPath, pattern.EventArgs.FullPath }
            );

        return recreated
            .Merge(removed)
            .Merge(renamed)
            .ObserveOn(notifyScheduler)
            .Subscribe(OnFolderChanged);
    }

    private void OnFolderChanged(string path)
    {
        var exists = File.Exists(path);
        if (!exists)
        {
            if (_rowsByPath.TryGetValue(path, out var row))
            {
                RemoveRow(row);
            }
            return;
        }

        var file = new FileInfo(path);
        if (!IsModArchive(file) || _rowsByPath.ContainsKey(path))
        {
            return;
        }
        AddRow(DiskJob(file));
    }

    private static bool IsModArchive(FileInfo file)
    {
        return file.Extension switch
        {
            { } extension when extension.Equals(".download", StringComparison.OrdinalIgnoreCase) =>
                false,
            { } extension when extension.Equals(".json", StringComparison.OrdinalIgnoreCase) =>
                false,
            _ => true,
        };
    }

    private static DownloadJob DiskJob(FileInfo file)
    {
        return new DownloadJob
        {
            Id = Guid.NewGuid(),
            Source = null,
            Destination = file,
            DisplayName = file.Name,
            QueuedUtc = DateTimeOffset.UtcNow,
            Status = DownloadJobStatus.Completed,
            TotalBytes = file.Length,
        };
    }
}
