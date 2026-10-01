using Ariadne.Contracts.ModManager;
using Ariadne.ModManager.Serialization;
using Xunit;

namespace Ariadne.ModManager.Tests;

public class ProfileServiceTests : IDisposable
{
    private sealed class FakePaths(DirectoryInfo root) : IModManagerPaths
    {
        public DirectoryInfo AssemblyFolder => root;
        public DirectoryInfo InstanceFolder => root;
        public DirectoryInfo StagingFolder => root;
        public DirectoryInfo ModsFolder => root;
        public DirectoryInfo ProfilesFolder { get; } = new(Path.Join(root.FullName, "Profiles"));
        public DirectoryInfo TemporaryFolder => root;
        public DirectoryInfo DownloadsFolder => root;
    }

    private readonly TempDirectory _temp = new();
    private readonly FakePaths _paths;
    private readonly ProfileService _service;

    public ProfileServiceTests()
    {
        _paths = new FakePaths(new DirectoryInfo(_temp.Path));
        _service = new ProfileService(
            new ModProfileSerializer(new LibraryModSerializer([]), _paths),
            _paths
        );
    }

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void Create_WritesProfile_AndListsIt()
    {
        var profile = _service.Create("NewProfile");

        Assert.Equal("NewProfile", profile.Name);
        Assert.Equal("NewProfile", profile.ProfileFolder.Name);
        Assert.True(
            File.Exists(
                Path.Join(
                    _paths.ProfilesFolder.FullName,
                    "NewProfile",
                    ModProfileSerializer.FileName
                )
            )
        );
        Assert.Equal(["NewProfile"], _service.Names);
    }

    [Fact]
    public void Switch_LoadsExistingProfile_AndSetsActive()
    {
        _service.Create("First");
        _service.Create("Second");

        var profile = _service.Switch("First");

        Assert.Same(profile, _service.Active);
        Assert.Equal("First", profile.ProfileFolder.Name);
    }

    [Fact]
    public void ActivateLatestOrDefault_CreatesDefault_WhenNoneExist()
    {
        var profile = _service.ActivateLatestOrDefault();

        Assert.Equal("Default", profile.ProfileFolder.Name);
        Assert.Same(profile, _service.Active);
        Assert.Contains("Default", _service.Names);
    }

    [Fact]
    public void ActivateLatestOrDefault_LoadsMostRecentlyWritten()
    {
        _service.Create("Old");
        _service.Create("Recent");
        File.SetLastWriteTimeUtc(
            Path.Join(_paths.ProfilesFolder.FullName, "Old", ModProfileSerializer.FileName),
            DateTime.UtcNow.AddDays(-1)
        );

        var profile = _service.ActivateLatestOrDefault();

        Assert.Equal("Recent", profile.ProfileFolder.Name);
    }

    [Fact]
    public void Read_LoadsWithoutChangingActive()
    {
        _service.Create("A");
        var active = _service.Switch("A");

        var read = _service.Read("A");

        Assert.NotSame(active, read);
        Assert.Equal("A", read.ProfileFolder.Name);
        Assert.Same(active, _service.Active);
    }

    [Fact]
    public void Switch_CoercesModelNameToFolderName()
    {
        var folder = new DirectoryInfo(Path.Join(_paths.ProfilesFolder.FullName, "Right"));
        var mismatched = new ModProfile("Wrong", new ModList([], []), new Version(1, 0), folder);
        mismatched.InitializeDisk();
        new ModProfileSerializer(new LibraryModSerializer([]), _paths).Save(mismatched);

        var profile = _service.Switch("Right");

        Assert.Equal("Right", profile.Name);
    }

    [Fact]
    public void SaveActive_PersistsActiveProfile()
    {
        _service.Create("Main");
        _service.Switch("Main");

        _service.SaveActive();

        var reloaded = new ProfileService(
            new ModProfileSerializer(new LibraryModSerializer([]), _paths),
            _paths
        ).Switch("Main");
        Assert.Equal("Main", reloaded.ProfileFolder.Name);
    }
}
