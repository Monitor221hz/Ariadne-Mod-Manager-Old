namespace Daedalus.Downloads;

public sealed record DownloadProgress(
    Guid Id,
    long ReceivedBytes,
    long TotalBytes,
    double Percentage,
    double BytesPerSecond
);
