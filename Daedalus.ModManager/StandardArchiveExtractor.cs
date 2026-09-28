using Daedalus.Contracts.ModManager;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Readers;

namespace Daedalus.ModManager;

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
        using var archive = ArchiveFactory.OpenArchive(archiveFile.FullName);
        var totalBytes = archive.TotalUncompressedSize;
        var transferred = 0L;

        void Report(string? entryPath)
        {
            double? percentage = totalBytes > 0 ? transferred * 100.0 / totalBytes : null;
            OnExtractionProgress?.Invoke(
                archiveFile,
                new ExtractionProgressEventArgs(
                    entryPath ?? string.Empty,
                    transferred,
                    totalBytes,
                    percentage
                )
            );
        }

        if (archive.IsSolid)
        {
            using var reader = archive.ExtractAllEntries();
            while (reader.MoveToNextEntry())
            {
                if (reader.Entry.IsDirectory)
                {
                    continue;
                }
                reader.WriteEntryToDirectory(
                    outputDirectory.FullName,
                    new ExtractionOptions { ExtractFullPath = true, Overwrite = true }
                );
                transferred += reader.Entry.Size;
                Report(reader.Entry.Key);
            }
            return;
        }

        foreach (var entry in archive.Entries.Where(entry => !entry.IsDirectory))
        {
            entry.WriteToDirectory(
                outputDirectory.FullName,
                new ExtractionOptions { ExtractFullPath = true, Overwrite = true }
            );
            transferred += entry.Size;
            Report(entry.Key);
        }
    }
}
