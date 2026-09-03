namespace Daedalus.ModManager.GUI;

public static class DiskSize
{
    public static string Format(long bytes) =>
        bytes switch
        {
            < 1L << 10 => $"{bytes} B",
            < 1L << 20 => $"{bytes / (1.0 * (1 << 10)):0.#} KB",
            < 1L << 30 => $"{bytes / (1.0 * (1 << 20)):0.#} MB",
            _ => $"{bytes / (1.0 * (1 << 30)):0.#} GB",
        };
}
