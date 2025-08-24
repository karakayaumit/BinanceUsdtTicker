namespace BinanceUsdtTicker
{
    public class Candle
    {
        public long Time { get; set; } // Unix time seconds
        public decimal Open { get; set; }
        public decimal High { get; set; }
        public decimal Low { get; set; }
        public decimal Close { get; set; }
    }
}
