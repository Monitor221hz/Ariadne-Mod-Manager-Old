using System.Collections.Generic;
using Ariadne.Contracts.ModManager;

namespace Ariadne.ModManager.GUI.ViewModels;

internal static class WorkspaceLinkMaps
{
    public static IReadOnlyDictionary<
        IModInfo,
        IReadOnlyList<LoadOrderInfoViewModel>
    > BuildPluginRowsByOrigin(IEnumerable<LoadOrderInfoViewModel> pluginRows)
    {
        var map = new Dictionary<IModInfo, List<LoadOrderInfoViewModel>>(
            ReferenceEqualityComparer.Instance
        );
        foreach (var row in pluginRows)
        {
            if (!map.TryGetValue(row.Model.Origin, out var list))
            {
                list = [];
                map.Add(row.Model.Origin, list);
            }
            list.Add(row);
        }
        return map.ToDictionary(
            kv => kv.Key,
            kv => (IReadOnlyList<LoadOrderInfoViewModel>)kv.Value
        );
    }

    public static IReadOnlyDictionary<IModInfo, ModEntryNodeViewModel> BuildModRowByInfo(
        IEnumerable<ModEntryNodeViewModel> modRows
    )
    {
        var map = new Dictionary<IModInfo, ModEntryNodeViewModel>(
            ReferenceEqualityComparer.Instance
        );
        foreach (var row in modRows)
        {
            map[row.Model.Info] = row;
        }
        return map;
    }
}
