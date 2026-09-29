using System.Globalization;
using Avalonia.Controls.DataGridHierarchical;
using Avalonia.Data.Converters;
using Ariadne.ModManager.GUI.ViewModels;

namespace Ariadne.ModManager.GUI.Converters;

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
            DeployedRowViewModel deployedRow => deployedRow.IsDirectory ? 20.0 : 16.0,
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
