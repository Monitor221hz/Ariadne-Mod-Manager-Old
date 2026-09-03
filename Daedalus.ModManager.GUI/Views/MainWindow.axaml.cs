using System.Linq;
using Avalonia.Controls;
using Daedalus.ModManager.GUI.ViewModels;

namespace Daedalus.ModManager.GUI.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Attach(DataContext as MainViewModel);
    }

    private void Attach(MainViewModel? viewModel)
    {
        if (viewModel is null)
        {
            return;
        }
        RebuildInstanceMenus(viewModel);
        viewModel.Instances.CollectionChanged += (_, _) => RebuildInstanceMenus(viewModel);

        ThemeItem.ItemsSource = viewModel
            .Themes.Select(entry =>
            {
                var item = new MenuItem
                {
                    Header = entry.Display,
                    ToggleType = MenuItemToggleType.Radio,
                    GroupName = "AppTheme",
                    DataContext = entry,
                };
                item.Bind(
                    MenuItem.IsCheckedProperty,
                    new Avalonia.Data.Binding(nameof(ThemeEntryViewModel.IsChecked))
                    {
                        Mode = Avalonia.Data.BindingMode.TwoWay,
                    }
                );
                return item;
            })
            .ToList();
    }

    private void RebuildInstanceMenus(MainViewModel viewModel)
    {
        SwitchInstanceItem.ItemsSource = viewModel
            .Instances.Select(instance => new MenuItem
            {
                Header = instance.DisplayHeader,
                Command = viewModel.SwitchInstanceCommand,
                CommandParameter = instance.Name,
                IsEnabled = !instance.IsCurrent,
            })
            .ToList();
        RemoveInstanceItem.ItemsSource = viewModel
            .Instances.Select(instance => new MenuItem
            {
                Header = instance.RemoveHeader,
                Command = viewModel.AskDeleteCommand,
                CommandParameter = instance.Name,
            })
            .ToList();
    }
}
