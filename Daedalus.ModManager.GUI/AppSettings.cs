using System.Text.Json;

namespace Daedalus.ModManager.GUI;

public static class AppSettings
{
    private sealed record SettingsRecord(string? Theme);

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
    };

    private static FileInfo SettingsFile =>
        new(Path.Join(AppContext.BaseDirectory, "appsettings.json"));

    public static string? LoadThemeId()
    {
        var file = SettingsFile;
        if (!file.Exists)
        {
            return null;
        }
        try
        {
            return JsonSerializer
                .Deserialize<SettingsRecord>(File.ReadAllText(file.FullName))
                ?.Theme;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static void SaveThemeId(string themeId)
    {
        var file = SettingsFile;
        file.Refresh();
        if (file.Directory is { Exists: false })
        {
            file.Directory.Create();
        }
        File.WriteAllText(
            file.FullName,
            JsonSerializer.Serialize(new SettingsRecord(themeId), SerializerOptions)
        );
    }
}
