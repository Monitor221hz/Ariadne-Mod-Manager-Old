using Ariadne.Contracts.ModManager;

namespace Ariadne.ModManager;

public static class ModOrderSync
{
    public static void ApplyOrder(
        IReadOnlyList<IModListEntry> looseMods,
        IReadOnlyList<IModGroup> groups,
        IReadOnlyList<IReadOnlyList<IModListEntry>> groupMembers,
        IModList modList
    )
    {
        var loose = modList.LooseMods;
        loose.Clear();
        foreach (var entry in looseMods)
        {
            loose.Add(entry);
        }

        var modGroups = modList.ModGroups;
        modGroups.Clear();
        foreach (var group in groups)
        {
            modGroups.Add(group);
        }

        for (var i = 0; i < groups.Count; i++)
        {
            var group = groups[i];
            group.Clear();
            foreach (var entry in groupMembers[i])
            {
                group.Add(entry);
            }
        }
        if (modList is ModList concrete)
        {
            concrete.RebuildSet();
        }
    }
}
