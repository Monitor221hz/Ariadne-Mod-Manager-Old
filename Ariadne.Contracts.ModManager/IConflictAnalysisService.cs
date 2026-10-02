namespace Ariadne.Contracts.ModManager;

public enum SelectedModVerdict
{
    LosesToSelected,
    BeatsSelected,
}

public interface IConflictAnalysisService
{
    /// <summary>
    /// Maps every active mod that conflicts with <paramref name="selected"/> to its verdict
    /// relative to the selected mod. Returns an empty map when the selected mod is not part
    /// of the active set or fewer than two mods are active.
    /// </summary>
    IReadOnlyDictionary<ILibraryMod, SelectedModVerdict> ComputeVerdicts(
        IModProfile profile,
        ILibraryMod selected
    );
}
