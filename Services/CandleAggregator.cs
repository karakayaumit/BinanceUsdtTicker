using System;
using System.Collections.Generic;

namespace BinanceUsdtTicker
{
    public class CandleAggregator
    {
        private readonly TimeSpan _interval;
        private readonly Dictionary<string, Candle> _candles = new(StringComparer.OrdinalIgnoreCase);

        public event Action<string, Candle>? OnCandle;

        public CandleAggregator(TimeSpan interval)
        {
            _interval = interval;
        }

        public void AddTick(string symbol, decimal price, DateTime timestamp)
        {
            var bucketStart = timestamp - TimeSpan.FromTicks(timestamp.Ticks % _interval.Ticks);
            var unix = new DateTimeOffset(bucketStart).ToUnixTimeSeconds();

            if (!_candles.TryGetValue(symbol, out var candle) || candle.Time != unix)
            {
                candle = new Candle
                {
                    Time = unix,
                    Open = price,
                    High = price,
                    Low = price,
                    Close = price
                };
                _candles[symbol] = candle;
            }
            else
            {
                if (price > candle.High) candle.High = price;
                if (price < candle.Low) candle.Low = price;
                candle.Close = price;
            }

            OnCandle?.Invoke(symbol, candle);
        }
    }
}
