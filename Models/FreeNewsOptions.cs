using System;

namespace BinanceUsdtTicker
{
    public class FreeNewsOptions
    {
        public TimeSpan PollInterval { get; set; } = TimeSpan.FromMinutes(1);
        public string? RssBaseUrl { get; set; }
    }
}
