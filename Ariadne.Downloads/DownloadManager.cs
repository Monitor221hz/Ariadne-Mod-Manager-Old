using System.ComponentModel;
using System.Security.Cryptography;
using Downloader;

namespace Ariadne.Downloads;

public sealed class DownloadManager : IDownloadManager
{
    public event EventHandler<DownloadProgress>? ProgressChanged;

    public async Task<DownloadResult> DownloadAsync(
        DownloadRequest request,
        CancellationToken cancellationToken = default
    )
    {
        var id = request.Id ?? Guid.NewGuid();
        var totalBytes = 0L;
        var receivedBytes = 0L;

        var configuration = CreateConfiguration();
        using var service = new DownloadService(configuration);

        service.DownloadStarted += (_, started) => totalBytes = started.TotalBytesToReceive;
        service.DownloadProgressChanged += (_, progress) =>
        {
            receivedBytes =
                progress.TotalBytesToReceive > 0
                    ? progress.ReceivedBytesSize
                    : receivedBytes + progress.ProgressedByteSize;
            ProgressChanged?.Invoke(
                this,
                new DownloadProgress(
                    id,
                    receivedBytes,
                    progress.TotalBytesToReceive,
                    progress.ProgressPercentage,
                    progress.BytesPerSecondSpeed
                )
            );
        };

        var completed = new TaskCompletionSource<AsyncCompletedEventArgs>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        service.DownloadFileCompleted += (_, finished) => completed.TrySetResult(finished);

        try
        {
            request.Destination.Directory?.Create();
            request.Destination.Refresh();
            if (request.Destination.Exists)
            {
                request.Destination.Delete();
            }

            using var registration = cancellationToken.Register(() =>
            {
                _ = service.CancelTaskAsync();
            });

            await service.DownloadFileTaskAsync(
                request.Source.AbsoluteUri,
                request.Destination.FullName,
                cancellationToken
            );
            var finished = await completed.Task.WaitAsync(cancellationToken);

            request.Destination.Refresh();
            if (finished.Cancelled)
            {
                return new DownloadResult(
                    id,
                    DownloadState.Cancelled,
                    null,
                    null,
                    receivedBytes,
                    totalBytes
                );
            }
            if (finished.Error is not null)
            {
                return new DownloadResult(
                    id,
                    DownloadState.Failed,
                    null,
                    finished.Error.Message,
                    receivedBytes,
                    totalBytes
                );
            }

            if (request.ExpectedChecksum is not null)
            {
                var digest = await ComputeDigestAsync(
                    request.Destination,
                    request.Digest,
                    cancellationToken
                );
                if (
                    !string.Equals(
                        digest,
                        request.ExpectedChecksum,
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                {
                    TryDelete(request.Destination);
                    return new DownloadResult(
                        id,
                        DownloadState.IntegrityMismatch,
                        null,
                        $"Expected {request.Digest} checksum {request.ExpectedChecksum}, got {digest}",
                        receivedBytes,
                        totalBytes
                    );
                }
            }

            return new DownloadResult(
                id,
                DownloadState.Completed,
                request.Destination,
                null,
                receivedBytes,
                totalBytes
            );
        }
        catch (OperationCanceledException)
        {
            return new DownloadResult(
                id,
                DownloadState.Cancelled,
                null,
                null,
                receivedBytes,
                totalBytes
            );
        }
        catch (Exception exception)
            when (exception is IOException or InvalidOperationException or HttpRequestException)
        {
            return new DownloadResult(
                id,
                DownloadState.Failed,
                null,
                exception.Message,
                receivedBytes,
                totalBytes
            );
        }
    }

    private static DownloadConfiguration CreateConfiguration()
    {
        return new DownloadConfiguration
        {
            ChunkCount = 8,
            ParallelDownload = true,
            ParallelCount = 4,
            MaxTryAgainOnFailure = 3,
            ClearPackageOnCompletionWithFailure = true,
            MaximumMemoryBufferBytes = 64 * 1024 * 1024,
            RequestConfiguration = new RequestConfiguration(),
        };
    }

    private static async Task<string> ComputeDigestAsync(
        FileInfo file,
        DownloadDigest digest,
        CancellationToken cancellationToken
    )
    {
        await using var stream = new FileStream(
            file.FullName,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read
        );
        HashAlgorithm hashAlgorithm =
            digest == DownloadDigest.Sha256 ? SHA256.Create() : MD5.Create();
        using (hashAlgorithm)
        {
            var hash = await hashAlgorithm.ComputeHashAsync(stream, cancellationToken);
            return Convert.ToHexString(hash);
        }
    }

    private static void TryDelete(FileInfo file)
    {
        try
        {
            file.Refresh();
            if (file.Exists)
            {
                file.Delete();
            }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
