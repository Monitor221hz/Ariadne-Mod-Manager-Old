using Daedalus.Contracts.Games;

namespace Daedalus.ModManager.GUI.ViewModels;

public sealed class DetectedGameViewModel(IInstalledGame game) : ViewModelBase
{
    public IInstalledGame Game { get; } = game;
    public string Name => Game.Configuration.Name;
    public string InstallPath => Game.InstallPath.FullName;
}
