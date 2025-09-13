using System;
using System.Globalization;

namespace BinanceUsdtTicker.Helpers
{
    /// <summary>
    /// Helper methods to safely parse user provided decimal strings.
    /// </summary>
    public static class InputParser
    {
        public static bool TryParseUserDecimal(string? s, out decimal value)
        {
            s = s?.Trim();
            if (string.IsNullOrEmpty(s))
            {
                value = 0m;
                return false;
            }

            var tr = new CultureInfo("tr-TR");
            s = s.Replace(" ", string.Empty)
                 .Replace(tr.NumberFormat.NumberGroupSeparator, string.Empty)
                 .Replace(CultureInfo.InvariantCulture.NumberFormat.NumberGroupSeparator, string.Empty);

            if (decimal.TryParse(s, NumberStyles.Number, tr, out value))
                return true;
            if (decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out value))
                return true;

            value = 0m;
            return false;
        }
    }
}
