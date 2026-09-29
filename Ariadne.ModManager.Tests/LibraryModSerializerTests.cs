using Ariadne.Contracts.ModManager;
using Ariadne.ModManager;
using Xunit;

namespace Ariadne.ModManager.Tests;

public class LibraryModSerializerTests : IDisposable
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

    private DirectoryInfo ModFolder(string name) => new(System.IO.Path.Combine(_temp.Path, name));

    private LibraryMod CreateMod(string folder) =>
        new(
            new ModInfo(
                1001,
                SourceType.NexusMods,
                "2.1.0",
                ["Textures", "Gameplay"],
                "Data",
                7,
                true
            ),
            ModFolder(folder),
            []
        );

    [Fact]
    public void Save_WritesMetaJsonIntoModFolder()
    {
        var mod = CreateMod("TestMod");
        new LibraryModSerializer([]).Save(mod);

        var file = new FileInfo(
            System.IO.Path.Combine(mod.Directory.FullName, LibraryModSerializer.FileName)
        );
        Assert.True(file.Exists);

        var json = File.ReadAllText(file.FullName);
        Assert.DoesNotContain("Directory", json);
        Assert.DoesNotContain("ProfileFolder", json);
        Assert.Contains("NexusMods", json);
        Assert.Contains("\"Priority\": 7", json);
    }

    [Fact]
    public void Save_CreatesMissingModFolder()
    {
        var mod = CreateMod("New/NewMod");
        new LibraryModSerializer([]).Save(mod);

        Assert.True(Directory.Exists(mod.Directory.FullName));
    }

    [Fact]
    public void Load_RoundTripsAllFields_AndDerivesDirectoryFromFileLocation()
    {
        var sut = new LibraryModSerializer([]);
        var mod = CreateMod("TestMod");
        sut.Save(mod);

        var loaded = sut.Load(ModFolder("TestMod"));

        Assert.Equal(mod.Info.ID, loaded.Info.ID);
        Assert.Equal(mod.Name, loaded.Name);
        Assert.Equal(mod.Info.ID.Source, loaded.Info.ID.Source);
        Assert.Equal(mod.Info.Version, loaded.Info.Version);
        Assert.Equal(mod.Info.Categories, loaded.Info.Categories);
        Assert.Equal(mod.Info.Target, loaded.Info.Target);
        Assert.Equal(mod.Info.Priority, loaded.Info.Priority);
        Assert.Equal(mod.Info.Active, loaded.Info.Active);
        Assert.Equal(ModFolder("TestMod").FullName, loaded.Directory.FullName);
    }

    [Fact]
    public void Load_FromMetaFilePath_DerivesParentFolder()
    {
        var sut = new LibraryModSerializer([]);
        var mod = CreateMod("Nested/TestMod");
        sut.Save(mod);

        var loaded = sut.Load(
            new FileInfo(System.IO.Path.Combine(mod.Directory.FullName, "meta.json"))
        );

        Assert.Equal(mod.Directory.FullName, loaded.Directory.FullName);
    }

    [Fact]
    public void Load_MissingMetaFile_Throws()
    {
        var sut = new LibraryModSerializer([]);
        var folder = ModFolder("Missing");
        folder.Create();

        Assert.Throws<FileNotFoundException>(() => sut.Load(folder));
    }
}
