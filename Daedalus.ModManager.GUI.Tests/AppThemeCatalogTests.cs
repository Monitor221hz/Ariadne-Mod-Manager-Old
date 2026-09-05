using Avalonia.Styling;
using Xunit;

namespace Daedalus.ModManager.GUI.Tests;

public class AppThemeCatalogTests : IDisposable
{
    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } =
            System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "DaedalusTests-" + Guid.NewGuid().ToString("N")
            );

        public TempDirectory() => Directory.CreateDirectory(Path);

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, true);
            }
        }
    }

    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    private static AppThemeCatalog Catalog(DirectoryInfo? loose = null) =>
        new(typeof(AppTheme).Assembly, loose);

    private DirectoryInfo LooseThemes =>
        Directory.CreateDirectory(System.IO.Path.Combine(_temp.Path, "Themes"));

    private static string LooseThemeJson(
        string id,
        string name,
        string variant,
        string baseColor
    ) =>
        $$"""
            {
              "Id": "{{id}}",
              "Name": "{{name}}",
              "Variant": "{{variant}}",
              "Base": "{{baseColor}}",
              "Mantle": "#FF16161E",
              "Darker": "#FF101014",
              "Elevated": "#FF202330",
              "Selection": "#40515C7E",
              "Hover": "#FF1C1D29",
              "ActiveList": "#FF29355A",
              "FgBase": "#FFA9B1D6",
              "FgMuted": "#FF787C99",
              "FgDimmed": "#FF545C7E",
              "FgBright": "#FFC0CAF5",
              "AccentPrimary": "#FF3D59A1",
              "AccentSoft": "#FF7DCFFF",
              "Border": "#FF101014",
              "GlassPanel": "#E616161E",
              "GlassBorder": "#1FA9B1D6",
              "ToolbarBorder": "#66101014",
              "CardShadow": "0 8 40 0 #88000000"
            }
            """;

    [Fact]
    public void LoadsEmbeddedThemes_FromAssembly()
    {
        var catalog = Catalog();

        Assert.True(catalog.Themes.Count >= 3);
        Assert.Contains(catalog.Themes, t => t.Info.Id == "tokyo-night");
        Assert.Contains(catalog.Themes, t => t.Info.Id == "catppuccin-mocha");
        Assert.Contains(catalog.Themes, t => t.Info.Id == "catppuccin-latte");
        Assert.DoesNotContain(catalog.Themes, t => t.Info.Id == "tokyo-day");
    }

    [Fact]
    public void EmbeddedTokyoNight_PaletteValuesFlowThrough()
    {
        var theme = Catalog().Themes.Single(t => t.Info.Id == "tokyo-night");

        Assert.Equal("Tokyo Night", theme.Info.Display);
        Assert.Equal(ThemeVariant.Dark, theme.Info.Variant);
        Assert.Equal("#FF1A1B26", theme.Palette.Base);
        Assert.Equal("#FF3D59A1", theme.Palette.AccentPrimary);
    }

    [Fact]
    public void MissingLooseDirectory_LoadsOnlyEmbedded()
    {
        var missing = new DirectoryInfo(System.IO.Path.Combine(_temp.Path, "nope"));

        var catalog = Catalog(missing);

        Assert.True(catalog.Themes.Count >= 3);
    }

    [Fact]
    public void LooseTheme_File_IsAdded()
    {
        File.WriteAllText(
            System.IO.Path.Combine(LooseThemes.FullName, "custom.json"),
            LooseThemeJson("custom", "Custom Theme", "Light", "#FF123456")
        );

        var catalog = Catalog(LooseThemes);

        var theme = Assert.Single(catalog.Themes, t => t.Info.Id == "custom");
        Assert.Equal("Custom Theme", theme.Info.Display);
        Assert.Equal(ThemeVariant.Light, theme.Info.Variant);
        Assert.Equal("#FF123456", theme.Palette.Base);
    }

    [Fact]
    public void LooseTheme_OverridesEmbeddedById()
    {
        File.WriteAllText(
            System.IO.Path.Combine(LooseThemes.FullName, "tokyo-night.json"),
            LooseThemeJson("tokyo-night", "Night Override", "Dark", "#FF000000")
        );

        var catalog = Catalog(LooseThemes);

        var theme = Assert.Single(catalog.Themes, t => t.Info.Id == "tokyo-night");
        Assert.Equal("Night Override", theme.Info.Display);
        Assert.Equal("#FF000000", theme.Palette.Base);
    }

    [Fact]
    public void BrokenLooseTheme_IsSkipped()
    {
        File.WriteAllText(System.IO.Path.Combine(LooseThemes.FullName, "broken.json"), "{ nope");
        File.WriteAllText(
            System.IO.Path.Combine(LooseThemes.FullName, "missing-fields.json"),
            """{ "Id": "incomplete" }"""
        );
        int embedded = Catalog().Themes.Count;

        var catalog = Catalog(LooseThemes);

        Assert.Equal(embedded, catalog.Themes.Count);
        Assert.DoesNotContain(catalog.Themes, t => t.Info.Id == "incomplete");
    }

    [Theory]
    [InlineData("Dark", false)]
    [InlineData("dark", false)]
    [InlineData("Light", true)]
    [InlineData("anything-else", false)]
    public void VariantParsing_DefaultsNonLightToDark(string variant, bool expectLight)
    {
        var record = new AppThemeRecord(
            "id",
            "n",
            variant,
            "#FF1A1B26",
            "#FF16161E",
            "#FF101014",
            "#FF202330",
            "#40515C7E",
            "#FF1C1D29",
            "#FF29355A",
            "#FFA9B1D6",
            "#FF787C99",
            "#FF545C7E",
            "#FFC0CAF5",
            "#FF3D59A1",
            "#FF7DCFFF",
            "#FF101014",
            "#E616161E",
            "#1FA9B1D6",
            "#66101014",
            "0 8 40 0 #88000000"
        );

        var expected = expectLight ? ThemeVariant.Light : ThemeVariant.Dark;
        Assert.Equal(expected, record.Map().Info.Variant);
    }
}
