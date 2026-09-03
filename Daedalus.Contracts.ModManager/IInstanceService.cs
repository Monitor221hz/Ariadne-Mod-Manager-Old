using Daedalus.Contracts.Games;

namespace Daedalus.Contracts.ModManager;

public interface IInstanceService
{
    IReadOnlyDictionary<string, DirectoryInfo> Instances { get; }
    string? CurrentName { get; }
    DirectoryInfo? CurrentFolder { get; }
    IInstalledGame? CurrentGame { get; }

    DirectoryInfo Create(string name, DirectoryInfo folder, IInstalledGame game);
    void Switch(string name);
    void Remove(string name, bool deleteFolder);
    IInstalledGame? ResolveGame(string instanceName);
}
