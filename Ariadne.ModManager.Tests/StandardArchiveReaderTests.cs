using System.Formats.Tar;
using Ariadne.Contracts.ModManager;
using Xunit;

namespace Ariadne.ModManager.Tests;

public class StandardArchiveReaderTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    private FileInfo CreateTar(params (string Name, int Size)[] entries)
    {
        var file = new FileInfo(_temp.Combine("archive.tar"));
        using var fileStream = new FileStream(file.FullName, FileMode.Create, FileAccess.ReadWrite);
        using var tar = new TarWriter(fileStream);
        foreach (var (name, size) in entries)
        {
            tar.WriteEntry(
                new PaxTarEntry(TarEntryType.RegularFile, name)
                {
                    DataStream = new MemoryStream(new byte[size]),
                }
            );
        }
        return file;
    }

    [Fact]
    public void Read_PlainTarArchive_ListsEntries()
    {
        var archive = CreateTar(("plugin.txt", 7));
        var modInfo = new ModInfo(1, SourceType.Local, "1.0", [], "");

        var tree = new StandardArchiveReader().Read(modInfo, archive);

        var file = Assert.Single(tree.Children);
        Assert.Equal("plugin.txt", file.Name);
        Assert.Equal(7, file.Data.Size);
    }

    [Fact]
    public void SupportedExtensions_ExcludeCompressedStreams()
    {
        var extensions = new StandardArchiveReader().SupportedExtensions;

        Assert.DoesNotContain(".gz", extensions);
        Assert.DoesNotContain(".bz2", extensions);
    }
}
