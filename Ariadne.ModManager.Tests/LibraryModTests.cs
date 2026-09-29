using Ariadne.Contracts.ModManager;
using Ariadne.ModManager;
using Xunit;

namespace Ariadne.ModManager.Tests;

public sealed class LibraryModTests : IDisposable
{
    private readonly DirectoryInfo _temp = new(
        Path.Combine(Path.GetTempPath(), "Ariadne-libmod-" + Guid.NewGuid().ToString("N"))
    );

    public void Dispose()
    {
        if (_temp.Exists)
        {
            _temp.Delete(recursive: true);
        }
    }

    [Fact]
    public void Rename_RebuildsContent_FromNewFolder()
    {
        _temp.Create();
        File.WriteAllText(Path.Combine(_temp.FullName, "readme.txt"), "hi");
        var mod = new LibraryMod(
            new ModInfo(0, SourceType.Local, "1.0.0", [], "", 1, true),
            _temp,
            []
        );
        var first = mod.Content;

        mod.RenameTo("renamed-mod");
        var second = mod.Content;

        Assert.Equal("renamed-mod", mod.Name);
        Assert.NotSame(first, second);
        Assert.Single(second.Children);
    }

    [Fact]
    public void Content_Excludes_Metadata_File()
    {
        _temp.Create();
        File.WriteAllText(Path.Combine(_temp.FullName, LibraryModSerializer.FileName), "{}");
        File.WriteAllText(Path.Combine(_temp.FullName, "readme.txt"), "hi");
        Directory.CreateDirectory(Path.Combine(_temp.FullName, "textures"));

        var mod = new LibraryMod(
            new ModInfo(0, SourceType.Local, "1.0.0", [], "", 1, true),
            _temp,
            []
        );

        var names = mod.Content.Children.Select(c => c.Name).ToList();
        Assert.Contains("readme.txt", names, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("textures", names, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            LibraryModSerializer.FileName,
            names,
            StringComparer.OrdinalIgnoreCase
        );
    }
}
