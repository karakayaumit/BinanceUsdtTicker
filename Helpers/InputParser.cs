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

            s = s.Replace(" ", string.Empty);

            var commaIndex = s.LastIndexOf(',');
            var dotIndex = s.LastIndexOf('.');

            if (commaIndex >= 0 && dotIndex >= 0)
            {
                if (commaIndex > dotIndex)
                {
                    s = s.Replace(".", string.Empty);
                    s = s.Replace(',', '.');
                }
                else
                {
                    s = s.Replace(",", string.Empty);
                }
            }
            else if (commaIndex >= 0)
            {
                s = s.Replace(',', '.');
            }
            else
            {
                s = s.Replace(",", string.Empty);
            }

            if (decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out value))
                return true;

            value = 0m;
            return false;
        }
    }
}
