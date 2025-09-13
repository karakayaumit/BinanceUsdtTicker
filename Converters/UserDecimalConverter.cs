using System;
using System.Globalization;
using System.Windows.Data;
using BinanceUsdtTicker.Helpers;

namespace BinanceUsdtTicker
{
    /// <summary>
    /// Converts between user entered decimal strings and decimal values using
    /// Turkish and invariant culture fallbacks.
    /// </summary>
    public class UserDecimalConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is decimal d)
                return d.ToString("0.####################", CultureInfo.CurrentCulture);
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var s = value?.ToString();
            if (string.IsNullOrWhiteSpace(s))
                return null;
            if (InputParser.TryParseUserDecimal(s, out var v))
                return v;
            throw new FormatException($"Invalid decimal: {s}");
        }
    }
}
