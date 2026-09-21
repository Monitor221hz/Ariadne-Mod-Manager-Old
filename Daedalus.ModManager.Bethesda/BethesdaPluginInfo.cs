using Daedalus.Contracts.Mods;
using Mutagen.Bethesda.Plugins;

namespace Daedalus.ModManager.Bethesda;

public class BethesdaPluginInfo : ILoadOrderInfo, IModKeyed
{
    public ModKey ModKey { get; }
    public string Name => ModKey.ToString();
    public IModInfo Origin { get; set; }
    public IReadOnlyList<FileInfo> Artifacts { get; set; }
    public IReadOnlyList<ILoadOrderInfo> Dependencies { get; set; }
    public bool Active { get; set; }

    public BethesdaPluginInfo(
        ModKey modKey,
        IModInfo origin,
        IReadOnlyList<FileInfo> artifacts,
        List<ILoadOrderInfo> dependencies,
        bool active
    )
    {
        ModKey = modKey;
        Origin = origin;
        Artifacts = artifacts;
        Dependencies = dependencies;
        Active = active;
    }
}
