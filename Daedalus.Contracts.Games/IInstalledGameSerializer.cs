namespace Daedalus.Contracts.Games;

public interface IInstalledGameSerializer
{
    IInstalledGame Load(FileInfo gameFile, ISupportedGame configuration);

    void Save(IInstalledGame game, DirectoryInfo folder);

    string GetFileName(IVendorInfo vendors);
}
