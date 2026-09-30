using System.Collections.Generic;
using Ariadne.Extensions.IO;
using Ariadne.ModManager.GUI.ViewModels;
using Avalonia.Controls.DataGridDragDrop;
using Avalonia.Input;

namespace Ariadne.ModManager.GUI.DragDrop;

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

    public static IReadOnlyList<ModEntryNodeViewModel> ExecuteContentDrop(
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
                    FlattenIntoModRoot(node, mod);
                }
                else
                {
                    Move(node, DestinationDirectory(target));
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

    private static bool IsLegalContentDrop(
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
                && CanMoveInto(dragged, directory.DiskPath),
            ModEntryNodeViewModel mod => IsFlattenDrop(dragged, mod)
                ? CanFlatten(dragged, mod)
                : CanMoveInto(dragged, mod.Model.Directory.FullName),
            _ => false,
        };
    }

    private static bool CanMoveInto(ContentNodeViewModel dragged, string destinationDirectory)
    {
        var source = dragged.DiskPath;
        if (dragged is DirectoryNodeViewModel && IsSameOrDescendant(destinationDirectory, source))
        {
            return false;
        }
        if (
            string.Equals(
                Path.GetDirectoryName(source),
                destinationDirectory,
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            return false;
        }
        return !Path.Exists(Path.Join(destinationDirectory, dragged.DisplayName));
    }

    private static bool CanFlatten(ContentNodeViewModel dragged, ModEntryNodeViewModel mod)
    {
        var source = new DirectoryInfo(dragged.DiskPath);
        if (!source.Exists)
        {
            return false;
        }
        var root = mod.Model.Directory.FullName;
        return source
            .EnumerateFileSystemInfos()
            .All(child => !Path.Exists(Path.Join(root, child.Name)));
    }

    private static bool IsSameOrDescendant(string candidate, string directory)
    {
        var trimmed = directory.TrimEnd(Path.DirectorySeparatorChar);
        return string.Equals(candidate, trimmed, StringComparison.OrdinalIgnoreCase)
            || candidate.StartsWith(
                trimmed + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase
            );
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

    private static void Move(ContentNodeViewModel node, string destinationDirectory)
    {
        var destination = Path.Join(destinationDirectory, node.DisplayName);
        if (node is DirectoryNodeViewModel)
        {
            Directory.Move(node.DiskPath, destination);
        }
        else
        {
            File.Move(node.DiskPath, destination);
        }
    }

    private static void FlattenIntoModRoot(ContentNodeViewModel folder, ModEntryNodeViewModel mod)
    {
        var source = new DirectoryInfo(folder.DiskPath);
        var root = mod.Model.Directory.FullName;
        foreach (var child in source.EnumerateFileSystemInfos())
        {
            var destination = Path.Join(root, child.Name);
            if (child is DirectoryInfo directory)
            {
                directory.MergeTo(destination);
            }
            else
            {
                ((FileInfo)child).MoveTo(destination);
            }
        }
        source.Refresh();
        if (source.Exists && !source.EnumerateFileSystemInfos().Any())
        {
            source.Delete();
        }
    }
}
