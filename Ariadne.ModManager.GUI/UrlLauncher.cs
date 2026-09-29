using System.Diagnostics;

namespace Ariadne.ModManager.GUI;

public interface IUrlLauncher
{
    void Open(string url);
}

public sealed class UrlLauncher : IUrlLauncher
{
    public void Open(string url)
    {
        if (string.IsNullOrEmpty(url))
        {
            return;
        }
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }
}
