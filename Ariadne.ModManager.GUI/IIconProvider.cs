using Avalonia.Media.Imaging;

namespace Ariadne.ModManager.GUI;

public interface IIconProvider
{
    Bitmap? ExtractIcon(string path);
}

public sealed class ShellIconProvider : IIconProvider
{
    public Bitmap? ExtractIcon(string path)
    {
        try
        {
            using var icon = System.Drawing.Icon.ExtractAssociatedIcon(path);
            if (icon is null)
            {
                return null;
            }
            using var bitmap = icon.ToBitmap();
            var stream = new MemoryStream();
            bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
            stream.Position = 0;
            return new Bitmap(stream);
        }
        catch (Exception ex)
            when (ex
                    is IOException
                        or InvalidOperationException
                        or ArgumentException
                        or PlatformNotSupportedException
            )
        {
            return null;
        }
    }
}
