using System;
using System.Globalization;
using System.Linq;
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
        public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is decimal d)
            {
                var bits  = decimal.GetBits(d);
                var scale = (bits[3] >> 16) & 0x7F;
                var fmt   = scale == 0 ? "0" : "0." + new string('0', scale);
                return d.ToString(fmt, CultureInfo.CurrentCulture);
            }
            return string.Empty;
        }

        public object? ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
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

            // If the text contains a decimal separator but only zeros so far,
            // defer parsing until the user enters a non-zero digit to avoid
            // the binding resetting the text to "0".
            int sepIndex = s.LastIndexOfAny(new[] { '.', ',' });
            if (sepIndex >= 0)
            {
                var intPart  = s[..sepIndex].Replace(".", string.Empty).Replace(",", string.Empty);
                var fracPart = s[(sepIndex + 1)..];
                bool intZeros  = intPart.Length == 0 || intPart.All(ch => ch == '0');
                bool fracZeros = fracPart.Length > 0 && fracPart.All(ch => ch == '0');
                if (intZeros && fracZeros)
                    return Binding.DoNothing;
            }

            try
            {
                return DecimalParser.ParseUser(s);
            }
            catch (FormatException)
            {
                return Binding.DoNothing;
            }
        }
    }
}
