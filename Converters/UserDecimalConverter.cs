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

            s = s.Trim();

            // Allow the user to temporarily enter a trailing decimal separator
            // without losing it immediately due to binding updates.
            char last = s[s.Length - 1];
            if (last == '.' || last == ',')
                return Binding.DoNothing;

            if (InputParser.TryParseUserDecimal(s, out var v))
                return v;

            return Binding.DoNothing;
        }
    }
}
