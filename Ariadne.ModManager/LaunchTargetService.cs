using System.Diagnostics;
using System.IO.Enumeration;
using Ariadne.Contracts.Games;
using Ariadne.Contracts.ModManager;

namespace Ariadne.ModManager;

public sealed class LaunchTargetService : ILaunchTargetService, IDisposable
{
    private const string ExecutablePattern = "*.exe";

    private readonly IInstanceService _instances;
    private readonly IDeploymentService _deployment;

    public LaunchTargetService(IInstanceService instances, IDeploymentService deployment)
    {
        _instances = instances;
        _deployment = deployment;
        _deployment.DeploymentChanged += OnDeploymentChanged;
        Rescan();
    }

    public IReadOnlyList<LaunchTarget> LaunchTargets { get; private set; } = [];

    public event EventHandler? LaunchTargetsChanged;

    public void Rescan()
    {
        var targets = new List<LaunchTarget>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (_instances.Current?.Game is { } game)
        {
            foreach (var path in _deployment.DeployedPaths)
            {
                path.Refresh();
                if (!path.Exists)
                {
                    continue;
                }
                foreach (
                    var file in path.EnumerateFiles(ExecutablePattern, SearchOption.AllDirectories)
                )
                {
                    if (seen.Add(file.FullName))
                    {
                        targets.Add(
                            new LaunchTarget(
                                file.Name,
                                file.FullName,
                                PriorityOf(game.Configuration, file.Name)
                            )
                        );
                    }
                }
            }
        }
        LaunchTargets = targets
            .OrderBy(target => target.Priority ?? int.MaxValue)
            .ThenBy(target => target.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        LaunchTargetsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Launch(LaunchTarget target)
    {
        Process.Start(
            new ProcessStartInfo
            {
                FileName = target.AbsolutePath,
                UseShellExecute = true,
                WorkingDirectory = Path.GetDirectoryName(target.AbsolutePath),
            }
        );
    }

    public void Dispose()
    {
        _deployment.DeploymentChanged -= OnDeploymentChanged;
    }

    private void OnDeploymentChanged(object? sender, EventArgs args) => Rescan();

    private static int? PriorityOf(ISupportedGame configuration, string fileName)
    {
        foreach (var (pattern, priority) in configuration.LaunchTargets)
        {
            if (FileSystemName.MatchesSimpleExpression(pattern, fileName, ignoreCase: true))
            {
                return priority;
            }
        }
        return null;
    }
}
