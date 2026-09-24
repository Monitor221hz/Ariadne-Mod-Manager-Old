using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using Daedalus.WebProtocol;

namespace Daedalus.WebProtocol.Modl;

public sealed partial record ModlLink : ISchemeLink
{
    public required string GameId { get; init; }
    public required Uri DownloadUri { get; init; }

    public string Scheme => "modl";

    public override string ToString()
    {
        return $"modl://{GameId}/?url={Uri.EscapeDataString(DownloadUri.AbsoluteUri)}";
    }

    public static ModlLink Parse(string link)
    {
        if (TryParse(link, out var modlLink))
        {
            return modlLink;
        }

        throw new FormatException($"\"{link}\" is not a valid modl:// link.");
    }

    public static bool TryParse(string? link, [NotNullWhen(true)] out ModlLink? modlLink)
    {
        modlLink = null;
        if (string.IsNullOrWhiteSpace(link))
        {
            return false;
        }

        if (!Uri.TryCreate(link.Trim(), UriKind.Absolute, out var uri))
        {
            return false;
        }
        if (!string.Equals(uri.Scheme, "modl", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var gameId = uri.Host.ToLowerInvariant();
        if (!GameIdPattern().IsMatch(gameId))
        {
            return false;
        }
        if (uri.AbsolutePath is not ("" or "/"))
        {
            return false;
        }

        if (!TryGetQueryValue(uri.Query, "url", out var encodedUrl))
        {
            return false;
        }

        var downloadUrl = Uri.UnescapeDataString(encodedUrl);
        if (!Uri.TryCreate(downloadUrl, UriKind.Absolute, out var downloadUri))
        {
            return false;
        }
        if (downloadUri.Scheme is not ("http" or "https"))
        {
            return false;
        }

        modlLink = new ModlLink { GameId = gameId, DownloadUri = downloadUri };
        return true;
    }

    private static bool TryGetQueryValue(
        string query,
        string name,
        [NotNullWhen(true)] out string? value
    )
    {
        value = null;
        foreach (var pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = pair.IndexOf('=');
            if (separator < 0)
            {
                continue;
            }
            if (string.Equals(pair[..separator], name, StringComparison.Ordinal))
            {
                value = pair[(separator + 1)..];
                return value.Length > 0;
            }
        }

        return false;
    }

    [GeneratedRegex("^[a-z0-9]+$", RegexOptions.CultureInvariant)]
    private static partial Regex GameIdPattern();
}
