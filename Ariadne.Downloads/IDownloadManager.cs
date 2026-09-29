namespace Ariadne.Downloads;

public interface IDownloadManager
{
    event EventHandler<DownloadProgress>? ProgressChanged;

    Task<DownloadResult> DownloadAsync(
        DownloadRequest request,
        CancellationToken cancellationToken = default
    );
}
