using Daedalus.WebProtocol;
using Xunit;

namespace Daedalus.WebProtocol.Tests;

public class ProtocolCommandLineTests
{
    private static readonly IReadOnlyDictionary<string, string> Options = new Dictionary<
        string,
        string
    >
    {
        ["--nxm"] = "nxm",
        ["--modl"] = "modl",
    };

    [Fact]
    public void Parse_NoArguments_ReturnsEmptyIntent()
    {
        var intent = ProtocolCommandLine.Parse([], Options);

        Assert.Null(intent.Scheme);
        Assert.Null(intent.Link);
    }

    [Fact]
    public void Parse_NxmOptionWithValue_ReturnsSchemeAndLink()
    {
        var intent = ProtocolCommandLine.Parse(["--nxm", "nxm://games/mods/1"], Options);

        Assert.Equal("nxm", intent.Scheme);
        Assert.Equal("nxm://games/mods/1", intent.Link);
    }

    [Fact]
    public void Parse_EqualsSyntax_ReturnsLink()
    {
        var intent = ProtocolCommandLine.Parse(["--modl=modl://skyrim/?url=x"], Options);

        Assert.Equal("modl", intent.Scheme);
        Assert.Equal("modl://skyrim/?url=x", intent.Link);
    }

    [Fact]
    public void Parse_MixedArguments_FindsLink()
    {
        var intent = ProtocolCommandLine.Parse(["--verbose", "--nxm", "the-link"], Options);

        Assert.Equal("nxm", intent.Scheme);
        Assert.Equal("the-link", intent.Link);
    }

    [Fact]
    public void Parse_OptionWithoutValue_IntentHasNoLink()
    {
        var intent = ProtocolCommandLine.Parse(["--nxm"], Options);

        Assert.Null(intent.Scheme);
        Assert.Null(intent.Link);
    }

    [Fact]
    public void Parse_FirstOptionWins()
    {
        var intent = ProtocolCommandLine.Parse(["--modl", "first", "--nxm", "second"], Options);

        Assert.Equal("modl", intent.Scheme);
        Assert.Equal("first", intent.Link);
    }
}
