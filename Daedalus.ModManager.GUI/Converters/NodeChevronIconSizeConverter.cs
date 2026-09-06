using System.Globalization;
using Avalonia.Controls.DataGridHierarchical;
using Avalonia.Data.Converters;
using Daedalus.Contracts.Mods;
using Daedalus.ModManager.GUI.ViewModels;

namespace Daedalus.ModManager.GUI.Converters;

public sealed class NodeChevronIconSizeConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var node = value is HierarchicalNode wrapper ? wrapper.Item : value;
        return node switch
        {
            GroupHeaderNodeViewModel => 16.0,
            ModEntryNodeViewModel => 16.0,
            DirectoryNodeViewModel => 20.0,
            _ => 20.0,
        };
    }

    public object ConvertBack(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture
    ) => throw new NotSupportedException();
}
