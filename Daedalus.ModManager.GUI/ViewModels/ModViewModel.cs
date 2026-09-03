using System.Collections.Generic;
using Daedalus.Contracts.Mods;
using ReactiveUI;

namespace Daedalus.ModManager.GUI.ViewModels;

public class ModViewModel : ViewModelBase
{
    private readonly IModInfo _mod;
    private string _target;
    private uint _priority;

    public IModInfo Model => _mod;

    public ulong ID => _mod.ID;
    public string Name => _mod.Name;
    public string Version => _mod.Version;
    public SourceType IDSource => _mod.IDSource;
    public string DirectoryPath => _mod.Directory.FullName;
    public IReadOnlyList<string> Categories => _mod.Categories;

    public string Target
    {
        get => _target;
        set
        {
            this.RaiseAndSetIfChanged(ref _target, value);
            _mod.Target = value;
        }
    }

    public uint Priority
    {
        get => _priority;
        set
        {
            this.RaiseAndSetIfChanged(ref _priority, value);
            _mod.Priority = value;
        }
    }

    public ModViewModel(IModInfo mod)
    {
        _mod = mod;
        _target = mod.Target;
        _priority = mod.Priority;
    }
}
