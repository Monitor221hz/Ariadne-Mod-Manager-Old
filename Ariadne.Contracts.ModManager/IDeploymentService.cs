namespace Ariadne.Contracts.ModManager;

public interface IDeploymentService
{
    bool IsDeployed { get; }

    event EventHandler? DeploymentChanged;

    Task DeployAsync(
        IModProfile profile,
        IReadOnlyList<ILoadOrderInfo> loadOrder,
        CancellationToken cancellationToken = default
    );

    Task UndeployAsync();
}
