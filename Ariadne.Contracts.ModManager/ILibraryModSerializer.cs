namespace Ariadne.Contracts.ModManager;

public interface ILibraryModSerializer
{
    ILibraryMod Load(FileInfo metaFile);
    ILibraryMod Load(DirectoryInfo modFolder);
    void Save(ILibraryMod mod);
}
