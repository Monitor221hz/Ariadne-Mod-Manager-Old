namespace Ariadne.Downloads;

public interface IDownloadQueue
{
    IReadOnlyList<DownloadJob> Jobs { get; }

    event EventHandler<DownloadJob>? JobQueued;
    event EventHandler<DownloadJob>? JobStarted;
    event EventHandler<DownloadProgress>? JobProgress;
    event EventHandler<DownloadCompletion>? JobCompleted;

    Guid Enqueue(DownloadRequest request);

    bool Cancel(Guid id);
}
