using System.Globalization;
using Avalonia.Controls.DataGridHierarchical;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Daedalus.ModManager.GUI.ViewModels;

namespace Daedalus.ModManager.GUI.Converters;

public sealed class GroupBandBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var node = value is HierarchicalNode wrapper ? wrapper.Item : value;
        return node is GroupHeaderNodeViewModel group ? group.SeparatorBrush : Brushes.Transparent;
    }

    public object ConvertBack(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture
    ) => throw new NotSupportedException();
}
