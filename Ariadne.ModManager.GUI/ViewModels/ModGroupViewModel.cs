using System.Collections.ObjectModel;
using Avalonia.Media;
using Ariadne.Contracts.ModManager;
using ReactiveUI;

namespace Ariadne.ModManager.GUI.ViewModels;

public class ModGroupViewModel : ViewModelBase
{
    private readonly IModGroup _group;
    private Color _headerColor;

    public string Name => _group.Name;

    public Color HeaderColor
    {
        get => _headerColor;
        set
        {
            this.RaiseAndSetIfChanged(ref _headerColor, value);
            _group.HeaderColor = System.Drawing.Color.FromArgb(value.A, value.R, value.G, value.B);
        }
    }

    public ObservableCollection<ModViewModel> Mods { get; }

    public ModGroupViewModel(IModGroup group)
    {
        _group = group;
        var color = group.HeaderColor;
        _headerColor = Color.FromArgb(color.A, color.R, color.G, color.B);
        Mods = new ObservableCollection<ModViewModel>(group.Select(mod => new ModViewModel(mod)));
    }
}
