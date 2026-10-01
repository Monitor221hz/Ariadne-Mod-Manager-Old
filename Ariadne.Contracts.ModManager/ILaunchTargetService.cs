namespace Ariadne.Contracts.ModManager;

public sealed record LaunchTarget(string Name, string AbsolutePath, int? Priority);

public interface ILaunchTargetService
{
    IReadOnlyList<LaunchTarget> LaunchTargets { get; }

    event EventHandler? LaunchTargetsChanged;

    void Rescan();

    void Launch(LaunchTarget target);
}
