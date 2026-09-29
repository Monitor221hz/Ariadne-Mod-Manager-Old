using Xunit;

namespace Ariadne.ModManager.Tests;

public class DeploymentPathsFactoryTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    private ModManagerPaths CreatePaths()
    {
        var service = TestAssets.CreateInstanceService(
            new FileInfo(_temp.Combine("instances.json"))
        );
        service.Create(
            "test",
            new DirectoryInfo(_temp.Combine("instance")),
            TestAssets.GameAt(new DirectoryInfo(_temp.Combine("game")))
        );
        return new ModManagerPaths(new DirectoryInfo(_temp.Path), service);
    }

    private ModProfile CreateProfile(string name) =>
        new(
            name,
            new ModList([], []),
            new Version(1, 0),
            new DirectoryInfo(_temp.Combine("instance", "Profiles", name))
        );

    [Fact]
    public void Create_UsesProfileOverwriteFolder_AndInstanceStaging()
    {
        var paths = CreatePaths();
        var sut = new DeploymentPathsFactory(paths);
        var profile = CreateProfile("Main");

        var deploymentPaths = sut.Create(profile);

        Assert.Equal(profile.OverwriteFolder.FullName, deploymentPaths.OverwriteDirectory.FullName);
        Assert.Equal(paths.StagingFolder.FullName, deploymentPaths.StagingDirectory.FullName);
    }

    [Fact]
    public void Create_OverwriteDiffersPerProfile()
    {
        var sut = new DeploymentPathsFactory(CreatePaths());

        var first = sut.Create(CreateProfile("Main")).OverwriteDirectory.FullName;
        var second = sut.Create(CreateProfile("Second")).OverwriteDirectory.FullName;

        Assert.NotEqual(first, second);
    }
}
