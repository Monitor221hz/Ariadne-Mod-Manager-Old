using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Daedalus.ModManager.GUI.ViewModels;

namespace Daedalus.ModManager.GUI.Converters;

public sealed class GroupBandBrushConverter : IValueConverter
{
    public object? Convert(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture
    ) => value is GroupHeaderNodeViewModel group ? group.SeparatorBrush : Brushes.Transparent;

    public object ConvertBack(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture
    ) => throw new NotSupportedException();
}
