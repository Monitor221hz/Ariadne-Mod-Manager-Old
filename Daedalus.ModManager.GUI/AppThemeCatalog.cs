using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.Json;

namespace Daedalus.ModManager.GUI;

public sealed class AppThemeCatalog
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public IReadOnlyList<LoadedAppTheme> Themes { get; }

    public AppThemeCatalog(Assembly assembly, DirectoryInfo? looseThemesDirectory = null)
    {
        Dictionary<string, LoadedAppTheme> themes = new(StringComparer.OrdinalIgnoreCase);

        foreach (var resourceName in assembly.GetManifestResourceNames())
        {
            if (!IsEmbeddedThemeResource(resourceName))
            {
                continue;
            }
            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream is null)
            {
                continue;
            }
            using var reader = new StreamReader(stream);
            if (TryParse(reader.ReadToEnd(), out var theme))
            {
                themes[theme.Info.Id] = theme;
            }
        }

        if (looseThemesDirectory is { Exists: true })
        {
            foreach (var file in looseThemesDirectory.EnumerateFiles("*.json"))
            {
                if (TryParse(File.ReadAllText(file.FullName), out var theme))
                {
                    themes[theme.Info.Id] = theme;
                }
            }
        }

        Themes = [.. themes.Values];
    }

    private static bool IsEmbeddedThemeResource(string name) =>
        name.Contains(".Embedded.", StringComparison.Ordinal)
        && name.EndsWith(".json", StringComparison.OrdinalIgnoreCase);

    private static bool TryParse(string json, [NotNullWhen(true)] out LoadedAppTheme? theme)
    {
        theme = null;
        try
        {
            var record = JsonSerializer.Deserialize<AppThemeRecord>(json, SerializerOptions);
            if (record is null)
            {
                return false;
            }
            theme = record.Map();
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}

public sealed record class LoadedAppTheme(AppThemeInfo Info, AppThemePalette Palette);
