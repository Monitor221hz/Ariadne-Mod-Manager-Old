using System;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Daedalus.ModManager.GUI.ViewModels;
using Daedalus.ModManager.GUI.Views;
using Microsoft.Extensions.DependencyInjection;

namespace Daedalus.ModManager.GUI;

public partial class App : Application
{
    public static ServiceProvider Services { get; set; } = null!;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            AppTheme.Initialize();
            AppTheme.Apply(AppTheme.LoadSaved().Id);
            var mainViewModel = Services.GetRequiredService<MainViewModel>();
            desktop.Exit += (_, _) =>
            {
                try
                {
                    mainViewModel.SaveActiveInstance();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex);
                }
                Services.Dispose();
            };
            desktop.MainWindow = new MainWindow { DataContext = mainViewModel };
            mainViewModel.InitializeCommand.Execute().Subscribe();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
