using Daedalus.Contracts.Mods;
using ReactiveUI;

namespace Daedalus.ModManager.GUI.ViewModels;

public class ModViewModel : ViewModelBase
{
    private readonly ILibraryMod _mod;
    private string _target;
    private uint _priority;

    public ILibraryMod Model => _mod;

    public ulong ID => _mod.Info.ID;
    public string Name => _mod.Name;
    public string Version => _mod.Info.Version;
    public SourceType IDSource => _mod.Info.IDSource;
    public IReadOnlyList<string> Categories => _mod.Info.Categories;

    public string Target
    {
        get => _target;
        set
        {
            this.RaiseAndSetIfChanged(ref _target, value);
            _mod.Info.Target = value;
        }
    }

    public uint Priority
    {
        get => _priority;
        set
        {
            this.RaiseAndSetIfChanged(ref _priority, value);
            _mod.Info.Priority = value;
        }
    }

    public ModViewModel(ILibraryMod mod)
    {
        _mod = mod;
        _target = mod.Info.Target;
        _priority = mod.Info.Priority;
    }
}
