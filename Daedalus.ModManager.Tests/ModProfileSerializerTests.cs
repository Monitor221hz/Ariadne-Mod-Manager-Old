using System.Drawing;
using Daedalus.Contracts.Mods;
using Daedalus.ModManager.Serialization;
using Daedalus.Mods;
using Daedalus.Mods.Serialization;
using Xunit;

namespace Daedalus.ModManager.Tests;

public class ModProfileSerializerTests : IDisposable
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

    private sealed class RecordingModInfoSerializer(IModInfo result) : IModInfoSerializer
    {
        public int LoadCalls { get; private set; }

        public IModInfo Load(FileInfo metaFile)
        {
            LoadCalls++;
            return result;
        }

        public IModInfo Load(DirectoryInfo modFolder)
        {
            LoadCalls++;
            return result;
        }

        public void Save(IModInfo mod) { }
    }

    private readonly TempDirectory _temp = new();
    private readonly ModManagerPaths _paths;

    public ModProfileSerializerTests()
    {
        _paths = new ModManagerPaths(new DirectoryInfo(_temp.Path));
    }

    public void Dispose() => _temp.Dispose();

    private DirectoryInfo ModsRoot => _paths.ModsFolder;
    private DirectoryInfo ProfileFolder =>
        new(System.IO.Path.Combine(_temp.Path, "profiles", "Main"));

    private ModInfo CreateMod(string folder, ulong id, string name) =>
        new(
            id,
            name,
            new DirectoryInfo(System.IO.Path.Combine(ModsRoot.FullName, folder)),
            SourceType.NexusMods,
            "1.0",
            [],
            "Data"
        );

    private ModProfile CreateProfile(ModInfo modA, ModInfo modB)
    {
        List<IModInfo> loose = [modB];
        List<IModGroup> groups =
        [
            new ModGroup("Group G", [modA], Color.FromArgb(0x80, 0x12, 0x34, 0x56)),
        ];
        return new ModProfile(
            "Main Profile",
            new ModList(loose, groups),
            new Version(1, 2, 3),
            ProfileFolder
        );
    }

    private ModInfo SaveMod(IModInfoSerializer serializer, string folder, ulong id, string name)
    {
        var mod = CreateMod(folder, id, name);
        serializer.Save(mod);
        return mod;
    }

    [Fact]
    public void Save_WritesProfileJson_ReferencingModsByFolderName()
    {
        var modSerializer = new ModInfoSerializer();
        var modA = SaveMod(modSerializer, "TestModA", 1001, "Mod A");
        var modB = SaveMod(modSerializer, "TestModB", 2002, "Mod B");
        var profile = CreateProfile(modA, modB);
        var sut = new ModProfileSerializer(modSerializer, _paths);

        sut.Save(profile);

        var file = new FileInfo(
            System.IO.Path.Combine(ProfileFolder.FullName, ModProfileSerializer.FileName)
        );
        Assert.True(file.Exists); // Save creates a missing profile folder

        var json = File.ReadAllText(file.FullName);
        Assert.Contains("TestModA", json);
        Assert.Contains("TestModB", json);
        Assert.Contains("#80123456", json); // group color as ARGB hex
        Assert.DoesNotContain("ProfileFolder", json);
    }

    [Fact]
    public void Load_RoundTripsProfile_AndDerivesFoldersFromFileLocation()
    {
        var modSerializer = new ModInfoSerializer();
        var modA = SaveMod(modSerializer, "TestModA", 1001, "Mod A");
        var modB = SaveMod(modSerializer, "TestModB", 2002, "Mod B");
        var profile = CreateProfile(modA, modB);
        var sut = new ModProfileSerializer(modSerializer, _paths);
        sut.Save(profile);

        var loaded = sut.Load(ProfileFolder);

        Assert.Equal("Main Profile", loaded.Name);
        Assert.Equal(new Version(1, 2, 3), loaded.Version);
        Assert.Equal(ProfileFolder.FullName, loaded.ProfileFolder.FullName);
        Assert.Equal(
            System.IO.Path.Combine(ProfileFolder.FullName, "Overwrite"),
            loaded.OverwriteFolder.FullName
        );

        Assert.Equal(2, loaded.ModList.Count);
        Assert.Equal("Mod B", loaded.ModList[0].Name); // loose first
        Assert.Equal("Mod A", loaded.ModList[1].Name); // grouped after

        var group = Assert.Single(loaded.ModList.ModGroups);
        Assert.Equal("Group G", group.Name);
        Assert.Equal(Color.FromArgb(0x80, 0x12, 0x34, 0x56).ToArgb(), group.HeaderColor.ToArgb());
        Assert.Equal(
            System.IO.Path.Combine(ModsRoot.FullName, "TestModA"),
            group[0].Directory.FullName
        );
    }

    [Fact]
    public void Load_ResolvesModsThroughInjectedModInfoSerializer()
    {
        var mod = CreateMod("TestModA", 1001, "Mod A");
        var recording = new RecordingModInfoSerializer(mod);
        var sut = new ModProfileSerializer(recording, _paths);
        var profileFile = new FileInfo(
            System.IO.Path.Combine(ProfileFolder.FullName, ModProfileSerializer.FileName)
        );
        ProfileFolder.Create();
        File.WriteAllText(
            profileFile.FullName,
            """
            {
              "Name": "P",
              "ModList": { "LooseMods": ["TestModA"], "ModGroups": [] },
              "Version": "1.0.0"
            }
            """
        );

        var loaded = sut.Load(ProfileFolder);

        Assert.Equal(1, recording.LoadCalls);
        Assert.Same(mod, loaded.ModList[0]);
    }
}
