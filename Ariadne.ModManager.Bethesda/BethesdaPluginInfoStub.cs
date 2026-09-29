using Mutagen.Bethesda.Plugins;

namespace Ariadne.ModManager.Bethesda;

public class BethesdaPluginInfoStub : IModKeyed
{
    public ModKey ModKey { get; }
    public List<ModKey> Masters { get; }
    public BethesdaPluginInfo? pluginInfo { get; set; }

    public BethesdaPluginInfoStub(ModKey modKey, List<ModKey> masters)
    {
        ModKey = modKey;
        Masters = masters;
    }
}
