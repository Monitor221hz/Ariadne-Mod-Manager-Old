using Avalonia.Styling;

namespace Daedalus.ModManager.GUI;

public sealed record class AppThemePalette(
    string Base,
    string Mantle,
    string Darker,
    string Elevated,
    string Selection,
    string Hover,
    string ActiveList,
    string FgBase,
    string FgMuted,
    string FgDimmed,
    string FgBright,
    string AccentPrimary,
    string AccentSoft,
    string Border,
    string GlassPanel,
    string GlassBorder,
    string ToolbarBorder,
    string CardShadow
);

public sealed record class AppThemeRecord(
    string Id,
    string Name,
    string Variant,
    string Base,
    string Mantle,
    string Darker,
    string Elevated,
    string Selection,
    string Hover,
    string ActiveList,
    string FgBase,
    string FgMuted,
    string FgDimmed,
    string FgBright,
    string AccentPrimary,
    string AccentSoft,
    string Border,
    string GlassPanel,
    string GlassBorder,
    string ToolbarBorder,
    string CardShadow
)
{
    public LoadedAppTheme Map() =>
        new(
            new AppThemeInfo(
                Id,
                Name,
                Variant.Equals("Light", StringComparison.OrdinalIgnoreCase)
                    ? ThemeVariant.Light
                    : ThemeVariant.Dark
            ),
            new AppThemePalette(
                Base,
                Mantle,
                Darker,
                Elevated,
                Selection,
                Hover,
                ActiveList,
                FgBase,
                FgMuted,
                FgDimmed,
                FgBright,
                AccentPrimary,
                AccentSoft,
                Border,
                GlassPanel,
                GlassBorder,
                ToolbarBorder,
                CardShadow
            )
        );
}
