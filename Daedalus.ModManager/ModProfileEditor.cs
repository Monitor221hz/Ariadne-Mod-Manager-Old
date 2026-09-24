using Daedalus.Contracts.ModManager;

namespace Daedalus.ModManager;

public sealed class ModProfileEditor : IModProfileEditor
{
    public async Task RemoveModAsync(IModProfile profile, ILibraryMod mod)
    {
        var modList = profile.ModList;
        modList.Remove(mod);
        await Task.Run(() => mod.Directory.Delete(true));
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
