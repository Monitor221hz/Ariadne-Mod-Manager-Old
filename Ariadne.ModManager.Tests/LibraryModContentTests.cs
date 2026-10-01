using Ariadne.Contracts.ModManager;
using Ariadne.ModManager;
using Xunit;

namespace Ariadne.ModManager.Tests;

public class LibraryModContentTests : IDisposable
{
    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } =
            System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "AriadneTests-" + Guid.NewGuid().ToString("N")
            );

        public TempDirectory() => Directory.CreateDirectory(Path);

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, true);
            }
        }
    }

    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    private LibraryMod CreateMod(string folder, params IArchiveReader[] readers) =>
        new(
            new ModInfo(1, SourceType.Local, "1.0", [], ""),
            new DirectoryInfo(System.IO.Path.Combine(_temp.Path, folder)),
            readers
        );

    [Fact]
    public void Content_IsLazilyBuiltOnce_ThenCached()
    {
        using var cts = new CancellationTokenSource();
        var mod = CreateMod("mod");
        var dir = mod.Directory;
        dir.Create();
        File.WriteAllText(System.IO.Path.Combine(dir.FullName, "a.txt"), "x");

        var first = mod.Content;
        var second = mod.Content;

        Assert.Same(first, second);
        Assert.Single(first.Children);
    }

    [Fact]
    public void Content_ReflectsDirectoryStructure()
    {
        var mod = CreateMod("mod");
        var dir = mod.Directory;
        Directory.CreateDirectory(System.IO.Path.Combine(dir.FullName, "textures/sub"));
        File.WriteAllText(System.IO.Path.Combine(dir.FullName, "textures/sub/a.dds"), "x");
        File.WriteAllText(System.IO.Path.Combine(dir.FullName, "readme.txt"), "hi");

        var content = mod.Content;

        Assert.Equal(2, content.Children.Count);
        var textures = content.Children.First(node => node.IsDirectory);
        Assert.Equal("textures", textures.Name);
        var sub = Assert.Single(textures.Children);
        Assert.Equal("sub", sub.Name);
        Assert.Equal("a.dds", Assert.Single(sub.Children).Name);
    }

    [Fact]
    public void RefreshContent_PicksUpNewFiles()
    {
        var mod = CreateMod("mod");
        var dir = mod.Directory;
        dir.Create();
        File.WriteAllText(System.IO.Path.Combine(dir.FullName, "a.txt"), "x");
        _ = mod.Content;

        File.WriteAllText(System.IO.Path.Combine(dir.FullName, "b.txt"), "y");
        mod.RefreshContent();

        Assert.Equal(2, mod.Content.Children.Count);
    }

    [Fact]
    public void Archive_GetExpandableContent_FromZip()
    {
        var mod = CreateMod("mod", new StandardArchiveReader());
        var dir = mod.Directory;
        dir.Create();
        var zipPath = System.IO.Path.Combine(dir.FullName, "pack.zip");
        using (
            var zip = new System.IO.Compression.ZipArchive(
                File.Create(zipPath),
                System.IO.Compression.ZipArchiveMode.Create
            )
        )
        {
            var entry = zip.CreateEntry("folder/hello.txt");
            using var stream = entry.Open();
            var bytes = System.Text.Encoding.UTF8.GetBytes("hello");
            stream.Write(bytes, 0, bytes.Length);
        }

        var content = mod.Content;
        var zipNode = content.Children.First(node => !node.IsDirectory);

        Assert.Equal("pack.zip", zipNode.Name);
        Assert.Equal(ModEntryKind.Archive, zipNode.Data!.Kind);
        var folderNode = Assert.Single(zipNode.Children);
        Assert.Equal("folder", folderNode.Name);
        Assert.Equal("hello.txt", Assert.Single(folderNode.Children).Name);
    }

    [Fact]
    public void Archive_NoMatchingReader_StaysFileKind()
    {
        var mod = CreateMod("mod");
        var dir = mod.Directory;
        dir.Create();
        File.WriteAllText(System.IO.Path.Combine(dir.FullName, "pack.zip"), "not a real zip");

        var zipNode = Assert.Single(mod.Content.Children);

        Assert.Equal(ModEntryKind.File, zipNode.Data!.Kind);
    }

    [Fact]
    public void Archive_WithNoReader_StaysPlainFile()
    {
        var mod = CreateMod("mod");
        var dir = mod.Directory;
        dir.Create();
        File.WriteAllText(System.IO.Path.Combine(dir.FullName, "pack.zip"), "not really a zip");

        var zipNode = Assert.Single(mod.Content.Children);

        Assert.Empty(zipNode.Children);
    }

    [Fact]
    public void GzippedTar_WithReader_StaysPlainFile()
    {
        var mod = CreateMod("mod", new StandardArchiveReader());
        var dir = mod.Directory;
        Directory.CreateDirectory(System.IO.Path.Combine(dir.FullName, "src"));
        File.WriteAllText(
            System.IO.Path.Combine(dir.FullName, "src", "skse64-2.3.1.tar.gz"),
            "not really gzipped"
        );

        var src = Assert.Single(mod.Content.Children);
        var file = Assert.Single(src.Children);

        Assert.Equal("skse64-2.3.1.tar.gz", file.Name);
        Assert.Equal(ModEntryKind.File, file.Data!.Kind);
        Assert.Empty(file.Children);
    }
}
