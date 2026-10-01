using System.Windows.Input;

namespace Ariadne.ModManager.GUI.ViewModels;

public sealed record InstanceMenuOptionViewModel(
    string Header,
    ICommand Command,
    object? CommandParameter,
    bool IsEnabled = true
);
