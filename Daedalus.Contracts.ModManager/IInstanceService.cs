using Daedalus.Contracts.Games;

namespace Daedalus.Contracts.ModManager;

public sealed record CurrentInstance(string Name, DirectoryInfo Folder, IInstalledGame? Game);

public interface IInstanceService
{
    IReadOnlyDictionary<string, DirectoryInfo> Instances { get; }

    /// <summary>The active instance, or null when none is selected.
    /// <see cref="CurrentInstance.Game"/> is null only when the install cannot be resolved anymore.</summary>
    CurrentInstance? Current { get; }

    DirectoryInfo Create(string name, DirectoryInfo folder, IInstalledGame game);
    void Switch(string name);

    void Remove(string name, bool deleteFolder);

    IInstalledGame? ResolveGame(string instanceName);
}
