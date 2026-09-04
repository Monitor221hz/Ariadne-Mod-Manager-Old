using System.Collections.Generic;
using System.Linq;
using Daedalus.Contracts.Mods;
using Daedalus.Mods;
using ReactiveUI;

namespace Daedalus.ModManager.GUI.ViewModels;

public sealed class ModEntryNodeViewModel : TreeNodeViewModel
{
    private readonly ILibraryMod _mod;
    private uint _priorityValue;
    private string _sizeText;

    private string _displayName;
    public ILibraryMod Model => _mod;
    public override uint? PriorityValue => _priorityValue;
    public override string? VersionText => _mod.Info.Version;
    public override string DisplayName
    {
        get => _displayName;
        set => this.RaiseAndSetIfChanged(ref _displayName, value);
    }
    public override bool RenameAllowed => true;

    public override long SizeBytes => Children.Sum(c => c.SizeBytes);
    public override string SizeText
    {
        get => _sizeText;
        set => this.RaiseAndSetIfChanged(ref _sizeText, value);
    }

    public override IEnumerable<TreeNodeViewModel> Children =>
        _mod.Content.Children.Select(ContentNodeViewModel.Wrap);
    public override bool HasChildren => _mod.Content.Children.Count > 0;

    public ModEntryNodeViewModel(ILibraryMod mod)
    {
        _displayName = mod.Name;
        _mod = mod;
        _priorityValue = mod.Info.Priority;
        _sizeText = DiskSize.Format(SizeBytes);
    }

    public void RefreshFromModel()
    {
        this.RaiseAndSetIfChanged(ref _priorityValue, _mod.Info.Priority, nameof(PriorityValue));
    }

    protected override string ApplyRename(string name)
    {
        _mod.RenameTo(name);
        return _mod.Name;
    }
}
