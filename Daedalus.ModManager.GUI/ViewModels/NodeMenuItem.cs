using System.Windows.Input;

namespace Daedalus.ModManager.GUI.ViewModels;

public sealed record NodeMenuItem(string Header, ICommand Command);
