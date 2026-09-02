using Daedalus.Mods;
using Xunit;

namespace Daedalus.ModManager.Tests;

public class DeploymentPathsFactoryTests
{
    [Fact]
    public void Create_UsesProfileOverwriteFolder_NotManagerGlobal()
    {
        var paths = new ModManagerPaths(new DirectoryInfo(Path.Combine("C:", "Daedalus")));
        var sut = new DeploymentPathsFactory(paths);
        var profile = new ModProfile(
            "Main",
            new ModList([], []),
            new Version(1, 0),
            new DirectoryInfo(Path.Combine("C:", "Daedalus", "Profiles", "Main"))
        );

        var deploymentPaths = sut.Create(profile);

        Assert.Equal(profile.OverwriteFolder.FullName, deploymentPaths.OverwriteDirectory.FullName);
        Assert.Equal(paths.StagingFolder.FullName, deploymentPaths.StagingDirectory.FullName);
        Assert.NotEqual(
            Path.Combine(paths.AssemblyFolder.FullName, "Overwrite"),
            deploymentPaths.OverwriteDirectory.FullName
        );

        var otherProfile = new ModProfile(
            "Second",
            new ModList([], []),
            new Version(1, 0),
            new DirectoryInfo(Path.Combine("C:", "Daedalus", "Profiles", "Second"))
        );
        Assert.NotEqual(
            deploymentPaths.OverwriteDirectory.FullName,
            sut.Create(otherProfile).OverwriteDirectory.FullName
        );
    }
}
