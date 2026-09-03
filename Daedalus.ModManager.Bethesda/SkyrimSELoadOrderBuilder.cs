using Daedalus.Contracts.Games;
using Daedalus.Contracts.ModManager;
using Daedalus.Contracts.Mods;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Order;
using Mutagen.Bethesda.Skyrim;
using Reloaded.Memory.Extensions;

namespace Daedalus.ModManager.Bethesda;

public class SkyrimSELoadOrderBuilder : ILoadOrderBuilder
{
    public void Deploy(
        IInstalledGame game,
        IModDeploymentMethod deploymentMethod,
        IReadOnlyList<ILoadOrderInfo> loadOrderInfos
    )
    {
        var configuration = game.Configuration;
        if (!deploymentMethod.TryGetDeployedPath(game, configuration["AppData"], out var appData))
        {
            return;
        }
        var pluginsTxtPath = Path.Combine(appData.FullName, "plugins.txt");
        var listings = new List<LoadOrderListing>();
        foreach (var lo in loadOrderInfos)
        {
            if (lo is not BethesdaPluginInfo pluginInfo)
            {
                continue;
            }
            listings.Add(new LoadOrderListing(pluginInfo.ModKey, lo.Active));
        }
        var loadOrder = LoadOrder.Import<ISkyrimModGetter>(listings, GameRelease.SkyrimSE);
        LoadOrder.Write(pluginsTxtPath, GameRelease.SkyrimSE, loadOrder, true);
    }

    public IEnumerable<ILoadOrderInfo> Fetch(IInstalledGame game, IReadOnlyList<ILibraryMod> mods)
    {
        Dictionary<ModKey, BethesdaPluginInfoStub> _depLookupMap = new();
        foreach (var mod in mods)
        {
            foreach (var node in mod.Content.Children)
            {
                if (node.IsDirectory)
                {
                    continue;
                }
                var extension = Path.GetExtension(node.Name).ToLowerInvariant();
                if (extension is not (".esp" or ".esm" or ".esl"))
                {
                    continue;
                }
                var file = new FileInfo(node.Data!.AbsolutePath);
                using var modPlugin = SkyrimMod.CreateFromBinaryOverlay(
                    file.FullName,
                    SkyrimRelease.SkyrimSE
                );
                var stub = new BethesdaPluginInfoStub(
                    modPlugin.ModKey,
                    modPlugin.ModHeader.MasterReferences.Select(s => s.Master).ToList()
                )
                {
                    pluginInfo = new BethesdaPluginInfo(
                        modPlugin.ModKey,
                        mod.Info,
                        [file],
                        [],
                        false
                    ),
                };
                _depLookupMap.Add(modPlugin.ModKey, stub);
            }
        }
        foreach (var stub in _depLookupMap.Values)
        {
            if (stub.pluginInfo == null)
            {
                continue;
            }
            List<ILoadOrderInfo> dependencies = new(stub.Masters.Count);
            foreach (var master in stub.Masters)
            {
                if (_depLookupMap.TryGetValue(master, out var masterStub))
                {
                    if (masterStub.pluginInfo != null)
                    {
                        dependencies.Add(masterStub.pluginInfo);
                    }
                }
            }
            stub.pluginInfo.Dependencies = dependencies;
            yield return stub.pluginInfo;
        }
    }
}
