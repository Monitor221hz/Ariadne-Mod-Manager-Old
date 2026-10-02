using Ariadne.Contracts.Games;
using Ariadne.Contracts.ModManager;
using Ariadne.ModManager.GUI.ViewModels;
using Xunit;

namespace Ariadne.ModManager.GUI.Tests;

public class InstanceSetupViewModelTests
{
    private sealed class FakePaths : IModManagerPaths
    {
        private readonly DirectoryInfo _root = new(".");
        public DirectoryInfo AssemblyFolder => _root;
        public DirectoryInfo InstanceFolder => _root;
        public DirectoryInfo StagingFolder => _root;
        public DirectoryInfo ModsFolder => _root;
        public DirectoryInfo ProfilesFolder => _root;
        public DirectoryInfo TemporaryFolder => _root;
        public DirectoryInfo DownloadsFolder => _root;
    }

    private sealed class FakeInstances : IInstanceService
    {
        public IReadOnlyDictionary<string, DirectoryInfo> Instances =>
            new Dictionary<string, DirectoryInfo>();
        public CurrentInstance? Current => null;

        public DirectoryInfo Create(string name, DirectoryInfo folder, IInstalledGame game) =>
            folder;

        public void Switch(string name) { }

        public void Remove(string name, bool deleteFolder) { }

        public IInstalledGame? ResolveGame(string instanceName) => null;
    }

    private sealed class FakeGame : IInstalledGame
    {
        public DirectoryInfo InstallPath => new(".");
        public ISupportedGame Configuration => null!;

        public string LookupAbsolutePath(IGamePath path) => path.Key;

        public string UpdateAbsolutePath(IGamePath path) => path.Key;
    }

    private static InstanceSetupViewModel CreateViewModel() =>
        new(new FakeInstances(), new FakePaths(), new FakeGame());

    [Fact]
    public void InstanceName_Strips_Invalid_Path_Characters()
    {
        var viewModel = CreateViewModel();

        viewModel.InstanceName = "a<b>:c*d?e/f\\g|h\"i";

        Assert.Equal("abcdefghi", viewModel.InstanceName);
    }
}
