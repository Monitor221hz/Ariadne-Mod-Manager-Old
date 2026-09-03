namespace Daedalus.Contracts.Mods;

public interface ILibraryModSerializer
{
    ILibraryMod Load(FileInfo metaFile);
    ILibraryMod Load(DirectoryInfo modFolder);
    void Save(ILibraryMod mod);
}
