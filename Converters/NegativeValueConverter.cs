using System;
using System.Globalization;
using System.Windows.Data;

namespace BinanceUsdtTicker
{
    /// <summary>
    /// Multiplies numeric values by -1 to ensure negative display.
    /// </summary>
    public class NegativeValueConverter : IValueConverter
    {
        public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return null;
            try
            {
                var dec = System.Convert.ToDecimal(value, CultureInfo.InvariantCulture);
                return dec * -1;
            }
            catch
            {
                return value;
            }
        }

        public object? ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return null;
            try
            {
                var dec = System.Convert.ToDecimal(value, CultureInfo.InvariantCulture);
                return dec * -1;
            }
            catch
            {
                return value;
            }
        }
    }
}
