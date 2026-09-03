using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Models;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls.Selection;
using Avalonia.Input;

namespace Daedalus.ModManager.GUI.ViewModels;

public sealed class ModListGridSource : ITreeDataGridSource<TreeNodeViewModel>, IDisposable
{
    private readonly HierarchicalTreeDataGridSource<TreeNodeViewModel> _inner;

    public ModListGridSource(HierarchicalTreeDataGridSource<TreeNodeViewModel> inner)
    {
        _inner = inner;
    }

    public ColumnList<TreeNodeViewModel> Columns => _inner.Columns;

    public event EventHandler<TreeDataGridRowModelEventArgs>? RowCollapsed
    {
        add => _inner.RowCollapsed += value;
        remove => _inner.RowCollapsed -= value;
    }

    public event PropertyChangedEventHandler? PropertyChanged
    {
        add => _inner.PropertyChanged += value;
        remove => _inner.PropertyChanged -= value;
    }

    public event Action? Sorted
    {
        add => _inner.Sorted += value;
        remove => _inner.Sorted -= value;
    }

    public ITreeDataGridSelection? Selection
    {
        get => _inner.Selection;
        set => _inner.Selection = value;
    }

    IColumns ITreeDataGridSource.Columns => _inner.Columns;
    public IRows Rows => _inner.Rows;
    public bool IsHierarchical => _inner.IsHierarchical;
    public bool IsSorted => _inner.IsSorted;

    IEnumerable<TreeNodeViewModel> ITreeDataGridSource<TreeNodeViewModel>.Items
    {
        get => _inner.Items;
        set => _inner.Items = value;
    }

    IEnumerable<object> ITreeDataGridSource.Items => ((ITreeDataGridSource)_inner).Items;

    public void Expand(IndexPath index) => _inner.Expand(index);

    public void Collapse(IndexPath index) => _inner.Collapse(index);

    public bool SortBy(IColumn column, ListSortDirection direction) =>
        _inner.SortBy(column, direction);

    public IEnumerable<object>? GetModelChildren(object model) =>
        ((ITreeDataGridSource)_inner).GetModelChildren(model);

    private bool TryGetModel(IndexPath index, [NotNullWhen(true)] out TreeNodeViewModel? model) =>
        _inner.TryGetModelAt(index, out model);

    void ITreeDataGridSource.DragDropRows(
        ITreeDataGridSource source,
        IEnumerable<IndexPath> indexes,
        IndexPath targetIndex,
        TreeDataGridRowDropPosition position,
        DragDropEffects effects
    )
    {
        var dragged = indexes
            .Select(index => TryGetModel(index, out var model) ? model : null)
            .ToList();
        var hasTarget = TryGetModel(targetIndex, out var target);
        if (dragged.Any(node => node is null) || !hasTarget)
        {
            return;
        }
        if (!IsLegalDrop(dragged!, targetIndex, position, effects))
        {
            return;
        }
        ((ITreeDataGridSource)_inner).DragDropRows(_inner, indexes, targetIndex, position, effects);
    }

    private bool IsLegalDrop(
        List<TreeNodeViewModel> dragged,
        IndexPath targetIndex,
        TreeDataGridRowDropPosition position,
        DragDropEffects effects
    )
    {
        if (effects != DragDropEffects.Move)
        {
            return false;
        }
        TreeNodeViewModel? parentModel =
            targetIndex.Count <= 1 ? null
            : TryGetModel(targetIndex[..^1], out var parent) ? parent
            : null;
        _ = TryGetModel(targetIndex, out var target);

        foreach (var node in dragged)
        {
            var legal = node switch
            {
                ModEntryNodeViewModel => IsLegalModDrop(target!, parentModel, position),
                GroupHeaderNodeViewModel => position != TreeDataGridRowDropPosition.Inside
                    && targetIndex.Count == 1,
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
        TreeNodeViewModel target,
        TreeNodeViewModel? parentModel,
        TreeDataGridRowDropPosition position
    )
    {
        return position switch
        {
            TreeDataGridRowDropPosition.Inside => target is GroupHeaderNodeViewModel,
            _ => target is ModEntryNodeViewModel && parentModel is null or GroupHeaderNodeViewModel,
        };
    }

    public void Dispose() => _inner.Dispose();
}
