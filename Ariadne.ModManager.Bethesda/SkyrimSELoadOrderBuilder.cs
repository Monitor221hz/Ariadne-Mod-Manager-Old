using Ariadne.Contracts.Games;
using Ariadne.Contracts.ModManager;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Order;
using Mutagen.Bethesda.Skyrim;

namespace Ariadne.ModManager.Bethesda;

public class SkyrimSELoadOrderBuilder : ILoadOrderBuilder
{
    private const string PLUGINS_TXT = "plugins.txt";

    private static void WriteLoadOrder(
        IReadOnlyList<ILoadOrderInfo> loadOrderInfos,
        string pluginsTxtPath
    )
    {
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
        LoadOrder.Write(pluginsTxtPath, GameRelease.SkyrimSE, loadOrder, false);
    }

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
        var pluginsTxtPath = Path.Combine(appData.FullName, PLUGINS_TXT);
        WriteLoadOrder(loadOrderInfos, pluginsTxtPath);
    }

    public IEnumerable<ILoadOrderInfo> Fetch(IInstalledGame game, IModList mods)
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
                _depLookupMap[modPlugin.ModKey] = stub;
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

    public void Save(IModProfile currentProfile, IReadOnlyList<ILoadOrderInfo> loadOrderInfos)
    {
        var filePath = Path.Join(currentProfile.ProfileFolder.FullName, PLUGINS_TXT);
        WriteLoadOrder(loadOrderInfos, filePath);
    }

    public IEnumerable<ILoadOrderInfo> Sort(
        IModProfile currentProfile,
        IEnumerable<ILoadOrderInfo> loadOrderInfos
    )
    {
        var filePath = Path.Join(currentProfile.ProfileFolder.FullName, PLUGINS_TXT);
        if (!File.Exists(filePath))
        {
            foreach (var info in loadOrderInfos)
            {
                info.Active = true;
                yield return info;
            }
            yield break;
        }
        string[] pluginLines = File.ReadAllLines(filePath);
        Dictionary<ModKey, ILoadOrderInfo> pluginLookup = new();
        foreach (var plugin in loadOrderInfos)
        {
            if (plugin is not BethesdaPluginInfo bethPlugin)
            {
                continue;
            }
            pluginLookup.Add(bethPlugin.ModKey, plugin);
        }
        foreach (var pluginLine in pluginLines)
        {
            bool active = pluginLine.StartsWith("*");
            var pluginName = active ? pluginLine[1..] : pluginLine;
            if (!ModKey.TryFromNameAndExtension(pluginName, out var modKey))
            {
                continue;
            }
            if (pluginLookup.TryGetValue(modKey, out var pluginInfo))
            {
                pluginInfo.Active = active;
                yield return pluginInfo;
            }
        }
    }
}
