using System.IO.Compression;
using Daedalus.Contracts.ModManager;
using Xunit;

namespace Daedalus.ModManager.Tests;

public class StandardArchiveExtractorTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    private FileInfo CreateZip(params (string Name, int Size)[] entries)
    {
        var path = new FileInfo(Path.Combine(_temp.Path, "archive.zip"));
        using var fileStream = new FileStream(
            path.FullName,
            FileMode.Create,
            FileAccess.ReadWrite
        );
        using var zip = new ZipArchive(fileStream, ZipArchiveMode.Create);
        foreach (var (name, size) in entries)
        {
            var entry = zip.CreateEntry(name);
            using var entryStream = entry.Open();
            entryStream.Write(new byte[size], 0, size);
        }
        return path;
    }

    [Fact]
    public void Extract_MultiEntryArchive_ReportsCumulativeMonotonicProgress()
    {
        var archive = CreateZip(
            ("a.bin", 64 * 1024),
            ("b.bin", 128 * 1024),
            ("c.bin", 32 * 1024)
        );
        var totalBytes = 64 * 1024 + 128 * 1024 + 32 * 1024;

        var extractor = new StandardArchiveExtractor();
        var events = new List<ExtractionProgressEventArgs>();
        extractor.OnExtractionProgress += (_, args) => events.Add(args);

        var output = new DirectoryInfo(Path.Combine(_temp.Path, "out"));
        output.Create();
        extractor.Extract(output, archive);

        Assert.NotEmpty(events);

        var percentages = events
            .Where(e => e.ProgressPercentage.HasValue)
            .Select(e => e.ProgressPercentage!.Value)
            .ToList();
        Assert.NotEmpty(percentages);
        for (var i = 1; i < percentages.Count; i++)
        {
            Assert.True(
                percentages[i] >= percentages[i - 1],
                $"percentage went backwards at index {i}: {percentages[i - 1]} -> {percentages[i]}"
            );
        }
        Assert.Equal(100.0, percentages[^1]);

        var bytesSeries = events.Select(e => e.BytesTransferred).ToList();
        for (var i = 1; i < bytesSeries.Count; i++)
        {
            Assert.True(bytesSeries[i] >= bytesSeries[i - 1]);
        }
        Assert.Equal(totalBytes, events[^1].TotalBytes);
        Assert.Equal(totalBytes, bytesSeries[^1]);

        Assert.True(File.Exists(Path.Combine(output.FullName, "a.bin")));
        Assert.True(File.Exists(Path.Combine(output.FullName, "b.bin")));
        Assert.True(File.Exists(Path.Combine(output.FullName, "c.bin")));
    }
}
