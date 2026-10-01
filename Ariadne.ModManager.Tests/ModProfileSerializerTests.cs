using System.Drawing;
using Ariadne.Contracts.ModManager;
using Ariadne.ModManager.Serialization;
using Xunit;

namespace Ariadne.ModManager.Tests;

public class ModProfileSerializerTests : IDisposable
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

    private sealed class RecordingLibraryModSerializer(ILibraryMod result) : ILibraryModSerializer
    {
        public int LoadCalls { get; private set; }

        public ILibraryMod Load(FileInfo metaFile)
        {
            LoadCalls++;
            return result;
        }

        public ILibraryMod Load(DirectoryInfo modFolder)
        {
            LoadCalls++;
            return result;
        }

        public void Save(ILibraryMod mod) { }
    }

    private readonly TempDirectory _temp = new();
    private readonly ModManagerPaths _paths;

    public ModProfileSerializerTests()
    {
        var service = TestAssets.CreateInstanceService(
            new FileInfo(System.IO.Path.Combine(_temp.Path, "instances.json"))
        );
        service.Create(
            "test",
            new DirectoryInfo(System.IO.Path.Combine(_temp.Path, "instance")),
            TestAssets.GameAt(new DirectoryInfo(System.IO.Path.Combine(_temp.Path, "game")))
        );
        _paths = new ModManagerPaths(new DirectoryInfo(_temp.Path), service);
    }

    public void Dispose() => _temp.Dispose();

    private DirectoryInfo ModsRoot => _paths.ModsFolder;
    private DirectoryInfo ProfileFolder =>
        new(System.IO.Path.Combine(_temp.Path, "profiles", "Main"));

    private LibraryMod CreateMod(string folder, ulong id) =>
        new(
            new ModInfo(id, SourceType.NexusMods, "1.0", [], "Data"),
            new DirectoryInfo(System.IO.Path.Combine(ModsRoot.FullName, folder)),
            []
        );

    private ModProfile CreateProfile(LibraryMod modA, LibraryMod modB)
    {
        List<IModListEntry> loose = [new ModListEntry(modB, true)];
        List<IModGroup> groups =
        [
            new ModGroup(
                "Group G",
                [new ModListEntry(modA, true)],
                Color.FromArgb(0x80, 0x12, 0x34, 0x56)
            ),
        ];
        return new ModProfile(
            "Main Profile",
            new ModList(loose, groups),
            new Version(1, 2, 3),
            ProfileFolder
        );
    }

    private LibraryMod SaveMod(ILibraryModSerializer serializer, string folder, ulong id)
    {
        var mod = CreateMod(folder, id);
        serializer.Save(mod);
        return mod;
    }

    [Fact]
    public void Save_WritesProfileJson_ReferencingModsByFolderName()
    {
        var modSerializer = new LibraryModSerializer([]);
        var modA = SaveMod(modSerializer, "TestModA", 1001);
        var modB = SaveMod(modSerializer, "TestModB", 2002);
        var profile = CreateProfile(modA, modB);
        var sut = new ModProfileSerializer(modSerializer, _paths);

        sut.Save(profile);

        var file = new FileInfo(
            System.IO.Path.Combine(ProfileFolder.FullName, ModProfileSerializer.FileName)
        );
        Assert.True(file.Exists);

        var json = File.ReadAllText(file.FullName);
        Assert.Contains("TestModA", json);
        Assert.Contains("TestModB", json);
        Assert.Contains("#80123456", json);
        Assert.DoesNotContain("ProfileFolder", json);
    }

    [Fact]
    public void Load_RoundTripsProfile_AndDerivesFoldersFromFileLocation()
    {
        var modSerializer = new LibraryModSerializer([]);
        var modA = SaveMod(modSerializer, "TestModA", 1001);
        var modB = SaveMod(modSerializer, "TestModB", 2002);
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
        Assert.Equal("TestModB", loaded.ModList[0].Mod.Name); // loose first
        Assert.Equal("TestModA", loaded.ModList[1].Mod.Name); // grouped after

        var group = Assert.Single(loaded.ModList.ModGroups);
        Assert.Equal("Group G", group.Name);
        Assert.Equal(Color.FromArgb(0x80, 0x12, 0x34, 0x56).ToArgb(), group.HeaderColor.ToArgb());
        Assert.Equal(
            System.IO.Path.Combine(ModsRoot.FullName, "TestModA"),
            group[0].Mod.Directory.FullName
        );
    }

    [Fact]
    public void SaveLoad_RoundTripsActiveStates()
    {
        var modSerializer = new LibraryModSerializer([]);
        var modA = SaveMod(modSerializer, "TestModA", 1001);
        var modB = SaveMod(modSerializer, "TestModB", 2002);
        var profile = CreateProfile(modA, modB);
        profile.ModList.First(entry => ReferenceEquals(entry.Mod, modA)).Active = false;
        var sut = new ModProfileSerializer(modSerializer, _paths);
        sut.Save(profile);

        var loaded = sut.Load(ProfileFolder);

        Assert.False(loaded.ModList.Single(entry => entry.Mod.Name == "TestModA").Active);
        Assert.True(loaded.ModList.Single(entry => entry.Mod.Name == "TestModB").Active);
    }

    [Fact]
    public void Load_WithoutActiveStates_DefaultsInactive()
    {
        var mod = CreateMod("TestModA", 1001);
        var recording = new RecordingLibraryModSerializer(mod);
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

        Assert.False(loaded.ModList[0].Active);
    }

    [Fact]
    public void Load_ResolvesModsThroughInjectedLibraryModSerializer()
    {
        var mod = CreateMod("TestModA", 1001);
        var recording = new RecordingLibraryModSerializer(mod);
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
        Assert.Same(mod, loaded.ModList[0].Mod);
    }
}
