using Daedalus.VFS;
using Xunit;
using Wildcard = Daedalus.VFS.VirtualNode<object>.Wildcard;

namespace Daedalus.VFS.Tests;

public class WildcardTests
{
    [Theory]
    [InlineData("foo.txt", "*.txt")]
    [InlineData("FOO.TXT", "foo.txt")]
    [InlineData("foo", "foo*")]
    [InlineData("foo.txt", "foo*")]
    [InlineData("foo", "foo*.*")]
    [InlineData("foox.txt", "foo?.txt")]
    public void Match_FullMatch_ReturnsTrue(string name, string pattern)
    {
        Assert.True(Wildcard.Match(name, pattern));
    }

    [Theory]
    [InlineData("foo.txt", "*.md")]
    [InlineData("foo.txt", "foo?.txt")]
    [InlineData("footxt", "fo?.txt")]
    [InlineData("abc", "zzz")]
    public void Match_Mismatch_ReturnsFalse(string name, string pattern)
    {
        Assert.False(Wildcard.Match(name, pattern));
    }

    [Theory]
    [InlineData("foo.txt", "*.txt", "")]
    [InlineData("bar", "*", "")]
    [InlineData("bar", "*\\quux", "\\quux")]
    [InlineData("weapons", "*.nif", ".nif")]
    public void PartialMatch_ReturnsRemainder(string name, string pattern, string remainder)
    {
        Assert.Equal(remainder, Wildcard.PartialMatch(name, pattern));
    }

    [Fact]
    public void PartialMatch_Mismatch_ReturnsNull()
    {
        Assert.Null(Wildcard.PartialMatch("abc", "zzz"));
    }

    [Fact]
    public void PartialMatch_LeadingDot_IsIgnored()
    {
        Assert.Equal("", Wildcard.PartialMatch(".foo", "foo"));
    }

    [Fact]
    public void Match_DosWildcards_BehaveLikeStarAndQuestion()
    {
        Assert.True(Wildcard.Match("foo.txt", "foo<txt"));
        Assert.True(Wildcard.Match("foo.txt", "foo>txt"));
    }
}
