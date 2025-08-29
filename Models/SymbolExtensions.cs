using System;

namespace BinanceUsdtTicker
{
    public static class SymbolExtensions
    {
        public static string ToBaseSymbol(this string? symbol)
        {
            if (string.IsNullOrWhiteSpace(symbol))
                return string.Empty;

            return symbol.EndsWith("USDT", StringComparison.OrdinalIgnoreCase)
                ? symbol[..^4]
                : symbol;
        }
    }
}
