using Daedalus.Contracts.ModManager;
using Daedalus.Contracts.Mods;
using ReactiveUI;

namespace Daedalus.ModManager.GUI.ViewModels;

public class ProfileViewModel : ViewModelBase
{
    private readonly IModProfile _profile;
    private string _name;

    public string Name
    {
        get => _name;
        set
        {
            this.RaiseAndSetIfChanged(ref _name, value);
            _profile.Name = value;
        }
    }

    public string Version => _profile.Version.ToString();
    public string ProfileFolder => _profile.ProfileFolder.FullName;
    public string OverwriteFolder => _profile.OverwriteFolder.FullName;

    public IModProfile Model => _profile;

    public ProfileViewModel(IModProfile profile)
    {
        _profile = profile;
        _name = profile.Name;
    }

    public void AddLooseMod(ILibraryMod mod) => _profile.ModList.Add(mod);

    public void AddGroup(IModGroup group) => _profile.ModList.ModGroups.Add(group);
}
