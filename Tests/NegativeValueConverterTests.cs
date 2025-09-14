using System.Globalization;
using BinanceUsdtTicker;
using Xunit;

public class NegativeValueConverterTests
{
    [Theory]
    [InlineData(1, -1)]
    [InlineData(-2.5, -2.5)]
    [InlineData(0, 0)]
    public void Convert_ReturnsNegative(decimal input, decimal expected)
    {
        var conv = new NegativeValueConverter();
        var result = conv.Convert(input, typeof(decimal), null, CultureInfo.InvariantCulture);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(1, -1)]
    [InlineData(-2.5, -2.5)]
    [InlineData(0, 0)]
    public void ConvertBack_ReturnsNegative(decimal input, decimal expected)
    {
        var conv = new NegativeValueConverter();
        var result = conv.ConvertBack(input, typeof(decimal), null, CultureInfo.InvariantCulture);
        Assert.Equal(expected, result);
    }
}
