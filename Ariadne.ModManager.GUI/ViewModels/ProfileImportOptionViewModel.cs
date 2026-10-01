using System.Windows.Input;
using Avalonia.Input;

namespace Ariadne.ModManager.GUI.ViewModels;

public sealed record ProfileImportOptionViewModel(
    string Header,
    ICommand Command,
    object? CommandParameter = null,
    KeyGesture? Gesture = null
);
