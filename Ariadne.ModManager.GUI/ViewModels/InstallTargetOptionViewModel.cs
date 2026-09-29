using System.Windows.Input;
using Ariadne.Contracts.Games;

namespace Ariadne.ModManager.GUI.ViewModels;

public sealed record InstallTargetOptionViewModel(IGamePath Target, ICommand Command)
{
    public string Header => Target.Key;
}
