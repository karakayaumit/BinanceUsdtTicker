using BinanceUsdtTicker.Helpers;
using Xunit;

namespace BinanceUsdtTicker.Tests;

public class InputParserTests
{
    [Theory]
    [InlineData("116500,50")]
    [InlineData("116500.50")]
    [InlineData("116,500.50")]
    [InlineData("116.500,50")]
    public void ParsesDecimalWithCommaOrDot(string input)
    {
        var ok = InputParser.TryParseUserDecimal(input, out var value);
        Assert.True(ok);
        Assert.Equal(116500.50m, value);
    }
}
