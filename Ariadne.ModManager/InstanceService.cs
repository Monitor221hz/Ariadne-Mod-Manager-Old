using Ariadne.Contracts.Games;
using Ariadne.Contracts.ModManager;

namespace Ariadne.ModManager;

public sealed class InstanceService : IInstanceService
{
    private readonly IInstanceStore _store;
    private readonly IInstalledGameSerializer _gameSerializer;
    private readonly IGameCatalog _catalog;

    public InstanceService(
        IInstanceStore store,
        IInstalledGameSerializer gameSerializer,
        IGameCatalog catalog
    )
    {
        _store = store;
        _gameSerializer = gameSerializer;
        _catalog = catalog;
    }

    public IReadOnlyDictionary<string, DirectoryInfo> Instances => _store.Instances;

    public CurrentInstance? Current
    {
        get
        {
            var name = _store.LastActive;
            if (name is null || !_store.Instances.TryGetValue(name, out var folder))
            {
                return null;
            }
            return new CurrentInstance(name, folder, ResolveGame(name));
        }
    }

    public DirectoryInfo Create(string name, DirectoryInfo folder, IInstalledGame game)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Instance name must not be empty.", nameof(name));
        }
        folder.Create();
        Directory.CreateDirectory(Path.Join(folder.FullName, "Mods"));
        Directory.CreateDirectory(Path.Join(folder.FullName, "Profiles"));
        Directory.CreateDirectory(Path.Join(folder.FullName, "Staging"));
        _gameSerializer.Save(game, folder);
        _store.Add(name, folder);
        _store.SetLastActive(name);
        return folder;
    }

    public void Switch(string name)
    {
        if (!_store.Instances.ContainsKey(name))
        {
            throw new ArgumentException($"Unknown instance \"{name}\".", nameof(name));
        }
        _store.SetLastActive(name);
    }

    public void Remove(string name, bool deleteFolder)
    {
        if (!_store.Instances.TryGetValue(name, out var folder))
        {
            throw new ArgumentException($"Unknown instance \"{name}\".", nameof(name));
        }
        _store.Remove(name);
        if (deleteFolder && folder.Exists)
        {
            folder.Delete(recursive: true);
        }
    }

    public IInstalledGame? ResolveGame(string instanceName)
    {
        if (!_store.Instances.TryGetValue(instanceName, out var folder))
        {
            throw new ArgumentException(
                $"Unknown instance \"{instanceName}\".",
                nameof(instanceName)
            );
        }
        folder.Refresh();
        if (!folder.Exists)
        {
            return null;
        }
        foreach (var file in folder.EnumerateFiles("*.json", SearchOption.TopDirectoryOnly))
        {
            var game = _catalog.Games.FirstOrDefault(g =>
                _gameSerializer.GetFileName(g.Vendors) == file.Name
            );
            if (game is not null)
            {
                return _gameSerializer.Load(file, game);
            }
        }
        return null;
    }
}
