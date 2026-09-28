using System.IO.Compression;
using Xunit;

namespace Daedalus.ModManager.Tests;

public class StandardArchiveExtractorTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    private FileInfo CreateZip(params (string Name, int Size)[] entries)
    {
        var path = new FileInfo(Path.Combine(_temp.Path, "archive.zip"));
        using var fileStream = new FileStream(path.FullName, FileMode.Create, FileAccess.ReadWrite);
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
    public void Extract_MultiEntryArchive_ExtractsAllFiles()
    {
        var archive = CreateZip(("a.bin", 64 * 1024), ("b.bin", 128 * 1024), ("c.bin", 32 * 1024));

        var extractor = new StandardArchiveExtractor();
        var output = new DirectoryInfo(Path.Combine(_temp.Path, "out"));
        output.Create();
        extractor.Extract(output, archive);

        Assert.Equal(64 * 1024, new FileInfo(Path.Combine(output.FullName, "a.bin")).Length);
        Assert.Equal(128 * 1024, new FileInfo(Path.Combine(output.FullName, "b.bin")).Length);
        Assert.Equal(32 * 1024, new FileInfo(Path.Combine(output.FullName, "c.bin")).Length);
    }
}
