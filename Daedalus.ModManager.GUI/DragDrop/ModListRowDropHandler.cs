using Avalonia.Controls;
using Avalonia.Controls.DataGridDragDrop;
using Avalonia.Controls.DataGridHierarchical;
using Avalonia.Input;
using Avalonia.VisualTree;
using Daedalus.ModManager.GUI.ViewModels;

namespace Daedalus.ModManager.GUI.DragDrop;

public sealed class ModListRowDropHandler(
    Func<bool> isSorted,
    Func<IList<TreeNodeViewModel>> getRoots,
    Func<HierarchicalModel<TreeNodeViewModel>?> getModel
) : IDataGridRowDropHandler
{
    private readonly DataGridHierarchicalRowReorderHandler _reorder = new();

    public bool Validate(DataGridRowDropEventArgs args)
    {
        if (isSorted())
        {
            args.EffectiveEffect = DragDropEffects.None;
            if (args.Session is not null)
            {
                args.Session.FeedbackCaption =
                    "Reordering is disabled while a column sort is active.";
            }
            return false;
        }
        var dragged = args
            .Items.OfType<HierarchicalNode>()
            .Select(node => node.Item as TreeNodeViewModel)
            .ToList();
        var targetNode = args.TargetItem as HierarchicalNode;
        var target = targetNode?.Item as TreeNodeViewModel;
        var targetParent = targetNode?.Parent?.Item as TreeNodeViewModel;
        if (
            dragged.Any(node => node is null)
            || !ModListDropRules.IsLegal(
                dragged!,
                target,
                targetParent,
                args.Position,
                args.RequestedEffect
            )
        )
        {
            args.EffectiveEffect = DragDropEffects.None;
            if (args.Session is not null)
            {
                args.Session.FeedbackCaption = "Drop here is not allowed.";
            }
            return false;
        }
        if (IsGroupInsideDrop(args))
        {
            args.EffectiveEffect = DragDropEffects.Move;
            if (args.Session is not null)
            {
                args.Session.FeedbackCaption =
                    $"Move into {GetTargetGroup(args)?.DisplayName ?? "group"}.";
            }
            return true;
        }
        var valid = _reorder.Validate(args);
        if (valid && args.Session is not null)
        {
            args.Session.FeedbackCaption = $"Move {args.Items.Count} row(s).";
        }
        return valid;
    }

    private static bool IsGroupInsideDrop(DataGridRowDropEventArgs args) =>
        args.Position == DataGridRowDropPosition.Inside
        && args.TargetItem is HierarchicalNode { Item: GroupHeaderNodeViewModel };

    private static GroupHeaderNodeViewModel? GetTargetGroup(DataGridRowDropEventArgs args) =>
        args.TargetItem is HierarchicalNode { Item: GroupHeaderNodeViewModel group } ? group : null;

    private bool ExecuteFallback(DataGridRowDropEventArgs args)
    {
        if (
            args.Position != DataGridRowDropPosition.Inside
            || args.TargetItem
                is not HierarchicalNode { Item: GroupHeaderNodeViewModel targetGroup }
        )
        {
            return false;
        }
        var items = args
            .Items.OfType<HierarchicalNode>()
            .Select(draggedNode => draggedNode.Item as TreeNodeViewModel)
            .Where(item => item != null)
            .ToList()!;
        ModListDropRules.MoveIntoGroup(getRoots(), targetGroup, items);
        targetGroup.IsExpanded = true;
        var model = getModel();
        var node = model?.FindNode(targetGroup);
        if (node is { } typed)
        {
            model!.Refresh(typed);
        }
        return true;
    }

    public bool Execute(DataGridRowDropEventArgs args)
    {
        var scrollViewer = args.Grid.FindDescendantOfType<ScrollViewer>();
        var offset = scrollViewer?.Offset;
        var result = IsGroupInsideDrop(args)
            ? ExecuteFallback(args)
            : _reorder.Execute(args) || ExecuteFallback(args);
        if (result && scrollViewer is not null && offset is { } restoreTo)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(
                () => scrollViewer.Offset = restoreTo,
                Avalonia.Threading.DispatcherPriority.Loaded
            );
        }
        return result;
    }
}
