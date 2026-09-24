using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Daedalus.Contracts.ModManager;
using Daedalus.Downloads;
using ReactiveUI;
using ReactiveUI.Avalonia;

namespace Daedalus.ModManager.GUI.ViewModels;

public sealed class DownloadListViewModel : ViewModelBase, IWorkspaceTab, IDisposable
{
    private static readonly TimeSpan ProgressSampleInterval = TimeSpan.FromMilliseconds(200);

    private readonly IDownloadQueue _queue;
    private readonly DirectoryInfo _downloadsFolder;
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
        IScheduler? sampleScheduler = null,
        IScheduler? notifyScheduler = null
    )
    {
        _queue = queue;
        _downloadsFolder = paths.DownloadsFolder;
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

        Rows.CollectionChanged += (_, _) =>
        {
            this.RaisePropertyChanged(nameof(HasRows));
            this.RaisePropertyChanged(nameof(SummaryText));
        };
    }

    public string Title => "Downloads";

    public ObservableCollection<DownloadRowViewModel> Rows { get; } = [];

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
            _rowsByPath.Remove(job.Destination.FullName);
            _rowsById[job.Id] = seeded;
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

    private void AddRow(DownloadJob job)
    {
        var row = new DownloadRowViewModel(job, id => _queue.Cancel(id));
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
