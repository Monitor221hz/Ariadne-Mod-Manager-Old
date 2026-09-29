namespace Ariadne.Downloads;

public sealed record DownloadRequest(
    Uri Source,
    FileInfo Destination,
    string? ExpectedChecksum = null,
    DownloadDigest Digest = DownloadDigest.Md5,
    Guid? Id = null
);

public enum DownloadDigest
{
    Md5,
    Sha256,
}
