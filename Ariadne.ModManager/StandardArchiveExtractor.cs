using Ariadne.Contracts.ModManager;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Readers;
using ReaderOptions = SharpCompress.Readers.ReaderOptions;

namespace Ariadne.ModManager;

public sealed class StandardArchiveExtractor : IArchiveExtractor
{
    private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".zip",
        ".7z",
        ".rar",
        ".tar",
        ".gz",
        ".bz2",
    };

    public event EventHandler<ExtractionProgressEventArgs>? OnExtractionProgress;
    public IReadOnlyCollection<string> SupportedExtensions => Extensions;

    public void Extract(DirectoryInfo outputDirectory, FileInfo archiveFile)
    {
        var progress = new Progress<ProgressReport>(report =>
        {
            OnExtractionProgress?.Invoke(
                archiveFile,
                new ExtractionProgressEventArgs(
                    report.EntryPath,
                    report.BytesTransferred,
                    report.TotalBytes,
                    report.PercentComplete
                )
            );
        });
        using var archive = ArchiveFactory.OpenArchive(
            archiveFile.FullName,
            ReaderOptions.ForFilePath.WithProgress(progress)
        );
        archive.WriteToDirectory(
            outputDirectory.FullName,
            new ExtractionOptions
            {
                ExtractFullPath = true,
                Overwrite = true,
                CheckCrc = true,
            }
        );
    }
}
