using System.Diagnostics;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using Ariadne.ModManager.GUI.ViewModels;
using Avalonia.Controls;
using Avalonia.Controls.DataGridDragDrop;
using Avalonia.Controls.DataGridHierarchical;
using Avalonia.Input;
using Avalonia.VisualTree;
using ReactiveUI;

namespace Ariadne.ModManager.GUI.DragDrop;

public sealed class ModListRowDropHandler(
    Func<bool> isSorted,
    Func<IList<TreeNodeViewModel>> getRoots,
    Func<HierarchicalModel<TreeNodeViewModel>?> getModel
) : IDataGridRowDropHandler
{
    private readonly DataGridHierarchicalRowReorderHandler _reorder = new();

    public bool Validate(DataGridRowDropEventArgs args)
    {
        var dragged = args
            .Items.OfType<HierarchicalNode>()
            .Select(node => node.Item as TreeNodeViewModel)
            .ToList();
        var targetNode = args.TargetItem as HierarchicalNode;
        var target = targetNode?.Item as TreeNodeViewModel;
        var targetParent = targetNode?.Parent?.Item as TreeNodeViewModel;
        if (
            dragged.Count == 0
            || dragged.Any(node => node is null)
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
        if (dragged.All(node => node is ContentNodeViewModel))
        {
            args.EffectiveEffect = DragDropEffects.Move;
            if (args.Session is not null)
            {
                args.Session.FeedbackCaption = ContentDropCaption(dragged!, target);
            }
            return true;
        }
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

    private static string ContentDropCaption(
        IReadOnlyList<TreeNodeViewModel> dragged,
        TreeNodeViewModel? target
    )
    {
        if (
            dragged.Count == 1
            && dragged[0] is ContentNodeViewModel content
            && ModListDropRules.IsFlattenDrop(content, target)
        )
        {
            return $"Move contents of {content.DisplayName} to top level of {target?.DisplayName}.";
        }
        return $"Move into {target?.DisplayName ?? "target"}.";
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
        if (node != null)
        {
            model!.Refresh(node);
        }
        return true;
    }

    private bool ExecuteContentDrop(DataGridRowDropEventArgs args)
    {
        var dragged = args
            .Items.OfType<HierarchicalNode>()
            .Select(node => node.Item)
            .OfType<TreeNodeViewModel>()
            .ToList();
        var target = (args.TargetItem as HierarchicalNode)?.Item as TreeNodeViewModel;
        if (dragged.Count == 0 || target is null)
        {
            return false;
        }
        var affected = ModListDropRules.ExecuteContentDrop(dragged, target);
        if (affected.Count == 0)
        {
            return false;
        }
        target.IsExpanded = true;
        foreach (var entry in affected)
        {
            _ = ReloadAndRefreshAsync(entry);
        }
        return true;
    }

    private async Task ReloadAndRefreshAsync(ModEntryNodeViewModel entry)
    {
        try
        {
            var arrival = entry
                .WhenAnyValue(x => x.ContentTree)
                .Skip(1)
                .WhereNotNull()
                .FirstAsync()
                .ToTask();
            entry.ReloadContent();
            await arrival;
            getModel()?.Refresh();
            foreach (var group in getRoots().OfType<GroupHeaderNodeViewModel>())
            {
                group.RefreshSizeText();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }
    }

    public bool Execute(DataGridRowDropEventArgs args)
    {
        var dragged = args
            .Items.OfType<HierarchicalNode>()
            .Select(node => node.Item as TreeNodeViewModel)
            .ToList();
        if (dragged.Count > 0 && dragged.All(node => node is ContentNodeViewModel))
        {
            return ExecuteContentDrop(args);
        }
        var scrollViewer = args.Grid.FindDescendantOfType<ScrollViewer>();
        var offset = scrollViewer?.Offset;
        var result = IsGroupInsideDrop(args)
            ? ExecuteFallback(args)
            : _reorder.Execute(args) || ExecuteFallback(args);
        if (result && scrollViewer is not null && offset.HasValue)
        {
            var restoreTo = offset.Value;
            Avalonia.Threading.Dispatcher.UIThread.Post(
                () => scrollViewer.Offset = restoreTo,
                Avalonia.Threading.DispatcherPriority.Loaded
            );
        }
        return result;
    }
}
