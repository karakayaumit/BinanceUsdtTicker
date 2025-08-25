using System;

namespace BinanceUsdtTicker.Models
{
    /// <summary>
    /// Temel vadeli işlem emri bilgisi.
    /// </summary>
    public class FuturesOrder
    {
        public string Symbol { get; set; } = string.Empty;
        public string Side { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal Price { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime Time { get; set; }

        public string BaseSymbol =>
            Symbol.EndsWith("USDT", StringComparison.OrdinalIgnoreCase)
                ? Symbol[..^4]
                : Symbol;
    }
}
