using System.Collections.Concurrent;

namespace Daedalus.Downloads;

public sealed class DownloadQueue : IDownloadQueue, IDisposable
{
    private const int DefaultMaxParallelJobs = 3;

    private readonly IDownloadManager _manager;
    private readonly int _maxParallelJobs;
    private readonly ConcurrentQueue<DownloadJob> _pending = new();
    private readonly Dictionary<Guid, DownloadJob> _jobsById = new();
    private readonly Dictionary<Guid, CancellationTokenSource> _runningCancellations = new();
    private readonly object _sync = new();
    private readonly object _eventGate = new();

    private int _runningCount;
    private bool _disposed;

    public event EventHandler<DownloadJob>? JobQueued;
    public event EventHandler<DownloadJob>? JobStarted;
    public event EventHandler<DownloadProgress>? JobProgress;
    public event EventHandler<DownloadCompletion>? JobCompleted;

    public DownloadQueue(IDownloadManager manager, int maxParallelJobs = DefaultMaxParallelJobs)
    {
        if (maxParallelJobs < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxParallelJobs));
        }
        _manager = manager;
        _maxParallelJobs = maxParallelJobs;
        _manager.ProgressChanged += OnManagerProgress;
    }

    public IReadOnlyList<DownloadJob> Jobs
    {
        get
        {
            lock (_sync)
            {
                return _jobsById.Values.ToList();
            }
        }
    }

    public Guid Enqueue(DownloadRequest request)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var job = new DownloadJob
        {
            Id = request.Id ?? Guid.NewGuid(),
            Source = request.Source,
            Destination = request.Destination,
            DisplayName = Path.GetFileName(request.Destination.FullName),
            ExpectedChecksum = request.ExpectedChecksum,
            Digest = request.Digest,
            QueuedUtc = DateTimeOffset.UtcNow,
            Status = DownloadJobStatus.Queued,
        };

        lock (_sync)
        {
            _jobsById[job.Id] = job;
        }
        _pending.Enqueue(job);
        lock (_eventGate)
        {
            JobQueued?.Invoke(this, job);
        }
        PumpPending();
        return job.Id;
    }

    public bool Cancel(Guid id)
    {
        lock (_sync)
        {
            if (_runningCancellations.TryGetValue(id, out var cancellation))
            {
                cancellation.Cancel();
                return true;
            }
        }

        var pending = _pending.ToArray();
        var queued = pending.FirstOrDefault(job => job.Id == id);
        if (queued is null)
        {
            return false;
        }

        var rebuilt = new ConcurrentQueue<DownloadJob>(pending.Where(job => job.Id != id));
        while (_pending.TryDequeue(out _)) { }
        foreach (var job in rebuilt)
        {
            _pending.Enqueue(job);
        }

        var cancelled = CompleteAsCancelled(queued);
        lock (_eventGate)
        {
            JobCompleted?.Invoke(
                this,
                new DownloadCompletion(
                    cancelled,
                    new DownloadResult(cancelled.Id, DownloadState.Cancelled, null, null, 0, 0)
                )
            );
        }
        return true;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        lock (_sync)
        {
            foreach (var cancellation in _runningCancellations.Values)
            {
                cancellation.Cancel();
            }
        }
        _manager.ProgressChanged -= OnManagerProgress;
    }

    private void OnManagerProgress(object? sender, DownloadProgress progress)
    {
        lock (_eventGate)
        {
            JobProgress?.Invoke(this, progress);
        }
    }

    private void PumpPending()
    {
        while (!_disposed)
        {
            Task running;
            lock (_sync)
            {
                if (_runningCount >= _maxParallelJobs || !_pending.TryDequeue(out var job))
                {
                    return;
                }
                _runningCount++;
                running = ExecuteAsync(job);
            }
            _ = running;
        }
    }

    private async Task ExecuteAsync(DownloadJob job)
    {
        var cancellation = new CancellationTokenSource();
        lock (_sync)
        {
            _runningCancellations[job.Id] = cancellation;
            job = job with { Status = DownloadJobStatus.Running };
            _jobsById[job.Id] = job;
        }
        lock (_eventGate)
        {
            JobStarted?.Invoke(this, job);
        }

        DownloadResult result;
        try
        {
            if (job.Source is null)
            {
                result = new DownloadResult(
                    job.Id,
                    DownloadState.Failed,
                    null,
                    "Download job has no source URI.",
                    0,
                    0
                );
            }
            else
            {
                var request = new DownloadRequest(
                    job.Source,
                    job.Destination,
                    job.ExpectedChecksum,
                    job.Digest,
                    job.Id
                );
                result = await _manager.DownloadAsync(request, cancellation.Token);
            }
        }
        catch (Exception exception)
        {
            result = new DownloadResult(
                job.Id,
                DownloadState.Failed,
                null,
                exception.Message,
                0,
                0
            );
        }
        finally
        {
            lock (_sync)
            {
                _runningCancellations.Remove(job.Id);
            }
            cancellation.Dispose();
        }

        var finished = job with
        {
            Status = ToJobStatus(result.State),
            FinishedUtc = DateTimeOffset.UtcNow,
            TotalBytes = result.TotalBytes,
            Error = result.Error,
        };
        lock (_sync)
        {
            _jobsById[finished.Id] = finished;
        }

        try
        {
            lock (_eventGate)
            {
                JobCompleted?.Invoke(this, new DownloadCompletion(finished, result));
            }
        }
        finally
        {
            lock (_sync)
            {
                _runningCount--;
            }
            PumpPending();
        }
    }

    private DownloadJob CompleteAsCancelled(DownloadJob queued)
    {
        var cancelled = queued with
        {
            Status = DownloadJobStatus.Cancelled,
            FinishedUtc = DateTimeOffset.UtcNow,
        };
        lock (_sync)
        {
            _jobsById[cancelled.Id] = cancelled;
        }
        return cancelled;
    }

    private static DownloadJobStatus ToJobStatus(DownloadState state)
    {
        return state switch
        {
            DownloadState.Completed => DownloadJobStatus.Completed,
            DownloadState.Cancelled => DownloadJobStatus.Cancelled,
            DownloadState.IntegrityMismatch => DownloadJobStatus.IntegrityMismatch,
            _ => DownloadJobStatus.Failed,
        };
    }
}
