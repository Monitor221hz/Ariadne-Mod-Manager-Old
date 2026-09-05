using Avalonia.Controls.DataGridDragDrop;
using Avalonia.Input;
using Daedalus.ModManager.GUI.ViewModels;

namespace Daedalus.ModManager.GUI.DragDrop;

public static class ModListDropRules
{
    public static bool IsLegal(
        IReadOnlyList<TreeNodeViewModel> dragged,
        TreeNodeViewModel? target,
        TreeNodeViewModel? targetParent,
        DataGridRowDropPosition position,
        DragDropEffects effect
    )
    {
        if (effect != DragDropEffects.Move)
        {
            return false;
        }
        foreach (var node in dragged)
        {
            var legal = node switch
            {
                ModEntryNodeViewModel => IsLegalModDrop(target, targetParent, position),
                GroupHeaderNodeViewModel => position != DataGridRowDropPosition.Inside
                    && targetParent is null,
                _ => false,
            };
            if (!legal)
            {
                return false;
            }
        }
        return true;
    }

    private static bool IsLegalModDrop(
        TreeNodeViewModel? target,
        TreeNodeViewModel? targetParent,
        DataGridRowDropPosition position
    )
    {
        return position switch
        {
            DataGridRowDropPosition.Inside => target is GroupHeaderNodeViewModel,
            _ => target is ModEntryNodeViewModel
                && targetParent is null or GroupHeaderNodeViewModel,
        };
    }
}
