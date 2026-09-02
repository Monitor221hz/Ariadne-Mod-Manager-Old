using Daedalus.Contracts.Mods;
using Daedalus.Mods.Serialization;
using Xunit;

namespace Daedalus.Mods.Tests;

public class ModInfoSerializerTests : IDisposable
{
    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } =
            System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "DaedalusTests-" + Guid.NewGuid().ToString("N")
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

    private ModInfo CreateMod(string folder) =>
        new(
            1001,
            "Mod A",
            ModFolder(folder),
            SourceType.NexusMods,
            "2.1.0",
            ["Textures", "Gameplay"],
            "Data"
        )
        {
            Priority = 7,
        };

    [Fact]
    public void Save_WritesMetaJsonIntoModFolder()
    {
        var mod = CreateMod("TestMod");
        new ModInfoSerializer().Save(mod);

        var file = new FileInfo(
            System.IO.Path.Combine(mod.Directory.FullName, ModInfoSerializer.FileName)
        );
        Assert.True(file.Exists);

        var json = File.ReadAllText(file.FullName);
        Assert.DoesNotContain("Directory", json);
        Assert.DoesNotContain("ProfileFolder", json);
        Assert.Contains("NexusMods", json); // enum as string
        Assert.Contains("\"Priority\": 7", json);
    }

    [Fact]
    public void Save_CreatesMissingModFolder()
    {
        var mod = CreateMod("New/NewMod");
        new ModInfoSerializer().Save(mod);

        Assert.True(Directory.Exists(mod.Directory.FullName));
    }

    [Fact]
    public void Load_RoundTripsAllFields_AndDerivesDirectoryFromFileLocation()
    {
        var sut = new ModInfoSerializer();
        var mod = CreateMod("TestMod");
        sut.Save(mod);

        var loaded = sut.Load(ModFolder("TestMod"));

        Assert.Equal(mod.ID, loaded.ID);
        Assert.Equal(mod.Name, loaded.Name);
        Assert.Equal(mod.IDSource, loaded.IDSource);
        Assert.Equal(mod.Version, loaded.Version);
        Assert.Equal(mod.Categories, loaded.Categories);
        Assert.Equal(mod.Target, loaded.Target);
        Assert.Equal(mod.Priority, loaded.Priority);
        Assert.Equal(ModFolder("TestMod").FullName, loaded.Directory.FullName);
    }

    [Fact]
    public void Load_FromMetaFilePath_DerivesParentFolder()
    {
        var sut = new ModInfoSerializer();
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
        var sut = new ModInfoSerializer();
        var folder = ModFolder("Missing");
        folder.Create();

        Assert.Throws<FileNotFoundException>(() => sut.Load(folder));
    }
}
