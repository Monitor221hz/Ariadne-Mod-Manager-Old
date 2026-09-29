using System.Text.Json;
using Ariadne.Contracts.Games;

namespace Ariadne.Games.Serialization;

public sealed class InstalledGameSerializer : IInstalledGameSerializer
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true,
    };

    public IInstalledGame Load(FileInfo gameFile, ISupportedGame configuration)
    {
        var record =
            JsonSerializer.Deserialize<InstalledGameRecord>(
                File.ReadAllText(gameFile.FullName),
                SerializerOptions
            )
            ?? throw new InvalidOperationException(
                $"Installed game \"{gameFile.FullName}\" deserialized to null."
            );
        return new InstalledGame(new DirectoryInfo(record.InstallPath), configuration);
    }

    public void Save(IInstalledGame game, DirectoryInfo folder)
    {
        var record = new InstalledGameRecord(game.InstallPath.FullName);
        folder.Refresh();
        if (!folder.Exists)
        {
            folder.Create();
        }
        File.WriteAllText(
            Path.Join(folder.FullName, GetFileName(game.Configuration.Vendors)),
            JsonSerializer.Serialize(record, SerializerOptions)
        );
    }

    public string GetFileName(IVendorInfo vendors) =>
        vendors.Steam != 0 ? $"steam_{vendors.Steam}.json" : $"gog_{vendors.GOG}.json";
}
