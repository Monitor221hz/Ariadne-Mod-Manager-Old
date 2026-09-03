using System.Collections.Generic;
using System.Linq;
using Daedalus.Contracts.Mods;
using ReactiveUI;

namespace Daedalus.ModManager.GUI.ViewModels;

public sealed class ModEntryNodeViewModel : TreeNodeViewModel
{
    private readonly ILibraryMod _mod;
    private uint _priorityValue;

    public ILibraryMod Model => _mod;
    public override uint? PriorityValue => _priorityValue;
    public override string? VersionText => _mod.Info.Version;
    public override string DisplayName => _mod.Info.Name;
    public override IEnumerable<TreeNodeViewModel> Children =>
        _mod.Content.Children.Select(ContentNodeViewModel.Wrap);
    public override bool HasChildren => _mod.Content.Children.Count > 0;

    public ModEntryNodeViewModel(ILibraryMod mod)
    {
        _mod = mod;
        _priorityValue = mod.Info.Priority;
    }

    public void RefreshFromModel()
    {
        this.RaiseAndSetIfChanged(ref _priorityValue, _mod.Info.Priority, nameof(PriorityValue));
    }
}
