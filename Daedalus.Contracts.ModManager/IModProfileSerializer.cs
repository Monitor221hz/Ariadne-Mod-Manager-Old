namespace Daedalus.Contracts.ModManager;

public interface IModProfileSerializer
{
    IModProfile Load(FileInfo profileFile);

    IModProfile Load(DirectoryInfo profileFolder);

    void Save(IModProfile profile);
}
