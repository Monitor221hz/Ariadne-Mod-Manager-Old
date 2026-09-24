using Daedalus.WebProtocol.Nexus;
using Xunit;

namespace Daedalus.WebProtocol.Nexus.Tests;

public class NexusCommandLineTests
{
    private const string ModLink =
        "nxm://stardewvalley/mods/2400/files/123456?key=abc&expires=1893456000&user_id=42";

    [Fact]
    public void Parse_NoArguments_ReturnsEmptyIntent()
    {
        var intent = NexusCommandLine.Parse([]);

        Assert.Null(intent.Link);
    }

    [Fact]
    public void Parse_LinkOptionWithValue_ReturnsParsedLink()
    {
        var intent = NexusCommandLine.Parse([NexusCommandLine.LinkOption, ModLink]);

        var modLink = Assert.IsType<NxmModLink>(intent.Link);
        Assert.Equal(2400, modLink.ModId);
        Assert.Equal(123456, modLink.FileId);
    }

    [Fact]
    public void Parse_LinkOptionEqualsSyntax_ReturnsParsedLink()
    {
        var intent = NexusCommandLine.Parse([$"{NexusCommandLine.LinkOption}={ModLink}"]);

        Assert.IsType<NxmModLink>(intent.Link);
    }

    [Fact]
    public void Parse_MixedArguments_FindsLink()
    {
        var intent = NexusCommandLine.Parse(
            ["--verbose", NexusCommandLine.LinkOption, ModLink, "--flag"]
        );

        Assert.NotNull(intent.Link);
    }

    [Fact]
    public void Parse_CollectionLink_ReturnsCollectionIntent()
    {
        var intent = NexusCommandLine.Parse(
            [NexusCommandLine.LinkOption, "nxm://starfield/collections/abc123/revisions/7"]
        );

        var collectionLink = Assert.IsType<NxmCollectionLink>(intent.Link);
        Assert.Equal("abc123", collectionLink.CollectionSlug);
        Assert.Equal(7, collectionLink.Revision);
    }

    [Fact]
    public void Parse_InvalidLink_IntentHasNoLink()
    {
        var intent = NexusCommandLine.Parse([NexusCommandLine.LinkOption, "not a link"]);

        Assert.Null(intent.Link);
    }

    [Fact]
    public void Parse_LinkOptionWithoutValue_IntentHasNoLink()
    {
        var intent = NexusCommandLine.Parse([NexusCommandLine.LinkOption]);

        Assert.Null(intent.Link);
    }

    [Fact]
    public void Parse_MultipleLinkOptions_FirstValidWins()
    {
        var second = "nxm://skyrim/mods/1/files/2?key=xyz&expires=1893456000&user_id=7";
        var intent = NexusCommandLine.Parse(
            [NexusCommandLine.LinkOption, ModLink, NexusCommandLine.LinkOption, second]
        );

        var modLink = Assert.IsType<NxmModLink>(intent.Link);
        Assert.Equal(2400, modLink.ModId);
    }
}
