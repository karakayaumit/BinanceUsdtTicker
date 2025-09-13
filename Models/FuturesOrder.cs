using System;

namespace BinanceUsdtTicker.Models
{
    /// <summary>
    /// Temel vadeli işlem emri bilgisi.
    /// </summary>
    public class FuturesOrder
    {
        public long OrderId { get; set; }
        public string Symbol { get; set; } = string.Empty;
        public string Side { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal Price { get; set; }
        public decimal Filled { get; set; }
        public decimal Amount => Price * Quantity;
        public string Status { get; set; } = string.Empty;
        public DateTime Time { get; set; }

        public string BaseSymbol => Symbol.ToBaseSymbol();
    }
}
