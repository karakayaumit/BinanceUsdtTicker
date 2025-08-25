using System;

namespace BinanceUsdtTicker.Models
{
    /// <summary>
    /// Temel vadeli işlem pozisyon bilgisi.
    /// </summary>
    public class FuturesPosition
    {
        public string Symbol { get; set; } = string.Empty;
        public decimal PositionAmt { get; set; }
        public decimal EntryPrice { get; set; }
        public decimal UnrealizedPnl { get; set; }
        public int Leverage { get; set; }
        public string MarginType { get; set; } = string.Empty;

        public string BaseSymbol =>
            Symbol.EndsWith("USDT", StringComparison.OrdinalIgnoreCase)
                ? Symbol[..^4]
                : Symbol;
    }
}
