using System.Collections.Concurrent;
using Ariadne.Downloads;
using Xunit;

namespace Ariadne.Downloads.Tests;

public class DownloadQueueTests : IDisposable
{
    private readonly DirectoryInfo _directory = new(
        Path.Combine(Path.GetTempPath(), $"Ariadne-queue-tests-{Guid.NewGuid():N}")
    );

    public void Dispose()
    {
        _directory.Refresh();
        if (_directory.Exists)
        {
            _directory.Delete(recursive: true);
        }
    }

    private DownloadRequest Request(string name)
    {
        return new DownloadRequest(
            new Uri($"https://example.com/{name}"),
            new FileInfo(Path.Combine(_directory.FullName, name))
        );
    }

    private sealed class ScriptedManager : IDownloadManager
    {
        private readonly bool _blocking;
        private readonly TaskCompletionSource _release = new(
            TaskCreationOptions.RunContinuationsAsynchronously
        );

        public ConcurrentQueue<DownloadRequest> StartedRequests { get; } = new();
        public int MaxConcurrent { get; private set; }

        public event EventHandler<DownloadProgress>? ProgressChanged;

        private int _running;

        public ScriptedManager(bool blocking)
        {
            _blocking = blocking;
        }

        public void ReleaseAll()
        {
            _release.TrySetResult();
        }

        public void EmitProgress(DownloadProgress progress)
        {
            ProgressChanged?.Invoke(this, progress);
        }

        public async Task<DownloadResult> DownloadAsync(
            DownloadRequest request,
            CancellationToken cancellationToken
        )
        {
            StartedRequests.Enqueue(request);
            var running = Interlocked.Increment(ref _running);
            lock (this)
            {
                MaxConcurrent = Math.Max(MaxConcurrent, running);
            }

            try
            {
                if (_blocking)
                {
                    await _release.Task.WaitAsync(cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                return new DownloadResult(
                    request.Id ?? Guid.Empty,
                    DownloadState.Cancelled,
                    null,
                    null,
                    0,
                    0
                );
            }
            finally
            {
                Interlocked.Decrement(ref _running);
            }

            return new DownloadResult(
                request.Id ?? Guid.Empty,
                DownloadState.Completed,
                request.Destination,
                null,
                0,
                0
            );
        }
    }

    private static bool WaitUntil(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
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
    public async Task Enqueue_BelowLimit_StartsAndCompletesImmediately()
    {
        var manager = new ScriptedManager(blocking: false);
        using var queue = new DownloadQueue(manager);
        var completions = new List<DownloadCompletion>();
        var started = new List<DownloadJob>();
        queue.JobStarted += (_, job) => started.Add(job);
        queue.JobCompleted += (_, completion) => completions.Add(completion);

        var id = queue.Enqueue(Request("first.7z"));

        Assert.True(WaitUntil(() => completions.Count == 1));
        var completion = completions[0];
        Assert.Equal(DownloadState.Completed, completion.Result.State);
        Assert.Equal(DownloadJobStatus.Completed, completion.Job.Status);
        Assert.Equal(id, completion.Job.Id);
        Assert.Equal(id, queue.Jobs.Single(job => job.Id == id).Id);
        Assert.Equal(id, started[0].Id);
    }

    [Fact]
    public void Enqueue_BeyondLimit_CapsConcurrencyAtThree()
    {
        var manager = new ScriptedManager(blocking: true);
        using var queue = new DownloadQueue(manager);

        var ids = new List<Guid>();
        for (var i = 0; i < 5; i++)
        {
            ids.Add(queue.Enqueue(Request($"file{i}.7z")));
        }

        Assert.True(WaitUntil(() => manager.StartedRequests.Count >= 3));
        Thread.Sleep(200);
        Assert.Equal(3, manager.StartedRequests.Count);
        Assert.Equal(3, manager.MaxConcurrent);

        manager.ReleaseAll();
    }

    [Fact]
    public async Task Enqueue_BeyondLimit_StartsPendingInFifoOrder()
    {
        var manager = new ScriptedManager(blocking: true);
        using var queue = new DownloadQueue(manager);
        var completions = new List<DownloadCompletion>();
        queue.JobCompleted += (_, completion) => completions.Add(completion);

        var names = Enumerable.Range(0, 5).Select(i => $"file{i}.7z").ToList();
        foreach (var name in names)
        {
            queue.Enqueue(Request(name));
        }

        Assert.True(WaitUntil(() => manager.StartedRequests.Count >= 3));
        manager.ReleaseAll();
        Assert.True(WaitUntil(() => completions.Count == 5));

        Assert.Equal(
            names,
            manager.StartedRequests.Select(request => request.Source.Segments[^1]).ToList()
        );
    }

    [Fact]
    public void ProgressFromManager_IsForwarded()
    {
        var manager = new ScriptedManager(blocking: false);
        using var queue = new DownloadQueue(manager);
        var received = new List<DownloadProgress>();
        queue.JobProgress += (_, progress) => received.Add(progress);

        var payload = new DownloadProgress(Guid.NewGuid(), 10, 100, 10, 1000);
        manager.EmitProgress(payload);

        Assert.Single(received);
        Assert.Equal(payload, received[0]);
    }

    [Fact]
    public async Task Cancel_QueuedJob_CompletesAsCancelledWithoutStarting()
    {
        var manager = new ScriptedManager(blocking: true);
        using var queue = new DownloadQueue(manager, maxParallelJobs: 1);
        var completions = new List<DownloadCompletion>();
        queue.JobCompleted += (_, completion) => completions.Add(completion);

        var first = queue.Enqueue(Request("first.7z"));
        var second = queue.Enqueue(Request("second.7z"));
        Assert.True(WaitUntil(() => manager.StartedRequests.Count == 1));

        Assert.True(queue.Cancel(second));
        Assert.True(WaitUntil(() => completions.Any(c => c.Job.Id == second)));

        var completion = completions.First(c => c.Job.Id == second);
        Assert.Equal(DownloadJobStatus.Cancelled, completion.Job.Status);
        Assert.DoesNotContain(manager.StartedRequests, r => r.Id == second);

        manager.ReleaseAll();
        await Task.CompletedTask;
        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Cancel_RunningJob_CancelsCancellationToken()
    {
        var manager = new ScriptedManager(blocking: true);
        using var queue = new DownloadQueue(manager);
        var completions = new List<DownloadCompletion>();
        queue.JobCompleted += (_, completion) => completions.Add(completion);

        var id = queue.Enqueue(Request("file.7z"));
        Assert.True(WaitUntil(() => manager.StartedRequests.Count == 1));

        Assert.True(queue.Cancel(id));
        Assert.True(WaitUntil(() => completions.Count == 1));
        Assert.Equal(DownloadState.Cancelled, completions[0].Result.State);
    }

    [Fact]
    public void ProgressFromManager_ConcurrentEmissions_AreSerializedToSubscribers()
    {
        var manager = new ScriptedManager(blocking: false);
        using var queue = new DownloadQueue(manager);

        var inFlight = 0;
        var maxInFlight = 0;
        queue.JobProgress += (_, _) =>
        {
            var depth = Interlocked.Increment(ref inFlight);
            int snapshot;
            for (var spins = 0; spins < 200; spins++)
            {
                Thread.SpinWait(50);
            }
            do
            {
                snapshot = maxInFlight;
                if (depth <= snapshot)
                {
                    break;
                }
            } while (Interlocked.CompareExchange(ref maxInFlight, depth, snapshot) != snapshot);
            Interlocked.Decrement(ref inFlight);
        };

        Parallel.For(
            0,
            8,
            _ =>
            {
                for (var n = 0; n < 250; n++)
                {
                    manager.EmitProgress(new DownloadProgress(Guid.NewGuid(), n, 250, n, n));
                }
            }
        );

        Assert.Equal(1, maxInFlight);
    }

    [Fact]
    public void Cancel_UnknownId_ReturnsFalse()
    {
        var manager = new ScriptedManager(blocking: false);
        using var queue = new DownloadQueue(manager);

        Assert.False(queue.Cancel(Guid.NewGuid()));
    }
}
