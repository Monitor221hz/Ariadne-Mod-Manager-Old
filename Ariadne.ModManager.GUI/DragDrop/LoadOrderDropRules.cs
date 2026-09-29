using Avalonia.Controls.DataGridDragDrop;
using Avalonia.Input;
using Ariadne.ModManager.GUI.ViewModels;

namespace Ariadne.ModManager.GUI.DragDrop;

public static class LoadOrderDropRules
{
    public static bool IsLegal(DataGridRowDropPosition position, DragDropEffects effected) =>
        effected == DragDropEffects.Move
        && position is DataGridRowDropPosition.Before or DataGridRowDropPosition.After;

    public static void Reorder(
        IList<LoadOrderInfoViewModel> rows,
        IReadOnlyList<LoadOrderInfoViewModel> dragged,
        LoadOrderInfoViewModel target,
        DataGridRowDropPosition position
    )
    {
        var targetIndex = rows.IndexOf(target);
        if (targetIndex < 0 || dragged.Count == 0)
        {
            return;
        }
        var ordered = dragged.OrderBy(rows.IndexOf).ToList();
        foreach (var item in ordered)
        {
            rows.Remove(item);
        }
        var insertAt = rows.IndexOf(target);
        if (position == DataGridRowDropPosition.After)
        {
            insertAt++;
        }
        for (int i = 0; i < ordered.Count; i++)
        {
            rows.Insert(insertAt + i, ordered[i]);
        }
    }
}
