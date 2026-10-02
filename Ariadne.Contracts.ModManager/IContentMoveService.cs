namespace Ariadne.Contracts.ModManager;

public interface IContentMoveService
{
    bool CanMoveInto(
        string sourcePath,
        bool sourceIsDirectory,
        string displayName,
        string destinationDirectory
    );

    bool CanFlattenIntoModRoot(string sourceDirectory, string modRootDirectory);

    void MoveInto(
        string sourcePath,
        bool sourceIsDirectory,
        string displayName,
        string destinationDirectory
    );

    void FlattenIntoModRoot(string sourceDirectory, string modRootDirectory);

    bool ShouldInheritInfo(ILibraryMod destination);

    void InheritInfo(ILibraryMod source, ILibraryMod destination);
}
