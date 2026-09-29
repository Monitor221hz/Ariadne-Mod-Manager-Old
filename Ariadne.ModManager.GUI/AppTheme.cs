using Avalonia.Media;
using Avalonia.Styling;

namespace Ariadne.ModManager.GUI;

public sealed record class AppThemeInfo(string Id, string Display, ThemeVariant Variant);

public static class AppTheme
{
    public const string DefaultThemeId = "tokyo-night";

    private static IReadOnlyList<AppThemeInfo> _all = [];
    private static Dictionary<string, AppThemePalette> _palettes = [];

    public static IReadOnlyList<AppThemeInfo> All => _all;

    public static AppThemeInfo Current { get; private set; } =
        new(DefaultThemeId, "Tokyo Night", ThemeVariant.Dark);

    public static void Initialize()
    {
        var catalog = new AppThemeCatalog(
            typeof(AppTheme).Assembly,
            new DirectoryInfo(Path.Join(AppContext.BaseDirectory, "Themes"))
        );
        _all = [.. catalog.Themes.Select(t => t.Info)];
        _palettes = catalog.Themes.ToDictionary(t => t.Info.Id, t => t.Palette);
        if (_all.Count == 0)
        {
            throw new InvalidOperationException(
                "No themes found. Embedded theme resources missing."
            );
        }
    }

    public static AppThemeInfo LoadSaved()
    {
        var saved = AppSettings.LoadThemeId();
        var theme =
            _all.FirstOrDefault(t => t.Id == saved)
            ?? _all.FirstOrDefault(t => t.Id == DefaultThemeId)
            ?? _all[0];
        Current = theme;
        return theme;
    }

    public static void Apply(string id)
    {
        var target = _all.FirstOrDefault(t => t.Id == id);
        if (target is null || !_palettes.TryGetValue(target.Id, out var palette))
        {
            return;
        }
        Current = target;

        var app = Avalonia.Application.Current;
        if (app != null)
        {
            AssignPalette(app.Resources, palette);
            app.RequestedThemeVariant = target.Variant;
        }

        AppSettings.SaveThemeId(target.Id);
    }

    private static void AssignPalette(
        Avalonia.Controls.IResourceDictionary resources,
        AppThemePalette palette
    )
    {
        static Color C(string hex) => Color.Parse(hex);

        var colors = new Dictionary<string, Color>
        {
            ["BgBase"] = C(palette.Base),
            ["BgMantle"] = C(palette.Mantle),
            ["BgDarker"] = C(palette.Darker),
            ["BgElevated"] = C(palette.Elevated),
            ["BgSelection"] = C(palette.Selection),
            ["BgHover"] = C(palette.Hover),
            ["BgActiveList"] = C(palette.ActiveList),
            ["FgBase"] = C(palette.FgBase),
            ["FgMuted"] = C(palette.FgMuted),
            ["FgDimmed"] = C(palette.FgDimmed),
            ["FgBright"] = C(palette.FgBright),
            ["AccentPrimary"] = C(palette.AccentPrimary),
            ["AccentSoft"] = C(palette.AccentSoft),
            ["BorderColor"] = C(palette.Border),
            ["GlassPanel"] = C(palette.GlassPanel),
        };
        foreach (var (key, color) in colors)
        {
            resources[key] = color;
        }

        var brushes = new Dictionary<string, IBrush>
        {
            ["AriadnePageBrush"] = new SolidColorBrush(C(palette.Mantle)),
            ["AriadneSurfaceBrush"] = new SolidColorBrush(C(palette.Mantle)),
            ["AriadneToolbarBackground"] = new SolidColorBrush(C(palette.Darker)),
            ["AriadneToolbarBorder"] = new SolidColorBrush(C(palette.Border)),
            ["AriadneToolbarBorderBrush"] = new SolidColorBrush(C(palette.ToolbarBorder)),
            ["FgBaseBrush"] = new SolidColorBrush(C(palette.FgBase)),
            ["FgMutedBrush"] = new SolidColorBrush(C(palette.FgMuted)),
            ["FgDimmedBrush"] = new SolidColorBrush(C(palette.FgDimmed)),
            ["FgBrightBrush"] = new SolidColorBrush(C(palette.FgBright)),
            ["AccentBrush"] = new SolidColorBrush(C(palette.AccentPrimary)),
            ["AccentSoftBrush"] = new SolidColorBrush(C(palette.AccentSoft)),
            ["AccentPrimaryFaintBrush"] = new SolidColorBrush(C(palette.AccentPrimary))
            {
                Opacity = 0.12,
            },
            ["SelectionBrush"] = new SolidColorBrush(C(palette.Selection)),
            ["HoverBrush"] = new SolidColorBrush(C(palette.Hover)),
            ["ActiveListBrush"] = new SolidColorBrush(C(palette.ActiveList)),
            ["BorderActiveBrush"] = new SolidColorBrush(C(palette.Border)),
            ["GlassPanelBrush"] = new SolidColorBrush(C(palette.GlassPanel)),
            ["GlassBorderBrush"] = new SolidColorBrush(C(palette.GlassBorder)),
            ["MenuFlyoutPresenterBackground"] = new SolidColorBrush(C(palette.GlassPanel)),
            ["MenuFlyoutPresenterBorderBrush"] = new SolidColorBrush(C(palette.Border)),
            ["FlyoutPresenterBackground"] = new SolidColorBrush(C(palette.GlassPanel)),
            ["FlyoutPresenterBorderBrush"] = new SolidColorBrush(C(palette.Border)),
            ["MenuFlyoutItemBackgroundPointerOver"] = new SolidColorBrush(C(palette.Hover)),
            ["MenuFlyoutItemSelectedGlyphColor"] = new SolidColorBrush(C(palette.AccentPrimary)),
        };
        foreach (var (key, brush) in brushes)
        {
            resources[key] = brush;
        }

        resources["GlassCardShadow"] = BoxShadows.Parse(palette.CardShadow);
    }
}
