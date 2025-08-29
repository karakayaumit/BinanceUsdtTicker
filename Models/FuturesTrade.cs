using System;

namespace BinanceUsdtTicker.Models
{
    /// <summary>
    /// Temel vadeli işlem trade kaydı.
    /// </summary>
    public class FuturesTrade
    {
        public string Symbol { get; set; } = string.Empty;
        public string Side { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal Price { get; set; }
        public decimal Fee { get; set; }
        public decimal RealizedPnl { get; set; }
        public DateTime Time { get; set; }

        public string BaseSymbol => Symbol.ToBaseSymbol();

        public decimal NetProfit => RealizedPnl - Fee;
    }
}
