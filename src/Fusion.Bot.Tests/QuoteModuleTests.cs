using Fusion.Bot.Modules;
using Xunit;

namespace Fusion.Bot.Tests;

public sealed class QuoteModuleTests
{
    [Theory]
    [InlineData("Ada Lovelace", "ada-lovelace")]
    [InlineData("  Ada  Lovelace  ", "ada-lovelace")]
    [InlineData("R@nd()m Text!!", "r-nd-m-text")]
    [InlineData("", "unknown")]
    [InlineData("()()(()()())", "unknown")]
    public void NormalizePersonKeyFormatsExpectedValues(string input, string expected)
    {
        var result = QuoteModule.NormalizePersonKey(input);
        Assert.Equal(expected, result);
    }
}
