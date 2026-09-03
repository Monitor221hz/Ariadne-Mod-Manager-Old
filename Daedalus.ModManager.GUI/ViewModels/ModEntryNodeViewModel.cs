using Daedalus.Contracts.Mods;

namespace Daedalus.ModManager.GUI.ViewModels;

public sealed class ModEntryNodeViewModel : DirectoryChildrenNodeViewModel
{
    private readonly IModInfo _mod;

    public override uint? PriorityValue => _mod.Priority;
    public override string? VersionText => _mod.Version;
    public override string DisplayName => _mod.Name;

    public ModEntryNodeViewModel(IModInfo mod)
        : base(mod.Directory)
    {
        _mod = mod;
    }
}
