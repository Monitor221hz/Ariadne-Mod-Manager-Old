namespace Ariadne.Contracts.ModManager;

public interface IContentMoveService
{
    /// <summary>
    /// Whether the file or directory at <paramref name="sourcePath"/> may be moved into
    /// <paramref name="destinationDirectory"/> without colliding or moving into itself.
    /// </summary>
    bool CanMoveInto(
        string sourcePath,
        bool sourceIsDirectory,
        string displayName,
        string destinationDirectory
    );

    /// <summary>
    /// Whether the contents of <paramref name="sourceDirectory"/> may be flattened into
    /// <paramref name="modRootDirectory"/> without overwriting existing entries.
    /// </summary>
    bool CanFlattenIntoModRoot(string sourceDirectory, string modRootDirectory);

    void MoveInto(
        string sourcePath,
        bool sourceIsDirectory,
        string displayName,
        string destinationDirectory
    );

    /// <summary>Moves all children of the source directory into the mod root, then removes
    /// the source directory when it is empty.</summary>
    void FlattenIntoModRoot(string sourceDirectory, string modRootDirectory);
}
