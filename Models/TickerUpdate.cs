using System;

namespace BinanceUsdtTicker
{
    public readonly struct TickerUpdate
    {
        public TickerUpdate(
            string symbol,
            decimal price,
            double changePct,
            decimal volume,
            decimal? open = null,
            decimal? high = null,
            decimal? low = null,
            DateTime? lastUpdate = null,
            decimal? baselinePrice = null)
        {
            Symbol = symbol;
            Price = price;
            ChangePct = changePct;
            Volume = volume;
            Open = open;
            High = high;
            Low = low;
            LastUpdate = lastUpdate;
            BaselinePrice = baselinePrice;
        }

        public string Symbol { get; }
        public decimal Price { get; }
        public double ChangePct { get; }
        public decimal Volume { get; }
        public decimal? Open { get; }
        public decimal? High { get; }
        public decimal? Low { get; }
        public DateTime? LastUpdate { get; }
        public decimal? BaselinePrice { get; }
    }
}
