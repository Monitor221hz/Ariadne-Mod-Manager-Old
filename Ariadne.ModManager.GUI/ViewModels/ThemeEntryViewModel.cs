using ReactiveUI;

namespace Ariadne.ModManager.GUI.ViewModels;

public sealed class ThemeEntryViewModel(AppThemeInfo info, bool isChecked) : ViewModelBase
{
    private bool _isChecked = isChecked;

    public AppThemeInfo Info { get; } = info;
    public string Id => Info.Id;
    public string Display => Info.Display;

    public bool IsChecked
    {
        get => _isChecked;
        set => this.RaiseAndSetIfChanged(ref _isChecked, value);
    }
}
