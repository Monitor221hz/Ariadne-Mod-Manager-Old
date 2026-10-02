using Ariadne.Contracts.Games;
using Ariadne.VFS;

namespace Ariadne.Contracts.ModManager;

public sealed record DeploymentTargetGroup(
    string DisplayKey,
    IGamePath? Path,
    IReadOnlyList<ILibraryMod> Mods
);

public interface IDeploymentPreviewService
{
    /// <summary>
    /// Groups the given mods (in priority order) by their install target, resolving each
    /// raw target string against the game configuration.
    /// </summary>
    IReadOnlyList<DeploymentTargetGroup> GroupByTarget(
        IReadOnlyList<ILibraryMod> mods,
        ISupportedGame? configuration
    );

    /// <summary>
    /// Overlays the content trees of the given mods (in priority order) into a single tree,
    /// mirroring what deployment would produce. Every node is guaranteed to carry data;
    /// synthesized directory entries use a placeholder origin.
    /// </summary>
    VirtualNode<ModFileEntry> MergeContent(IReadOnlyList<ILibraryMod> mods);
}
