using System.Globalization;
using System.Windows.Data;
using BinanceUsdtTicker;
using Xunit;

public class UserDecimalConverterTests
{
    [Fact]
    public void ConvertBack_AllowsLeadingZeroFraction()
    {
        var conv = new UserDecimalConverter();
        var culture = CultureInfo.GetCultureInfo("tr-TR");

        var intermediate = conv.ConvertBack("0,0", typeof(decimal), null, culture);
        Assert.Same(Binding.DoNothing, intermediate);

        var final = conv.ConvertBack("0,0001563", typeof(decimal), null, culture);
        Assert.IsType<decimal>(final);
        Assert.Equal(0.0001563m, (decimal)final);
    }
}
