using Ariadne.Contracts.ModManager;
using Xunit;

namespace Ariadne.ModManager.Tests;

public class PathNameTests
{
    [Theory]
    [InlineData("Valid Name 123")]
    [InlineData("a.b_c-d")]
    public void IsValid_Accepts_Ordinary_Names(string name)
    {
        Assert.True(PathName.IsValid(name));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("a/b")]
    [InlineData("a\\b")]
    [InlineData("a:b")]
    [InlineData("a*b")]
    [InlineData("a?b")]
    [InlineData("a<b")]
    [InlineData("a>b")]
    [InlineData("a|b")]
    [InlineData("a\"b")]
    [InlineData("a\tb")]
    public void IsValid_Rejects_Invalid_Names_On_Any_Platform(string name)
    {
        Assert.False(PathName.IsValid(name));
    }

    [Fact]
    public void Filter_Strips_Windows_And_Linux_Invalid_Chars()
    {
        Assert.Equal("abcdefghi", PathName.Filter("a<b>:c*d?e/f\\g|h\"i"));
    }

    [Fact]
    public void Filter_Strips_Control_Chars()
    {
        Assert.Equal("ab", PathName.Filter("a\tb"));
        Assert.Equal("ab", PathName.Filter("a\0b"));
    }

    [Fact]
    public void Filter_Returns_Input_When_Nothing_To_Strip()
    {
        Assert.Equal("Valid Name", PathName.Filter("Valid Name"));
    }

    [Fact]
    public void Filter_Handles_Null_And_Empty()
    {
        Assert.Equal(string.Empty, PathName.Filter(null));
        Assert.Equal(string.Empty, PathName.Filter(string.Empty));
    }
}
