using Ariadne.Contracts.ModManager;

namespace Ariadne.ModManager;

public sealed class ModProfileEditor : IModProfileEditor
{
    public async Task RemoveModAsync(IModProfile profile, ILibraryMod mod)
    {
        var modList = profile.ModList;
        var entry = modList.FirstOrDefault(candidate => candidate.Mod.Equals(mod));
        if (entry is not null)
        {
            modList.Remove(entry);
        }
        await Task.Run(() => mod.Directory.Delete(true));
    }

    public bool TryAddMod(IModProfile profile, ILibraryMod mod)
    {
        var exists = profile.ModList.Any(entry =>
            string.Equals(
                entry.Mod.Directory.FullName,
                mod.Directory.FullName,
                StringComparison.OrdinalIgnoreCase
            )
        );
        if (exists)
        {
            return false;
        }
        profile.ModList.Add(new ModListEntry(mod, active: false));
        return true;
    }

    public IModGroup CreateGroup(IModProfile profile)
    {
        var baseName = "New Group";
        var name = baseName;
        var suffix = 2;
        while (profile.ModList.ModGroups.Any(group => group.Name == name))
        {
            name = $"{baseName} {suffix++}";
        }
        var group = new ModGroup(name, [], GroupHeaderColors.Random());
        profile.ModList.ModGroups.Add(group);
        return group;
    }

    public async Task DissolveGroupAsync(IModProfile profile, IModGroup group)
    {
        var modList = profile.ModList;
        var groupIndex = modList.ModGroups.IndexOf(group);
        modList.ModGroups.RemoveAt(groupIndex);
        if (groupIndex >= 0 && groupIndex < modList.ModGroups.Count)
        {
            var newGroup = modList.ModGroups[groupIndex];
            foreach (var mod in group)
            {
                newGroup.Add(mod);
            }
            return;
        }
        foreach (var mod in group)
        {
            modList.LooseMods.Add(mod);
        }
    }
}
