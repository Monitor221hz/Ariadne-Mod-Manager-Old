using Xunit;

namespace Ariadne.ModManager.Tests;

public class ModManagerPathsTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    private ModManagerPaths CreatePaths(out DirectoryInfo instanceFolder)
    {
        instanceFolder = new DirectoryInfo(_temp.Combine("instance"));
        var service = TestAssets.CreateInstanceService(
            new FileInfo(_temp.Combine("instances.json"))
        );
        service.Create(
            "test",
            instanceFolder,
            TestAssets.GameAt(new DirectoryInfo(_temp.Combine("game")))
        );
        return new ModManagerPaths(new DirectoryInfo(_temp.Path), service);
    }

    [Fact]
    public void InstanceFolders_DeriveFromActiveInstance()
    {
        var paths = CreatePaths(out var instanceFolder);

        Assert.Equal(new DirectoryInfo(_temp.Path).FullName, paths.AssemblyFolder.FullName);
        Assert.Equal(instanceFolder.FullName, paths.InstanceFolder.FullName);
        Assert.Equal(Path.Combine(instanceFolder.FullName, "Mods"), paths.ModsFolder.FullName);
        Assert.Equal(
            Path.Combine(instanceFolder.FullName, "Profiles"),
            paths.ProfilesFolder.FullName
        );
        Assert.Equal(
            Path.Combine(instanceFolder.FullName, "Staging"),
            paths.StagingFolder.FullName
        );
    }

    [Fact]
    public void NoActiveInstance_InstanceFoldersThrow()
    {
        var service = TestAssets.CreateInstanceService(
            new FileInfo(_temp.Combine("instances.json"))
        );
        var paths = new ModManagerPaths(new DirectoryInfo(_temp.Path), service);

        Assert.Throws<InvalidOperationException>(() => paths.InstanceFolder);
        Assert.Throws<InvalidOperationException>(() => paths.ModsFolder);
    }
}
