using System.Diagnostics;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;

namespace Ariadne.ModManager.GUI;

public interface IAppLifecycleService
{
    void Quit();
    void Restart();
}

public sealed class AppLifecycleService : IAppLifecycleService
{
    public void Quit()
    {
        if (
            Application.Current?.ApplicationLifetime
            is IClassicDesktopStyleApplicationLifetime desktop
        )
        {
            desktop.Shutdown();
        }
    }

    public void Restart()
    {
        Process.Start(Environment.ProcessPath!);
        Quit();
    }
}
