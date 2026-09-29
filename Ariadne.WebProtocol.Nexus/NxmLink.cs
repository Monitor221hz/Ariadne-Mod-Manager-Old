using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using Ariadne.WebProtocol;

namespace Ariadne.WebProtocol.Nexus;

public abstract partial record NxmLink : ISchemeLink
{
    public required string GameDomain { get; init; }

    public string Scheme => "nxm";

    public abstract override string ToString();

    public static NxmLink Parse(string link)
    {
        if (TryParse(link, out var nxmLink))
        {
            return nxmLink;
        }

        throw new FormatException($"\"{link}\" is not a valid nxm:// link.");
    }

    public static bool TryParse(string? link, [NotNullWhen(true)] out NxmLink? nxmLink)
    {
        nxmLink = null;
        if (string.IsNullOrWhiteSpace(link))
        {
            return false;
        }

        var unescaped = Regex.Unescape(link.Trim());

        var modMatch = ModLinkPattern().Match(unescaped);
        if (
            modMatch.Success
            && int.TryParse(modMatch.Groups["mod"].ValueSpan, out var modId)
            && int.TryParse(modMatch.Groups["file"].ValueSpan, out var fileId)
            && long.TryParse(modMatch.Groups["expiry"].ValueSpan, out var expires)
            && long.TryParse(modMatch.Groups["user"].ValueSpan, out var userId)
        )
        {
            nxmLink = new NxmModLink
            {
                GameDomain = modMatch.Groups["domain"].Value.ToLowerInvariant(),
                ModId = modId,
                FileId = fileId,
                Key = Uri.UnescapeDataString(modMatch.Groups["key"].Value),
                Expires = expires,
                UserId = userId,
            };
            return true;
        }

        var collectionMatch = CollectionLinkPattern().Match(unescaped);
        if (
            collectionMatch.Success
            && int.TryParse(collectionMatch.Groups["revision"].ValueSpan, out var revision)
        )
        {
            nxmLink = new NxmCollectionLink
            {
                GameDomain = collectionMatch.Groups["domain"].Value.ToLowerInvariant(),
                CollectionSlug = collectionMatch.Groups["slug"].Value,
                Revision = revision,
            };
            return true;
        }

        return false;
    }

    [GeneratedRegex(
        @"nxm:\/\/(?<domain>[a-z0-9]+)\/mods\/(?<mod>[0-9]+)\/files\/(?<file>[0-9]+)\?key=(?<key>[^&\s]+)&expires=(?<expiry>[0-9]+)&user_id=(?<user>[0-9]+)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture
    )]
    private static partial Regex ModLinkPattern();

    [GeneratedRegex(
        @"nxm:\/\/(?<domain>[a-z0-9]+)\/collections\/(?<slug>[a-z0-9]+)\/revisions\/(?<revision>[0-9]+)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture
    )]
    private static partial Regex CollectionLinkPattern();
}

public sealed record NxmModLink : NxmLink
{
    public required int ModId { get; init; }
    public required int FileId { get; init; }
    public required string Key { get; init; }
    public required long Expires { get; init; }
    public required long UserId { get; init; }

    public DateTimeOffset ExpiresUtc => DateTimeOffset.FromUnixTimeSeconds(Expires);

    public override string ToString()
    {
        return $"nxm://{GameDomain}/mods/{ModId}/files/{FileId}?key={Uri.EscapeDataString(Key)}&expires={Expires}&user_id={UserId}";
    }
}

public sealed record NxmCollectionLink : NxmLink
{
    public required string CollectionSlug { get; init; }
    public required int Revision { get; init; }

    public override string ToString()
    {
        return $"nxm://{GameDomain}/collections/{CollectionSlug}/revisions/{Revision}";
    }
}
