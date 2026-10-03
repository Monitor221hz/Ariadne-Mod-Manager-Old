namespace Ariadne.Contracts.ModManager;

public enum SelectedModVerdict
{
    LosesToSelected,
    BeatsSelected,
}

public interface IConflictAnalysisService
{
    IReadOnlyDictionary<ILibraryMod, SelectedModVerdict> ComputeVerdicts(
        IModProfile profile,
        ILibraryMod selected
    );
}
