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
    IReadOnlyList<DeploymentTargetGroup> GroupByTarget(
        IReadOnlyList<ILibraryMod> mods,
        ISupportedGame? configuration
    );

    VirtualNode<ModFileEntry> MergeContent(IReadOnlyList<ILibraryMod> mods);
}
