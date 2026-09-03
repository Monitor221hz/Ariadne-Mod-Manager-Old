using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Daedalus.Contracts.Mods;
using Daedalus.ModManager.GUI.ViewModels;

namespace Daedalus.ModManager.GUI.Converters;

public sealed class NodeChevronSizeConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value switch
        {
            GroupHeaderNodeViewModel => 20.0,
            ModEntryNodeViewModel => 18.0,
            DirectoryNodeViewModel => 16.0,
            FileLeafNodeViewModel { Kind: ModEntryKind.Archive } => 16.0,
            FileLeafNodeViewModel => 14.0,
            _ => 16.0,
        };

    public object ConvertBack(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture
    ) => throw new NotSupportedException();
}
