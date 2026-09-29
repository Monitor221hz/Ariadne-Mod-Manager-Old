namespace Ariadne.Downloads;

public enum DownloadJobStatus
{
    Queued,
    Running,
    Completed,
    Failed,
    Cancelled,
    IntegrityMismatch,
}

public sealed record DownloadJob
{
    public required Guid Id { get; init; }
    public required Uri? Source { get; init; }
    public required FileInfo Destination { get; init; }
    public required string DisplayName { get; init; }
    public string? ExpectedChecksum { get; init; }
    public DownloadDigest Digest { get; init; }
    public required DateTimeOffset QueuedUtc { get; init; }
    public DateTimeOffset? FinishedUtc { get; init; }
    public DownloadJobStatus Status { get; init; }
    public long TotalBytes { get; init; }
    public string? Error { get; init; }
}
