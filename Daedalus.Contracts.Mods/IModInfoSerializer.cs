namespace Daedalus.Contracts.Mods;

public interface IModInfoSerializer
{
    IModInfo Load(FileInfo metaFile);

    IModInfo Load(DirectoryInfo modFolder);

    void Save(IModInfo mod);
}
