using Daedalus.WebProtocol.Nexus;
using Xunit;

namespace Daedalus.WebProtocol.Nexus.Tests;

public class NxmLinkTests
{
    [Fact]
    public void TryParse_ModLink_ReturnsModLinkWithAllFields()
    {
        var parsed = NxmLink.TryParse(
            "nxm://stardewvalley/mods/2400/files/123456?key=abcDEF123&expires=1893456000&user_id=42",
            out var link
        );

        Assert.True(parsed);
        var modLink = Assert.IsType<NxmModLink>(link);
        Assert.Equal("stardewvalley", modLink.GameDomain);
        Assert.Equal(2400, modLink.ModId);
        Assert.Equal(123456, modLink.FileId);
        Assert.Equal("abcDEF123", modLink.Key);
        Assert.Equal(1893456000, modLink.Expires);
        Assert.Equal(42, modLink.UserId);
    }

    [Fact]
    public void TryParse_EscapedLink_UnescapesSlashes()
    {
        var parsed = NxmLink.TryParse(
            @"nxm:\/\/starfield\/collections\/abc123\/revisions\/7",
            out var link
        );

        Assert.True(parsed);
        var collectionLink = Assert.IsType<NxmCollectionLink>(link);
        Assert.Equal("starfield", collectionLink.GameDomain);
        Assert.Equal("abc123", collectionLink.CollectionSlug);
        Assert.Equal(7, collectionLink.Revision);
    }

    [Fact]
    public void TryParse_MixedCaseLink_LowercasesDomain()
    {
        var parsed = NxmLink.TryParse(
            "nxm://SkyrimSpecialEdition/mods/1/files/2?key=KeY&expires=1&user_id=2",
            out var link
        );

        Assert.True(parsed);
        var modLink = Assert.IsType<NxmModLink>(link);
        Assert.Equal("skyrimspecialedition", modLink.GameDomain);
        Assert.Equal("KeY", modLink.Key);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not a link")]
    [InlineData("https://www.nexusmods.com/stardewvalley/mods/2400")]
    [InlineData("nxm://stardewvalley/mods/2400")]
    [InlineData("nxm://stardewvalley/mods/2400/files/123456")]
    [InlineData("nxm://stardewvalley/mods/2400/files/123456?key=&expires=1893456000&user_id=42")]
    [InlineData("nxm://stardewvalley/collections/abc123")]
    public void TryParse_InvalidLink_ReturnsFalse(string? link)
    {
        var parsed = NxmLink.TryParse(link, out var result);

        Assert.False(parsed);
        Assert.Null(result);
    }

    [Fact]
    public void Parse_InvalidLink_ThrowsFormatException()
    {
        Assert.Throws<FormatException>(() => NxmLink.Parse("not a link"));
    }

    [Fact]
    public void ExpiresUtc_MatchesUnixTimestamp()
    {
        var link = (NxmModLink)
            NxmLink.Parse(
                "nxm://stardewvalley/mods/2400/files/123456?key=abc&expires=1893456000&user_id=42"
            );

        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1893456000), link.ExpiresUtc);
    }

    [Fact]
    public void ToString_ModLink_RoundTripsThroughParse()
    {
        var link = NxmLink.Parse(
            "nxm://stardewvalley/mods/2400/files/123456?key=abc&expires=1893456000&user_id=42"
        );

        Assert.Equal(link, NxmLink.Parse(link.ToString()));
    }

    [Fact]
    public void ToString_CollectionLink_RoundTripsThroughParse()
    {
        var link = NxmLink.Parse("nxm://starfield/collections/abc123/revisions/7");

        Assert.Equal(link, NxmLink.Parse(link.ToString()));
    }

    [Fact]
    public void ToString_KeyWithReservedCharacters_EscapesAndRoundTrips()
    {
        var link = new NxmModLink
        {
            GameDomain = "skyrim",
            ModId = 1,
            FileId = 2,
            Key = "a&b=c/d e",
            Expires = 1L,
            UserId = 2L,
        };

        var reparsed = Assert.IsType<NxmModLink>(NxmLink.Parse(link.ToString()));

        Assert.Equal(link, reparsed);
    }
}
