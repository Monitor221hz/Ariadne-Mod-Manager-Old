using System.Windows.Input;
using Avalonia.Media.Imaging;

namespace Ariadne.ModManager.GUI.ViewModels;

public sealed record LaunchTargetItemViewModel(string Name, Bitmap? Icon, ICommand LaunchCommand);
