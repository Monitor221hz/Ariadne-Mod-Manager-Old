using Ariadne.Contracts.ModManager;
using Ariadne.VFS;

namespace Ariadne.ModManager;

public sealed class ConflictAnalysisService : IConflictAnalysisService
{
    private static readonly IReadOnlyDictionary<ILibraryMod, SelectedModVerdict> Empty =
        new Dictionary<ILibraryMod, SelectedModVerdict>();

    public IReadOnlyDictionary<ILibraryMod, SelectedModVerdict> ComputeVerdicts(
        IModProfile profile,
        ILibraryMod selected
    )
    {
        var mods = profile.ModList.Where(entry => entry.Active).Select(entry => entry.Mod).ToList();
        var focusIndex = mods.IndexOf(selected);
        if (focusIndex < 0 || mods.Count < 2)
        {
            return Empty;
        }

        var trees = mods.Select(mod => mod.Content).ToList();
        var conflicts = ConflictMapper<ModFileEntry>.MapConflictsFor(trees, focusIndex);
        var result = new Dictionary<ILibraryMod, SelectedModVerdict>();
        foreach (var conflict in conflicts)
        {
            foreach (var provider in conflict.Providers)
            {
                if (provider.Index == focusIndex)
                {
                    continue;
                }
                result[mods[provider.Index]] =
                    provider.Index < focusIndex
                        ? SelectedModVerdict.LosesToSelected
                        : SelectedModVerdict.BeatsSelected;
            }
        }
        return result;
    }
}
