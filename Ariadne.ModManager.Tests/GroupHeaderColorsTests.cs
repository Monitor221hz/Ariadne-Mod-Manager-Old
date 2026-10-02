using Xunit;

namespace Ariadne.ModManager.Tests;

public class GroupHeaderColorsTests
{
    [Fact]
    public void Random_AlwaysReturnsDarkEnoughColor()
    {
        for (var i = 0; i < 100; i++)
        {
            var color = GroupHeaderColors.Random();
            Assert.True(
                GroupHeaderColors.RelativeLuminance(color.R, color.G, color.B) <= 0.17,
                $"Luminance of {color} exceeds threshold"
            );
        }
    }

    [Fact]
    public void RelativeLuminance_BlackIsZero_WhiteIsOne()
    {
        Assert.Equal(0.0, GroupHeaderColors.RelativeLuminance(0, 0, 0), 3);
        Assert.Equal(1.0, GroupHeaderColors.RelativeLuminance(255, 255, 255), 3);
    }
}
