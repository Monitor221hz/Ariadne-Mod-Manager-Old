using Xunit;

namespace Ariadne.ModManager.Tests;

public class InstanceStoreTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    private FileInfo ConfigFile => new(_temp.Combine("instances.json"));

    [Fact]
    public void StartsEmpty_WhenConfigMissing()
    {
        var store = new InstanceStore(ConfigFile);

        Assert.Empty(store.Instances);
        Assert.Null(store.LastActive);
        Assert.False(ConfigFile.Exists);
    }

    [Fact]
    public void Add_PersistsAndReloads()
    {
        var store = new InstanceStore(ConfigFile);
        store.Add("Default", new DirectoryInfo(_temp.Combine("Default")));

        var reloaded = new InstanceStore(ConfigFile);

        var folder = Assert.Single(reloaded.Instances);
        Assert.Equal("Default", folder.Key);
        Assert.Equal(_temp.Combine("Default"), folder.Value.FullName);
    }

    [Fact]
    public void Add_DuplicateName_Throws()
    {
        var store = new InstanceStore(ConfigFile);
        store.Add("Default", new DirectoryInfo(_temp.Combine("Default")));

        Assert.Throws<ArgumentException>(() =>
            store.Add("Default", new DirectoryInfo(_temp.Combine("Other")))
        );
    }

    [Fact]
    public void SetLastActive_Persists_AndRemoveClearsIt()
    {
        var store = new InstanceStore(ConfigFile);
        store.Add("A", new DirectoryInfo(_temp.Combine("A")));
        store.Add("B", new DirectoryInfo(_temp.Combine("B")));
        store.SetLastActive("B");

        Assert.Equal("B", new InstanceStore(ConfigFile).LastActive);

        store.Remove("B");

        Assert.Null(store.LastActive);
        Assert.Null(new InstanceStore(ConfigFile).LastActive);
        Assert.Single(store.Instances);
    }
}
