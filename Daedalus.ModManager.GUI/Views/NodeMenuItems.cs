using Daedalus.ModManager.GUI.ViewModels;

namespace Daedalus.ModManager.GUI.Views;

public static class NodeMenuItems
{
    public static NodeMenuItem[] For(TreeNodeViewModel node) =>
        node switch
        {
            ModEntryNodeViewModel mod =>
            [
                new("Rename", mod.StartRenameCommand),
                new("Remove", mod.RemoveCommand),
            ],
            GroupHeaderNodeViewModel group =>
            [
                new("Rename", group.StartRenameCommand),
                new("Dissolve", group.DissolveCommand),
            ],
            _ => [],
        };
}
