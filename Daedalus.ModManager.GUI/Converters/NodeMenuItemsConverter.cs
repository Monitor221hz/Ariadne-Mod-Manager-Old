using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;
using Daedalus.ModManager.GUI.ViewModels;

namespace Daedalus.ModManager.GUI.Converters;

public sealed class NodeMenuItemsConverter : IValueConverter
{
    public object? Convert(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture
    ) =>
        value switch
        {
            ModEntryNodeViewModel mod => new NodeMenuItem[]
            {
                new("Rename", mod.StartRenameCommand),
                new("Remove", mod.RemoveCommand),
            },
            GroupHeaderNodeViewModel group => new NodeMenuItem[]
            {
                new("Rename", group.StartRenameCommand),
                new("Dissolve", group.DissolveCommand),
            },
            _ => Array.Empty<NodeMenuItem>(),
        };

    public object ConvertBack(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture
    ) => throw new NotSupportedException();
}
