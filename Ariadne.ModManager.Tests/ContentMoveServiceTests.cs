using Ariadne.Contracts.ModManager;
using Xunit;

namespace Ariadne.ModManager.Tests;

public class ContentMoveServiceTests
{
    private readonly ContentMoveService _service = new(new LibraryModSerializer([]));

    [Fact]
    public void CanMoveInto_RejectsExistingDestination()
    {
        using var temp = new TempDirectory();
        var source = Path.Combine(temp.Path, "a.txt");
        var targetDir = Directory.CreateDirectory(Path.Combine(temp.Path, "target")).FullName;
        File.WriteAllText(source, "x");
        File.WriteAllText(Path.Combine(targetDir, "a.txt"), "y");

        Assert.False(_service.CanMoveInto(source, false, "a.txt", targetDir));
    }

    [Fact]
    public void CanMoveInto_RejectsSameParentDirectory()
    {
        using var temp = new TempDirectory();
        var source = Path.Combine(temp.Path, "a.txt");
        File.WriteAllText(source, "x");

        Assert.False(_service.CanMoveInto(source, false, "a.txt", temp.Path));
    }

    [Fact]
    public void CanMoveInto_RejectsDirectoryIntoItselfOrDescendant()
    {
        using var temp = new TempDirectory();
        var source = Directory.CreateDirectory(Path.Combine(temp.Path, "mod")).FullName;
        var child = Directory.CreateDirectory(Path.Combine(source, "child")).FullName;

        Assert.False(_service.CanMoveInto(source, true, "mod", source));
        Assert.False(_service.CanMoveInto(source, true, "mod", child));
        Assert.True(_service.CanMoveInto(child, true, "child", temp.Path + "x"));
    }

    [Fact]
    public void MoveInto_MovesFile()
    {
        using var temp = new TempDirectory();
        var source = Path.Combine(temp.Path, "a.txt");
        var targetDir = Directory.CreateDirectory(Path.Combine(temp.Path, "target")).FullName;
        File.WriteAllText(source, "x");

        _service.MoveInto(source, false, "a.txt", targetDir);

        Assert.False(File.Exists(source));
        Assert.True(File.Exists(Path.Combine(targetDir, "a.txt")));
    }

    [Fact]
    public void FlattenIntoModRoot_MergesChildrenAndRemovesEmptySource()
    {
        using var temp = new TempDirectory();
        var root = Directory.CreateDirectory(Path.Combine(temp.Path, "root")).FullName;
        var source = Directory.CreateDirectory(Path.Combine(temp.Path, "source")).FullName;
        File.WriteAllText(Path.Combine(source, "a.txt"), "x");
        Directory.CreateDirectory(Path.Combine(source, "sub"));
        File.WriteAllText(Path.Combine(source, "sub", "b.txt"), "y");

        _service.FlattenIntoModRoot(source, root);

        Assert.True(File.Exists(Path.Combine(root, "a.txt")));
        Assert.True(File.Exists(Path.Combine(root, "sub", "b.txt")));
        Assert.False(Directory.Exists(source));
    }

    [Fact]
    public void CanFlattenIntoModRoot_RejectsCollisions()
    {
        using var temp = new TempDirectory();
        var root = Directory.CreateDirectory(Path.Combine(temp.Path, "root")).FullName;
        var source = Directory.CreateDirectory(Path.Combine(temp.Path, "source")).FullName;
        File.WriteAllText(Path.Combine(root, "a.txt"), "existing");
        File.WriteAllText(Path.Combine(source, "a.txt"), "new");

        Assert.False(_service.CanFlattenIntoModRoot(source, root));
        Assert.False(_service.CanFlattenIntoModRoot(Path.Combine(temp.Path, "missing"), root));
    }

    private static LibraryMod ModAt(
        TempDirectory temp,
        string name,
        SourceType source,
        bool withContent
    )
    {
        var dir = Directory.CreateDirectory(Path.Combine(temp.Path, name));
        if (withContent)
        {
            File.WriteAllText(Path.Combine(dir.FullName, "content.txt"), "x");
        }
        return new LibraryMod(
            new ModInfo(1, source, "1.0", [], ""),
            new DirectoryInfo(dir.FullName),
            []
        );
    }

    [Fact]
    public void ShouldInheritInfo_OnlyForBlankLocalMods()
    {
        using var temp = new TempDirectory();

        Assert.True(_service.ShouldInheritInfo(ModAt(temp, "blank", SourceType.Local, false)));
        Assert.False(_service.ShouldInheritInfo(ModAt(temp, "full", SourceType.Local, true)));
        Assert.False(_service.ShouldInheritInfo(ModAt(temp, "nexus", SourceType.NexusMods, false)));
    }

    [Fact]
    public void InheritInfo_CopiesIdentity_AndPersists()
    {
        using var temp = new TempDirectory();
        var source = ModAt(temp, "source", SourceType.NexusMods, true);
        source.ReplaceInfo(
            new ModInfo(new ModID(12604, SourceType.NexusMods), "6.1", ["UI"], "Data")
        );
        var destination = ModAt(temp, "destination", SourceType.Local, false);

        _service.InheritInfo(source, destination);

        Assert.Equal(new ModID(12604, SourceType.NexusMods), destination.Info.ID);
        Assert.Equal("6.1", destination.Info.Version);
        Assert.Equal(["UI"], destination.Info.Categories);
        Assert.Equal("Data", destination.Info.Target);
        Assert.True(
            File.Exists(Path.Combine(destination.Directory.FullName, LibraryModSerializer.FileName))
        );
    }
}
