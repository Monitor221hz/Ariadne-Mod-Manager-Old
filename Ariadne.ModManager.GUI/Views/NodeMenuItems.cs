using Ariadne.Contracts.Games;
using Ariadne.ModManager.GUI.ViewModels;

namespace Ariadne.ModManager.GUI.Views;

public static class NodeMenuItems
{
    public static NodeMenuItem[] For(
        TreeNodeViewModel node,
        IReadOnlyList<IGamePath>? installTargets = null
    ) =>
        node switch
        {
            FileLeafNodeViewModel => [new("Open", node.OpenCommand)],
            ModEntryNodeViewModel mod =>
            [
                new("Rename", mod.StartRenameCommand),
                new("Remove", mod.RemoveCommand),
                .. TargetItems(mod, installTargets),
            ],
            GroupHeaderNodeViewModel group =>
            [
                new("Rename", group.StartRenameCommand),
                new("Dissolve", group.DissolveCommand),
            ],
            _ => [],
        };

    private static NodeMenuItem[] TargetItems(
        ModEntryNodeViewModel mod,
        IReadOnlyList<IGamePath>? installTargets
    ) =>
        installTargets is { Count: > 0 }
            ?
            [
                new NodeMenuItem(
                    "Target...",
                    Children: installTargets
                        .Select(target => new NodeMenuItem(
                            target.Key,
                            mod.SetTargetCommand,
                            target
                        ))
                        .ToList()
                ),
            ]
            : [];
}
