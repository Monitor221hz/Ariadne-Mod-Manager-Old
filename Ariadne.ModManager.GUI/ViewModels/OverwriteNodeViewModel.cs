using Ariadne.Contracts.ModManager;
using ReactiveUI;

namespace Ariadne.ModManager.GUI.ViewModels;

public sealed class OverwriteNodeViewModel : ContentHostNodeViewModel
{
    private string _displayName = "Overwrite";

    public OverwriteNodeViewModel(ILibraryMod mod)
        : base(mod) { }

    public override string DisplayName
    {
        get => _displayName;
        set => this.RaiseAndSetIfChanged(ref _displayName, value);
    }

    public override bool RenameAllowed => false;
}
