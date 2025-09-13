using System;
using System.Globalization;

namespace BinanceUsdtTicker.Helpers
{
    public static class PrecisionHelper
    {
        public static decimal AdjustToStep(decimal value, decimal step)
        {
            if (step <= 0m) return value;
            var precision = GetPrecision(step);
            var n = Math.Floor(value / step) * step;
            return Math.Round(n, precision, MidpointRounding.ToZero);
        }

        public static string FormatForApi(decimal value, decimal step)
        {
            var precision = GetPrecision(step);
            string formatted = precision > 0
                ? value.ToString($"F{precision}", CultureInfo.InvariantCulture)
                : value.ToString(CultureInfo.InvariantCulture);
            var trimmed = formatted.TrimEnd('0').TrimEnd('.');
            return string.IsNullOrEmpty(trimmed) ? "0" : trimmed;
        }

        public static int GetPrecision(decimal step)
        {
            var s = step.ToString(CultureInfo.InvariantCulture).TrimEnd('0');
            var idx = s.IndexOf('.');
            return idx >= 0 ? s.Length - idx - 1 : 0;
        }
    }
}
