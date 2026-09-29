using Avalonia.Controls;
using Avalonia.Controls.DataGridDragDrop;
using Avalonia.Input;
using Avalonia.VisualTree;
using Ariadne.ModManager.GUI.ViewModels;

namespace Ariadne.ModManager.GUI.DragDrop;

public sealed class LoadOrderRowDropHandler(Func<IList<LoadOrderInfoViewModel>> getRows)
    : IDataGridRowDropHandler
{
    private (IReadOnlyList<LoadOrderInfoViewModel> Dragged, LoadOrderInfoViewModel? Target) Unpack(
        DataGridRowDropEventArgs args
    )
    {
        var dragged = args.Items.OfType<LoadOrderInfoViewModel>().ToList();
        var target = args.TargetItem as LoadOrderInfoViewModel;
        return (dragged, target);
    }

    public bool Validate(DataGridRowDropEventArgs args)
    {
        var (dragged, target) = Unpack(args);
        var rows = getRows();
        var legal =
            dragged.Count > 0
            && dragged.Count == args.Items.Count
            && target is not null
            && rows.Contains(target)
            && dragged.All(rows.Contains)
            && LoadOrderDropRules.IsLegal(args.Position, args.RequestedEffect);
        if (!legal)
        {
            args.EffectiveEffect = DragDropEffects.None;
            return false;
        }
        args.EffectiveEffect = DragDropEffects.Move;
        if (args.Session is not null)
        {
            args.Session.FeedbackCaption = $"Move {dragged.Count} plugin(s).";
        }
        return true;
    }

    public bool Execute(DataGridRowDropEventArgs args)
    {
        var (dragged, target) = Unpack(args);
        if (target is null || dragged.Count == 0)
        {
            return false;
        }
        var scrollViewer = args.Grid.FindDescendantOfType<ScrollViewer>();
        var offset = scrollViewer?.Offset;
        LoadOrderDropRules.Reorder(getRows(), dragged, target, args.Position);
        if (scrollViewer is not null && offset.HasValue)
        {
            var restoreTo = offset.Value;
            Avalonia.Threading.Dispatcher.UIThread.Post(
                () => scrollViewer.Offset = restoreTo,
                Avalonia.Threading.DispatcherPriority.Loaded
            );
        }
        return true;
    }
}
