namespace Ariadne.Downloads;

public enum DownloadState
{
    Completed,
    Cancelled,
    IntegrityMismatch,
    Failed,
}

public sealed record DownloadResult(
    Guid Id,
    DownloadState State,
    FileInfo? File,
    string? Error,
    long ReceivedBytes,
    long TotalBytes
);
