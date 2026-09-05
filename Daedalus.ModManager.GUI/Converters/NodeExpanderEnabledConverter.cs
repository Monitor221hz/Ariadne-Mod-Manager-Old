using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia.Controls.DataGridHierarchical;
using Avalonia.Data.Converters;
using Daedalus.ModManager.GUI.ViewModels;

namespace Daedalus.ModManager.GUI.Converters;

public sealed class NodeExpanderEnabledConverter : IMultiValueConverter
{
    public object? Convert(
        IList<object?> values,
        Type targetType,
        object? parameter,
        CultureInfo culture
    )
    {
        if (values.Count != 2 || values[0] is not bool expandable)
        {
            return false;
        }
        var node = values[1] is HierarchicalNode wrapper ? wrapper.Item : values[1];
        var hasChildren = node is TreeNodeViewModel viewModel && viewModel.Children.Any();
        return expandable && hasChildren;
    }
}
