namespace Ariadne.Contracts.ModManager;

public static class PathName
{
    private static readonly char[] InvalidChars = BuildInvalidChars();

    private static char[] BuildInvalidChars()
    {
        var chars = new List<char> { '<', '>', ':', '"', '/', '\\', '|', '?', '*' };
        for (var c = '\0'; c < ' '; c++)
        {
            chars.Add(c);
        }
        return [.. chars];
    }

    public static bool IsValid(string name) =>
        !string.IsNullOrWhiteSpace(name) && name.IndexOfAny(InvalidChars) < 0;

    public static string Filter(string? text)
    {
        if (string.IsNullOrEmpty(text) || text.IndexOfAny(InvalidChars) < 0)
        {
            return text ?? string.Empty;
        }
        var chars = new List<char>(text.Length);
        foreach (var c in text)
        {
            if (Array.IndexOf(InvalidChars, c) < 0)
            {
                chars.Add(c);
            }
        }
        return new string([.. chars]);
    }
}
