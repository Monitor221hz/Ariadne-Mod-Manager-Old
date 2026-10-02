using System.Collections.Generic;
using Ariadne.Contracts.ModManager;
using Ariadne.ModManager.GUI.ViewModels;
using Avalonia.Controls.DataGridDragDrop;
using Avalonia.Input;

namespace Ariadne.ModManager.GUI.DragDrop;

public sealed class ModListDropRules(IContentMoveService moves)
{
    public bool IsLegal(
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
                ContentNodeViewModel content => IsLegalContentDrop(content, target, position),
                _ => false,
            };
            if (!legal)
            {
                return false;
            }
        }
        return true;
    }

    public static void MoveIntoGroup(
        IList<TreeNodeViewModel> roots,
        GroupHeaderNodeViewModel targetGroup,
        IReadOnlyList<TreeNodeViewModel> dragged
    )
    {
        foreach (var item in dragged)
        {
            if (item is not ModEntryNodeViewModel)
            {
                continue;
            }
            roots.Remove(item);
            foreach (var group in roots)
            {
                if (group is GroupHeaderNodeViewModel sourceGroup)
                {
                    sourceGroup.ObservableChildren.Remove(item);
                }
            }
            targetGroup.ObservableChildren.Add(item);
        }
    }

    public static bool IsFlattenDrop(ContentNodeViewModel dragged, TreeNodeViewModel? target) =>
        dragged is DirectoryNodeViewModel
        && target is ModEntryNodeViewModel mod
        && ReferenceEquals(dragged.Owner, mod);

    public IReadOnlyList<ModEntryNodeViewModel> ExecuteContentDrop(
        IReadOnlyList<TreeNodeViewModel> dragged,
        TreeNodeViewModel target
    )
    {
        var affected = new HashSet<ModEntryNodeViewModel>();
        foreach (var node in dragged.OfType<ContentNodeViewModel>())
        {
            try
            {
                if (target is ModEntryNodeViewModel mod && IsFlattenDrop(node, mod))
                {
                    moves.FlattenIntoModRoot(node.DiskPath, mod.Model.Directory.FullName);
                }
                else
                {
                    moves.MoveInto(
                        node.DiskPath,
                        node is DirectoryNodeViewModel,
                        node.DisplayName,
                        DestinationDirectory(target)
                    );
                }
                affected.Add(node.Owner);
                if (TargetOwner(target) is { } targetOwner)
                {
                    affected.Add(targetOwner);
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
        return affected.ToList();
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

    private bool IsLegalContentDrop(
        ContentNodeViewModel dragged,
        TreeNodeViewModel? target,
        DataGridRowDropPosition position
    )
    {
        if (position != DataGridRowDropPosition.Inside || !dragged.IsDiskBacked)
        {
            return false;
        }
        return target switch
        {
            DirectoryNodeViewModel directory => directory.IsDiskBacked
                && moves.CanMoveInto(
                    dragged.DiskPath,
                    dragged is DirectoryNodeViewModel,
                    dragged.DisplayName,
                    directory.DiskPath
                ),
            ModEntryNodeViewModel mod => IsFlattenDrop(dragged, mod)
                ? moves.CanFlattenIntoModRoot(dragged.DiskPath, mod.Model.Directory.FullName)
                : moves.CanMoveInto(
                    dragged.DiskPath,
                    dragged is DirectoryNodeViewModel,
                    dragged.DisplayName,
                    mod.Model.Directory.FullName
                ),
            _ => false,
        };
    }

    private static string DestinationDirectory(TreeNodeViewModel target) =>
        target switch
        {
            DirectoryNodeViewModel directory => directory.DiskPath,
            ModEntryNodeViewModel mod => mod.Model.Directory.FullName,
            _ => throw new ArgumentException("Unsupported drop target.", nameof(target)),
        };

    private static ModEntryNodeViewModel? TargetOwner(TreeNodeViewModel target) =>
        target switch
        {
            ModEntryNodeViewModel mod => mod,
            DirectoryNodeViewModel directory => directory.Owner,
            _ => null,
        };
}
