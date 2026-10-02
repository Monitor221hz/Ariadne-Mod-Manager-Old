namespace Ariadne.Contracts.ModManager;

public interface ILibraryImportService
{
    IReadOnlyList<ILibraryMod> ScanLibrary();

    IReadOnlyList<ILibraryMod> FindMissingMods(IModProfile target, IModProfile source);
}
