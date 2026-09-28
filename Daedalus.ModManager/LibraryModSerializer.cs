using System.Text.Json;
using Daedalus.Contracts.ModManager;
using Daedalus.ModManager.Serialization;

namespace Daedalus.ModManager;

public sealed class LibraryModSerializer(IReadOnlyList<IArchiveReader> archiveReaders)
    : ILibraryModSerializer
{
    public const string FileName = "meta.json";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true,
    };

    public ILibraryMod Load(FileInfo metaFile)
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
        return new LibraryMod(
            new ModInfo(
                record.ID,
                record.IDSource,
                record.Version,
                record.Categories,
                record.Target,
                record.Priority,
                record.IsEnabled
            ),
            directory,
            archiveReaders
        );
    }

    public ILibraryMod Load(DirectoryInfo modFolder) =>
        Load(new FileInfo(Path.Join(modFolder.FullName, FileName)));

    public void Save(ILibraryMod mod)
    {
        var record = new ModInfoRecord(
            mod.Info.ID.Value,
            mod.Info.ID.Source,
            mod.Info.Version,
            mod.Info.Categories,
            mod.Info.Target,
            mod.Info.Priority,
            mod.Info.Active
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
