using Fusion.Infrastructure.Warcraft;

namespace Fusion.Infrastructure.Tests;

public sealed class BlizzardRegionsTests
{
    [Theory]
    [InlineData("US", BlizzardRegions.Us)]
    [InlineData("eu", BlizzardRegions.Eu)]
    [InlineData(" Kr ", BlizzardRegions.Kr)]
    public void NormalizeReturnsCanonicalLowercase(string input, string expected)
    {
        var normalized = BlizzardRegions.Normalize(input);

        Assert.Equal(expected, normalized);
    }

    [Fact]
    public void NormalizeThrowsWhenUnsupported()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => BlizzardRegions.Normalize("latam"));

        Assert.Equal("region", exception.ParamName);
    }

    [Theory]
    [InlineData("us", true)]
    [InlineData("tw", true)]
    [InlineData("  cn ", true)]
    [InlineData("br", false)]
    [InlineData(null, false)]
    public void IsSupportedValidatesInput(string? region, bool expected)
    {
        var isSupported = BlizzardRegions.IsSupported(region);

        Assert.Equal(expected, isSupported);
    }
}
