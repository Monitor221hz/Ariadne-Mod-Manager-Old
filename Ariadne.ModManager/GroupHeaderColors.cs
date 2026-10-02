using System.Drawing;

namespace Ariadne.ModManager;

public static class GroupHeaderColors
{
    public static Color Random()
    {
        for (var attempt = 0; attempt < 32; attempt++)
        {
            var color = Color.FromArgb(
                255,
                System.Random.Shared.Next(40, 100),
                System.Random.Shared.Next(40, 140),
                System.Random.Shared.Next(40, 160)
            );
            if (RelativeLuminance(color.R, color.G, color.B) <= 0.17)
            {
                return color;
            }
        }
        return Color.FromArgb(255, 69, 71, 90);
    }

    public static double RelativeLuminance(byte r, byte g, byte b)
    {
        static double Linear(double c) =>
            c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        return 0.2126 * Linear(r / 255.0) + 0.7152 * Linear(g / 255.0) + 0.0722 * Linear(b / 255.0);
    }
}
