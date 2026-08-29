using Daedalus.Contracts.Mods;

namespace Daedalus.Mods;

public abstract class LoadOrderInfo : ILoadOrderInfo
{
    public IModInfo Origin { get; }
    public IReadOnlyList<FileInfo> Artifacts { get; }
    public IReadOnlyList<ILoadOrderInfo> Dependencies { get; }
    public bool Active { get; set; }

    public LoadOrderInfo(
        IModInfo origin,
        IReadOnlyList<FileInfo> artifacts,
        IReadOnlyList<ILoadOrderInfo> dependencies,
        bool active
    )
    {
        Origin = origin;
        Artifacts = artifacts;
        Dependencies = dependencies;
        Active = active;
    }
}
