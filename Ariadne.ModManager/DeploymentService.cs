using System.Diagnostics;
using Ariadne.Contracts.ModManager;

namespace Ariadne.ModManager;

public sealed class DeploymentService(
    IInstanceService instances,
    IModDeploymentMethodFactory deploymentMethods,
    ILoadOrderBuilder loadOrderBuilder
) : IDeploymentService
{
    private IModDeploymentMethod? _deployment;

    public bool IsDeployed { get; private set; }

    public event EventHandler? DeploymentChanged;

    public async Task DeployAsync(
        IModProfile profile,
        IReadOnlyList<ILoadOrderInfo> loadOrder,
        CancellationToken cancellationToken = default
    )
    {
        if (IsDeployed || !instances.TryGetInstanceGame(out var game))
        {
            return;
        }
        var method = deploymentMethods.Create(profile);
        try
        {
            var mods = profile.ModList.Where(mod => mod.Info.Active).ToList();
            await Task.Run(
                () =>
                {
                    method.Deploy(game, mods);
                    loadOrderBuilder.Deploy(game, method, loadOrder);
                },
                cancellationToken
            );
        }
        catch
        {
            method.Dispose();
            throw;
        }
        _deployment = method;
        IsDeployed = true;
        DeploymentChanged?.Invoke(this, EventArgs.Empty);
    }

    public Task UndeployAsync()
    {
        var method = _deployment;
        _deployment = null;
        if (method is null)
        {
            return Task.CompletedTask;
        }
        IsDeployed = false;
        DeploymentChanged?.Invoke(this, EventArgs.Empty);
        return Task.Run(() =>
        {
            try
            {
                if (instances.TryGetInstanceGame(out var game))
                {
                    method.Revert(game);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }
            method.Dispose();
        });
    }
}
