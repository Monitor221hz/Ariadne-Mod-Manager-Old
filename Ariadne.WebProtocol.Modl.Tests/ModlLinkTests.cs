using Ariadne.WebProtocol.Modl;
using Xunit;

namespace Ariadne.WebProtocol.Modl.Tests;

public class ModlLinkTests
{
    [Fact]
    public void TryParse_ModPubExample_ReturnsGameAndDownloadUri()
    {
        var parsed = ModlLink.TryParse(
            "modl://falloutnv/?url=https%3A%2F%2Feddoursul.win%2FCyberware%20TTW%20Patch.7z",
            out var link
        );

        Assert.True(parsed);
        Assert.NotNull(link);
        Assert.Equal("falloutnv", link.GameId);
        Assert.Equal(
            "https://eddoursul.win/Cyberware%20TTW%20Patch.7z",
            link.DownloadUri.AbsoluteUri
        );
    }

    [Fact]
    public void TryParse_OtherGameId_IsAccepted()
    {
        var parsed = ModlLink.TryParse(
            "modl://other/?url=https%3A%2F%2Fexample.com%2Ffile.7z",
            out var link
        );

        Assert.True(parsed);
        Assert.NotNull(link);
        Assert.Equal("other", link.GameId);
    }

    [Fact]
    public void TryParse_MixedCaseLink_LowercasesGameId()
    {
        var parsed = ModlLink.TryParse(
            "MODL://SkyrimSE/?url=https%3A%2F%2Fexample.com%2Ffile.7z",
            out var link
        );

        Assert.True(parsed);
        Assert.NotNull(link);
        Assert.Equal("skyrimse", link.GameId);
    }

    [Fact]
    public void TryParse_EncodedPlusInUrl_PreservesPlus()
    {
        var parsed = ModlLink.TryParse(
            "modl://starfield/?url=https%3A%2F%2Fexample.com%2Ffile%2Bname.7z",
            out var link
        );

        Assert.True(parsed);
        Assert.NotNull(link);
        Assert.Equal("https://example.com/file+name.7z", link.DownloadUri.AbsoluteUri);
    }

    [Fact]
    public void TryParse_RawPlusInEncodedUrl_PreservesPlus()
    {
        var parsed = ModlLink.TryParse(
            "modl://starfield/?url=https%3A%2F%2Fexample.com%2Fa+b.7z",
            out var link
        );

        Assert.True(parsed);
        Assert.NotNull(link);
        Assert.Equal("https://example.com/a+b.7z", link.DownloadUri.AbsoluteUri);
    }

    [Fact]
    public void TryParse_AdditionalQueryParameters_AreTolerated()
    {
        var parsed = ModlLink.TryParse(
            "modl://skyrimse/?ref=button&url=https%3A%2F%2Fexample.com%2Ffile.7z&x=1",
            out var link
        );

        Assert.True(parsed);
        Assert.NotNull(link);
        Assert.Equal("skyrimse", link.GameId);
        Assert.Equal("example.com", link.DownloadUri.Host);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not a link")]
    [InlineData("https://example.com/?url=https%3A%2F%2Fexample.com%2Ffile.7z")]
    [InlineData("nxm://skyrimspecialedition/mods/1/files/2?key=k&expires=1&user_id=2")]
    [InlineData("modl:///?url=https%3A%2F%2Fexample.com%2Ffile.7z")]
    [InlineData("modl://fallout nv/?url=https%3A%2F%2Fexample.com%2Ffile.7z")]
    [InlineData("modl://falloutnv/?noturl=https%3A%2F%2Fexample.com%2Ffile.7z")]
    [InlineData("modl://falloutnv/?url=")]
    [InlineData("modl://falloutnv/?url=not-a-url")]
    [InlineData("modl://falloutnv/?url=..%2F..%2Fetc%2Fpasswd")]
    [InlineData("modl://falloutnv/?url=file%3A%2F%2FC%3A%2FWindows%2Fnotepad.exe")]
    [InlineData("modl://falloutnv/download?url=https%3A%2F%2Fexample.com%2Ffile.7z")]
    public void TryParse_InvalidLink_ReturnsFalse(string? link)
    {
        var parsed = ModlLink.TryParse(link, out var result);

        Assert.False(parsed);
        Assert.Null(result);
    }

    [Fact]
    public void Parse_InvalidLink_ThrowsFormatException()
    {
        Assert.Throws<FormatException>(() => ModlLink.Parse("not a link"));
    }

    [Fact]
    public void ToString_RoundTripsThroughParse()
    {
        var link = ModlLink.Parse(
            "modl://falloutnv/?url=https%3A%2F%2Feddoursul.win%2FCyberware%20TTW%20Patch.7z"
        );

        Assert.Equal(link, ModlLink.Parse(link.ToString()));
    }

    [Fact]
    public void ToString_InnerUrlWithReservedCharacters_EscapesAndRoundTrips()
    {
        var link = ModlLink.Parse(
            "modl://skyrimse/?url=https%3A%2F%2Fexample.com%2Ffile%3Fversion%3D1%26mode%3Dfast"
        );

        Assert.Equal("https://example.com/file?version=1&mode=fast", link.DownloadUri.AbsoluteUri);
        Assert.Equal(link, ModlLink.Parse(link.ToString()));
    }

    [Fact]
    public void Scheme_IsModl()
    {
        var link = ModlLink.Parse("modl://other/?url=https%3A%2F%2Fexample.com%2Fa.zip");

        Assert.Equal("modl", link.Scheme);
    }
}
