using System;

namespace BinanceUsdtTicker
{
    public class FreeNewsOptions
    {
        public TimeSpan PollInterval { get; set; } = TimeSpan.FromMinutes(1);
        public string? CryptoPanicToken { get; set; }
        public string RssBaseUrl { get; set; } = "http://localhost:5000";
    }
}
