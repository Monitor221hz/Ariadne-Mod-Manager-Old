using System.Diagnostics.CodeAnalysis;
using Ariadne.Contracts.Games;

namespace Ariadne.Contracts.ModManager;

public sealed record CurrentInstance(string Name, DirectoryInfo Folder, IInstalledGame? Game);

public interface IInstanceService
{
    IReadOnlyDictionary<string, DirectoryInfo> Instances { get; }

    /// <summary>The active instance, or null when none is selected.
    /// <see cref="CurrentInstance.Game"/> is null only when the install cannot be resolved anymore.</summary>
    CurrentInstance? Current { get; }

    public bool TryGetInstance([NotNullWhen(true)] out CurrentInstance? instance)
    {
        if (Current is null)
        {
            instance = null;
            return false;
        }
        instance = Current;
        return true;
    }

    public bool TryGetInstanceGame([NotNullWhen(true)] out IInstalledGame? game)
    {
        if (Current?.Game is null)
        {
            game = null;
            return false;
        }
        game = Current.Game;
        return true;
    }

    string SuggestInstanceName(string baseName = "Default")
    {
        var name = baseName;
        var suffix = 2;
        while (Instances.Keys.Contains(name, StringComparer.OrdinalIgnoreCase))
        {
            name = $"{baseName} {suffix++}";
        }
        return name;
    }

    DirectoryInfo Create(string name, DirectoryInfo folder, IInstalledGame game);
    void Switch(string name);

    void Remove(string name, bool deleteFolder);

    IInstalledGame? ResolveGame(string instanceName);
}
