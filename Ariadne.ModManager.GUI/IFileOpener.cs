using System.Diagnostics;

namespace Ariadne.ModManager.GUI;

public interface IFileOpener
{
    void OpenFile(string absolutePath);
}

public sealed class ShellFileOpener : IFileOpener
{
    public void OpenFile(string absolutePath)
    {
        try
        {
            Process.Start(
                new ProcessStartInfo
                {
                    FileName = absolutePath,
                    UseShellExecute = true,
                    WorkingDirectory = Path.GetDirectoryName(absolutePath),
                }
            );
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }
    }
}
