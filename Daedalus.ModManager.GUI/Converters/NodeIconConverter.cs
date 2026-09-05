using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Controls.DataGridHierarchical;
using Avalonia.Data.Converters;
using Daedalus.Contracts.Mods;
using Daedalus.ModManager.GUI.ViewModels;
using FluentIcons.Common;

namespace Daedalus.ModManager.GUI.Converters;

public sealed class NodeIconConverter : IMultiValueConverter
{
    public object? Convert(
        IList<object?> values,
        Type targetType,
        object? parameter,
        CultureInfo culture
    )
    {
        if (values.Count != 2 || values[1] is not bool isChecked)
        {
            return null;
        }
        var node = values[0] is HierarchicalNode wrapper ? wrapper.Item : values[0];
        return (node, isChecked) switch
        {
            (GroupHeaderNodeViewModel, true) => Icon.ChevronDoubleDown,
            (GroupHeaderNodeViewModel, false) => Icon.ChevronDoubleRight,
            (ModEntryNodeViewModel, true) => Icon.ChevronDown,
            (ModEntryNodeViewModel, false) => Icon.ChevronRight,
            (DirectoryNodeViewModel, true) => Icon.FolderOpen,
            (DirectoryNodeViewModel, false) => Icon.Folder,
            (FileLeafNodeViewModel { Kind: ModEntryKind.Archive }, true) => Icon.FolderOpen,
            (FileLeafNodeViewModel { Kind: ModEntryKind.Archive }, false) => Icon.FolderZip,
            (FileLeafNodeViewModel, _) => Icon.Document,
            _ => null,
        };
    }
}
