using System;
using System.Globalization;

namespace BinanceUsdtTicker.Helpers
{
    public static class DecimalParser
    {
        private static readonly CultureInfo Tr = new("tr-TR");
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        // TR ve EN karışık yazımları güvenle çözer.
        public static decimal ParseUser(string? s)
        {
            if (string.IsNullOrWhiteSpace(s))
                throw new FormatException("Empty price/qty");

            s = s.Trim();

            // 1) TR dene (115.800,00 / 0,0014101)
            if (decimal.TryParse(s, NumberStyles.Number, Tr, out var v))
                return v;

            // 2) EN/Invariant dene (115800.00 / 0.0014101)
            if (decimal.TryParse(s, NumberStyles.Number, Inv, out v))
                return v;

            // 3) Son çare: son nokta/virgülü ondalık varsay
            // (Örn. "116.500,00" gibi ikili formatlarda)
            var lastComma = s.LastIndexOf(',');
            var lastDot   = s.LastIndexOf('.');
            var sepIndex  = Math.Max(lastComma, lastDot);
            if (sepIndex >= 0)
            {
                var intPart = s[..sepIndex].Replace(".", "").Replace(",", "");
                var frac    = s[(sepIndex+1)..].Replace(".", "").Replace(",", "");
                var rebuilt = intPart + "." + frac;
                if (decimal.TryParse(rebuilt, NumberStyles.Number, Inv, out v))
                    return v;
            }

            throw new FormatException($"Invalid decimal input: '{s}'");
        }

        public static string ToInvString(decimal v)
            => v.ToString("0.####################", Inv)
                .TrimEnd('0').TrimEnd('.');
    }
}
