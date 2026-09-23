using System.Globalization;
using Avalonia.Controls.DataGridHierarchical;
using Avalonia.Data.Converters;
using Daedalus.Contracts.Mods;
using Daedalus.ModManager.GUI.ViewModels;
using FluentIcons.Common;

namespace Daedalus.ModManager.GUI.Converters;

public sealed class NodeChevronIconConverter : IMultiValueConverter
{
    public object? Convert(
        IList<object?> values,
        Type targetType,
        object? parameter,
        CultureInfo culture
    )
    {
        if (values.Count != 2)
        {
            return null;
        }
        bool isChecked = values[1] is bool isCheckedValue && isCheckedValue;
        var node = values[0] is HierarchicalNode wrapper ? wrapper.Item : values[0];
        return (node, isChecked) switch
        {
            (GroupHeaderNodeViewModel, true) => Icon.ChevronDoubleDown,
            (GroupHeaderNodeViewModel, false) => Icon.ChevronDoubleRight,
            (ModEntryNodeViewModel, true) => Icon.ChevronDown,
            (ModEntryNodeViewModel, false) => Icon.ChevronRight,
            (DirectoryNodeViewModel, true) => Icon.FolderOpen,
            (DirectoryNodeViewModel, false) => Icon.Folder,
            (ArchiveLeafNodeViewModel, true) => Icon.FolderOpen,
            (ArchiveLeafNodeViewModel, false) => Icon.FolderZip,
            (DeployedRowViewModel { IsArchive: true }, true) => Icon.FolderOpen,
            (DeployedRowViewModel { IsArchive: true }, false) => Icon.FolderZip,
            (DeployedRowViewModel { IsDirectory: true }, true) => Icon.FolderOpen,
            (DeployedRowViewModel { IsDirectory: true }, false) => Icon.Folder,
            (DeployedRowViewModel, _) => Icon.Document,
            _ => null,
        };
    }
}
