using Avalonia.Controls;
using Avalonia.Controls.DataGridDragDrop;
using Avalonia.Controls.DataGridHierarchical;
using Avalonia.Input;
using Avalonia.VisualTree;
using Daedalus.ModManager.GUI.ViewModels;

namespace Daedalus.ModManager.GUI.DragDrop;

public sealed class ModListRowDropHandler(Func<bool> isSorted) : IDataGridRowDropHandler
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
        var valid = _reorder.Validate(args);
        if (valid && args.Session is not null)
        {
            args.Session.FeedbackCaption = $"Move {args.Items.Count} row(s).";
        }
        return valid;
    }

    public bool Execute(DataGridRowDropEventArgs args)
    {
        var scrollViewer = args.Grid.FindDescendantOfType<ScrollViewer>();
        var offset = scrollViewer?.Offset;
        var result = _reorder.Execute(args);
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
