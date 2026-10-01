using Ariadne.Contracts.Games;
using Ariadne.Contracts.ModManager;
using Ariadne.Games;
using Xunit;

namespace Ariadne.ModManager.Tests;

public class LaunchTargetServiceTests : IDisposable
{
    private sealed class FakeDeployment : IDeploymentService
    {
        public bool IsDeployed { get; private set; }
        public IReadOnlyList<DirectoryInfo> DeployedPaths { get; private set; } = [];

        public event EventHandler? DeploymentChanged;

        public void Deploy(params DirectoryInfo[] paths)
        {
            IsDeployed = true;
            DeployedPaths = paths;
            DeploymentChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Undeploy()
        {
            IsDeployed = false;
            DeployedPaths = [];
            DeploymentChanged?.Invoke(this, EventArgs.Empty);
        }

        public Task DeployAsync(
            IModProfile profile,
            IReadOnlyList<ILoadOrderInfo> loadOrder,
            CancellationToken cancellationToken = default
        ) => throw new NotSupportedException();

        public Task UndeployAsync() => throw new NotSupportedException();
    }

    private sealed class FakeInstances(IInstalledGame game) : IInstanceService
    {
        public IReadOnlyDictionary<string, DirectoryInfo> Instances =>
            new Dictionary<string, DirectoryInfo>();
        public CurrentInstance? Current { get; } = new("main", new DirectoryInfo("."), game);

        public DirectoryInfo Create(string name, DirectoryInfo folder, IInstalledGame g) => folder;

        public void Switch(string name) { }

        public void Remove(string name, bool deleteFolder) { }

        public IInstalledGame? ResolveGame(string instanceName) => game;
    }

    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    private static IInstalledGame GameAt(
        DirectoryInfo installDir,
        IReadOnlyDictionary<string, int> launchTargets
    )
    {
        var config = new SupportedGame(
            "Test Game",
            [],
            new VendorInfo(0, 0),
            new GamePath("_root", "", [], []),
            [],
            [],
            launchTargets: launchTargets
        );
        installDir.Create();
        return new InstalledGame(installDir, config);
    }

    private DirectoryInfo DeployedFolder(params string[] exeNames)
    {
        var folder = new DirectoryInfo(_temp.Combine("deployed"));
        folder.Create();
        foreach (var name in exeNames)
        {
            File.WriteAllText(Path.Combine(folder.FullName, name), "fake");
        }
        return folder;
    }

    [Fact]
    public void Rescan_WithoutDeployment_YieldsEmpty()
    {
        var service = new LaunchTargetService(
            new FakeInstances(
                GameAt(new DirectoryInfo(_temp.Combine("game")), new Dictionary<string, int>())
            ),
            new FakeDeployment()
        );

        Assert.Empty(service.LaunchTargets);
    }

    [Fact]
    public void Scan_AfterDeployment_SortsRegisteredFirst_ThenUnregistered()
    {
        var deployed = DeployedFolder("skse64_loader.exe", "SkyrimSE.exe", "random_tool.exe");
        var deployment = new FakeDeployment();
        var service = new LaunchTargetService(
            new FakeInstances(
                GameAt(
                    new DirectoryInfo(_temp.Combine("game")),
                    new Dictionary<string, int> { ["SkyrimSE.exe"] = 1, ["skse64_loader.exe"] = 0 }
                )
            ),
            deployment
        );

        deployment.Deploy(deployed);

        Assert.Equal(
            new[] { "skse64_loader.exe", "SkyrimSE.exe", "random_tool.exe" },
            service.LaunchTargets.Select(target => target.Name).ToArray()
        );
    }

    [Fact]
    public void Scan_WildcardPattern_MatchesCaseInsensitively()
    {
        var deployed = DeployedFolder("SKSE64_loader.exe", "other.exe");
        var deployment = new FakeDeployment();
        var service = new LaunchTargetService(
            new FakeInstances(
                GameAt(
                    new DirectoryInfo(_temp.Combine("game")),
                    new Dictionary<string, int> { ["skse64*"] = 0 }
                )
            ),
            deployment
        );

        deployment.Deploy(deployed);

        Assert.Equal(0, service.LaunchTargets[0].Priority);
        Assert.Equal("SKSE64_loader.exe", service.LaunchTargets[0].Name);
        Assert.Null(service.LaunchTargets[1].Priority);
    }

    [Fact]
    public void Undeploy_ClearsTargets()
    {
        var deployed = DeployedFolder("SkyrimSE.exe");
        var deployment = new FakeDeployment();
        var service = new LaunchTargetService(
            new FakeInstances(
                GameAt(new DirectoryInfo(_temp.Combine("game")), new Dictionary<string, int>())
            ),
            deployment
        );
        deployment.Deploy(deployed);
        Assert.Single(service.LaunchTargets);

        deployment.Undeploy();

        Assert.Empty(service.LaunchTargets);
    }
}
