using System.Windows.Input;

namespace Ariadne.ModManager.GUI.ViewModels;

public sealed record NodeMenuItem(string Header, ICommand Command);
