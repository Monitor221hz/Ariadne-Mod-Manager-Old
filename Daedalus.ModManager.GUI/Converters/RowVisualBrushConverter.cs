using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.DataGridHierarchical;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Daedalus.ModManager.GUI.ViewModels;

namespace Daedalus.ModManager.GUI.Converters;

public sealed class RowVisualBrushConverter : IMultiValueConverter
{
    private static readonly IBrush WinFallback = new SolidColorBrush(Color.Parse("#33A6E3A1"));
    private static readonly IBrush LoseFallback = new SolidColorBrush(Color.Parse("#33F38BA8"));

    public object? Convert(
        IList<object?> values,
        Type targetType,
        object? parameter,
        CultureInfo culture
    )
    {
        if (values.Count > 1 && values[1] is SelectedModVerdict verdict)
        {
            return VerdictBrush(verdict);
        }
        var item = values.Count > 0 ? values[0] : null;
        if (item is HierarchicalNode node)
        {
            item = node.Item;
        }
        return item is GroupHeaderNodeViewModel group ? group.SeparatorBrush : Brushes.Transparent;
    }

    private static IBrush VerdictBrush(SelectedModVerdict verdict)
    {
        var key =
            verdict == SelectedModVerdict.LosesToSelected
                ? "ConflictWinBrush"
                : "ConflictLoseBrush";
        var app = Application.Current;
        if (app != null && app.TryFindResource(key, out var resource) && resource is IBrush brush)
        {
            return brush;
        }
        return verdict == SelectedModVerdict.LosesToSelected ? WinFallback : LoseFallback;
    }
}
