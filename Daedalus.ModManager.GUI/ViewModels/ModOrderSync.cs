using Daedalus.Contracts.ModManager;

namespace Daedalus.ModManager.GUI.ViewModels;

public static class ModOrderSync
{
    public static void ApplyOrder(
        IReadOnlyList<ILibraryMod> looseMods,
        IReadOnlyList<IModGroup> groups,
        IReadOnlyList<IReadOnlyList<ILibraryMod>> groupMembers,
        IModList modList
    )
    {
        var loose = modList.LooseMods;
        loose.Clear();
        foreach (var mod in looseMods)
        {
            loose.Add(mod);
        }

        var modGroups = modList.ModGroups;
        modGroups.Clear();
        foreach (var group in groups)
        {
            modGroups.Add(group);
        }

        var priority = 0u;
        foreach (var mod in looseMods)
        {
            mod.Info.Priority = ++priority;
        }
        for (var i = 0; i < groups.Count; i++)
        {
            var group = groups[i];
            group.Clear();
            foreach (var mod in groupMembers[i])
            {
                mod.Info.Priority = ++priority;
                group.Add(mod);
            }
        }
    }
}
