namespace Ariadne.Contracts.ModManager;

public sealed class ExtractionProgressEventArgs(
    string entryPath,
    long bytesTransferred,
    long? totalBytes,
    double? progressPercentage
) : EventArgs
{
    public string EntryPath { get; } = entryPath;
    public long BytesTransferred { get; } = bytesTransferred;
    public long? TotalBytes { get; } = totalBytes;
    public double? ProgressPercentage { get; } = progressPercentage;
}

public interface IArchiveExtractor
{
    event EventHandler<ExtractionProgressEventArgs>? OnExtractionProgress;
    IReadOnlyCollection<string> SupportedExtensions { get; }
    void Extract(DirectoryInfo outputDirectory, FileInfo archiveFile);
}
