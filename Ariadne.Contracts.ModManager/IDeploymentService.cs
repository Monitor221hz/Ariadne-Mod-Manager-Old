namespace Ariadne.Contracts.ModManager;

public interface IDeploymentService
{
    bool IsDeployed { get; }

    IReadOnlyList<DirectoryInfo> DeployedPaths { get; }

    event EventHandler? DeploymentChanged;

    Task DeployAsync(
        IModProfile profile,
        IReadOnlyList<ILoadOrderInfo> loadOrder,
        CancellationToken cancellationToken = default
    );

    Task UndeployAsync();
}
