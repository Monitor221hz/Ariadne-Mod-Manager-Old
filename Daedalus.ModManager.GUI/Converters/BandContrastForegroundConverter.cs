using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Daedalus.ModManager.GUI.Converters;

public sealed class BandContrastForegroundConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not SolidColorBrush brush)
        {
            return null;
        }
        var color = brush.Color;
        var luminance = 0.299 * color.R + 0.587 * color.G + 0.114 * color.B;
        return luminance > 150 ? Brushes.Black : Brushes.White;
    }

    public object ConvertBack(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture
    ) => throw new NotSupportedException();
}
