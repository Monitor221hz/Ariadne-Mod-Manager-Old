using System.Text.Json;
using Ariadne.Contracts.ModManager;
using Ariadne.ModManager.Serialization;

namespace Ariadne.ModManager;

public sealed class InstanceStore : IInstanceStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true,
    };

    private readonly FileInfo _configFile;
    private readonly Dictionary<string, DirectoryInfo> _instances;

    public string? LastActive { get; private set; }

    public InstanceStore(FileInfo configFile)
    {
        _configFile = configFile;
        _instances = [];
        if (configFile.Exists)
        {
            var record =
                JsonSerializer.Deserialize<InstancesConfigRecord>(
                    File.ReadAllText(configFile.FullName),
                    SerializerOptions
                ) ?? new InstancesConfigRecord([], null);
            _instances = record.Instances.ToDictionary(
                kv => kv.Key,
                kv => new DirectoryInfo(kv.Value)
            );
            LastActive = record.LastActive;
        }
    }

    public IReadOnlyDictionary<string, DirectoryInfo> Instances => _instances;

    public void Add(string name, DirectoryInfo folder)
    {
        if (_instances.ContainsKey(name))
        {
            throw new ArgumentException(
                $"An instance named \"{name}\" already exists.",
                nameof(name)
            );
        }
        _instances[name] = folder;
        Save();
    }

    public void Remove(string name)
    {
        if (!_instances.Remove(name))
        {
            return;
        }
        if (LastActive == name)
        {
            LastActive = null;
        }
        Save();
    }

    public void SetLastActive(string? name)
    {
        LastActive = name;
        Save();
    }

    private void Save()
    {
        var parent = _configFile.Directory;
        if (parent is { Exists: false })
        {
            parent.Create();
        }
        var record = new InstancesConfigRecord(
            _instances.ToDictionary(kv => kv.Key, kv => kv.Value.FullName),
            LastActive
        );
        File.WriteAllText(
            _configFile.FullName,
            JsonSerializer.Serialize(record, SerializerOptions)
        );
    }
}
