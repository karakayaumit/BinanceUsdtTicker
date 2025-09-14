using System;
using System.Globalization;
using Xunit;
using BinanceUsdtTicker.Helpers;

namespace BinanceUsdtTicker.Tests;

public class DecimalParserTests
{
    [Theory]
    [InlineData("0,0014101", "0.0014101")]
    [InlineData("116.500,00", "116500")]
    [InlineData("0.00000045", "0.00000045")]
    public void ParseUser_Works(string input, string expected)
    {
        var value = DecimalParser.ParseUser(input);
        var exp = decimal.Parse(expected, CultureInfo.InvariantCulture);
        Assert.Equal(exp, value);
    }

    [Fact]
    public void Quantize_DoesNotChangeSmallPrice()
    {
        decimal tickSize = 0.0000001m;
        var price = DecimalParser.ParseUser("0,0014101");
        var quantized = Math.Round(price / tickSize, MidpointRounding.AwayFromZero) * tickSize;
        Assert.Equal(0.0014101m, quantized);
        Assert.Equal("0.0014101", DecimalParser.ToInvString(quantized));
    }
}
