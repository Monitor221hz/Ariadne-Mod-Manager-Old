using System.Text.Json;
using Daedalus.Contracts.Mods;

namespace Daedalus.Mods.Serialization;

public sealed class ModInfoSerializer : IModInfoSerializer
{
    public const string FileName = "meta.json";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true,
    };

    public IModInfo Load(FileInfo metaFile)
    {
        var record =
            JsonSerializer.Deserialize<ModInfoRecord>(
                File.ReadAllText(metaFile.FullName),
                SerializerOptions
            )
            ?? throw new InvalidOperationException(
                $"Mod metadata \"{metaFile.FullName}\" deserialized to null."
            );
        var directory =
            metaFile.Directory
            ?? throw new InvalidOperationException(
                $"Mod metadata file \"{metaFile.FullName}\" has no parent directory."
            );
        return new ModInfo(
            record.ID,
            record.Name,
            directory,
            record.IDSource,
            record.Version,
            record.Categories,
            record.Target
        )
        {
            Priority = record.Priority,
        };
    }

    public IModInfo Load(DirectoryInfo modFolder) =>
        Load(new FileInfo(Path.Join(modFolder.FullName, FileName)));

    public void Save(IModInfo mod)
    {
        var record = new ModInfoRecord(
            mod.ID,
            mod.Name,
            mod.IDSource,
            mod.Version,
            mod.Categories,
            mod.Target,
            mod.Priority
        );
        mod.Directory.Refresh();
        if (!mod.Directory.Exists)
        {
            mod.Directory.Create();
        }
        File.WriteAllText(
            Path.Join(mod.Directory.FullName, FileName),
            JsonSerializer.Serialize(record, SerializerOptions)
        );
    }
}
