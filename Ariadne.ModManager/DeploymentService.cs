using System.Diagnostics;
using Ariadne.Contracts.ModManager;

namespace Ariadne.ModManager;

public sealed class DeploymentService(
    IInstanceService instances,
    IModDeploymentMethodFactory deploymentMethods,
    ILoadOrderBuilderResolver loadOrderBuilders
) : IDeploymentService
{
    private IModDeploymentMethod? _deployment;

    public bool IsDeployed { get; private set; }

    public IReadOnlyList<DirectoryInfo> DeployedPaths
    {
        get
        {
            if (_deployment is null || !instances.TryGetInstanceGame(out var game))
            {
                return [];
            }
            var paths = new List<DirectoryInfo>();
            foreach (var path in game.Configuration.Deployments.Prepend(game.Configuration.Root))
            {
                if (_deployment.TryGetDeployedPath(game, path, out var directory))
                {
                    paths.Add(directory);
                }
            }
            return paths;
        }
    }

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
            var mods = profile
                .ModList.Where(entry => entry.Active)
                .Select(entry => entry.Mod)
                .ToList();
            var loadOrderBuilder = loadOrderBuilders.GetFor(game.Configuration);
            await Task.Run(
                () =>
                {
                    method.Deploy(game, mods);
                    loadOrderBuilder?.Deploy(game, method, loadOrder);
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
