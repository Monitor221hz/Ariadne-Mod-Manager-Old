using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Daedalus.Contracts.Mods;
using Daedalus.ModManager.GUI.ViewModels;
using Lucide.Avalonia;

namespace Daedalus.ModManager.GUI.Converters;

public sealed class NodeChevronGeometryConverter : IMultiValueConverter
{
    private static readonly Geometry GroupCollapsed = StreamGeometry.Parse(
        LucideIconKind.SquareChevronRight.GetGeometryData()
    );
    private static readonly Geometry GroupExpanded = StreamGeometry.Parse(
        LucideIconKind.SquareChevronDown.GetGeometryData()
    );
    private static readonly Geometry ModCollapsed = StreamGeometry.Parse(
        LucideIconKind.Package.GetGeometryData()
    );
    private static readonly Geometry ModExpanded = StreamGeometry.Parse(
        LucideIconKind.PackageOpen.GetGeometryData()
    );
    private static readonly Geometry DirectoryCollapsed = StreamGeometry.Parse(
        LucideIconKind.Folder.GetGeometryData()
    );
    private static readonly Geometry DirectoryExpanded = StreamGeometry.Parse(
        LucideIconKind.FolderOpen.GetGeometryData()
    );
    private static readonly Geometry FileIcon = StreamGeometry.Parse(
        LucideIconKind.File.GetGeometryData()
    );
    private static readonly Geometry ArchiveCollapsed = StreamGeometry.Parse(
        LucideIconKind.FolderArchive.GetGeometryData()
    );
    private static readonly Geometry ArchiveExpanded = StreamGeometry.Parse(
        LucideIconKind.FolderOpenDot.GetGeometryData()
    );

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
        return (values[0], isChecked) switch
        {
            (GroupHeaderNodeViewModel, true) => GroupExpanded,
            (GroupHeaderNodeViewModel, false) => GroupCollapsed,
            (ModEntryNodeViewModel, true) => ModExpanded,
            (ModEntryNodeViewModel, false) => ModCollapsed,
            (DirectoryNodeViewModel, true) => DirectoryExpanded,
            (DirectoryNodeViewModel, false) => DirectoryCollapsed,
            (FileLeafNodeViewModel { Kind: ModEntryKind.Archive }, true) => ArchiveExpanded,
            (FileLeafNodeViewModel { Kind: ModEntryKind.Archive }, false) => ArchiveCollapsed,
            (FileLeafNodeViewModel, _) => FileIcon,
            _ => null,
        };
    }
}
