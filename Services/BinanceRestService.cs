using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace BinanceUsdtTicker
{
    public sealed class Candle
    {
        public DateTime OpenTimeUtc { get; set; }
        public decimal Open { get; set; }
        public decimal High { get; set; }
        public decimal Low { get; set; }
        public decimal Close { get; set; }
        public decimal Volume { get; set; }
    }

    public static class BinanceRestService
    {
        private static readonly HttpClient _http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(15)
        };

        /// <summary>
        /// Binance klines (mum) verisi: interval: 1m, 5m, 15m, 1h, 4h, 1d...
        /// </summary>
        public static async Task<List<Candle>> GetKlinesAsync(string symbol, string interval = "1m", int limit = 200, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(symbol)) throw new ArgumentException(nameof(symbol));
            if (limit <= 0 || limit > 1000) limit = 200;

            var url = $"https://api.binance.com/api/v3/klines?symbol={symbol.ToUpperInvariant()}&interval={interval}&limit={limit}";
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            resp.EnsureSuccessStatusCode();

            await using var stream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);

            var list = new List<Candle>(limit);
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                // kline array yapýsý:
                // 0 openTime(ms), 1 open, 2 high, 3 low, 4 close, 5 volume, 6 closeTime(ms), ...
                var openTimeMs = el[0].GetInt64();
                var c = new Candle
                {
                    OpenTimeUtc = DateTimeOffset.FromUnixTimeMilliseconds(openTimeMs).UtcDateTime,
                    Open = ParseDec(el[1]),
                    High = ParseDec(el[2]),
                    Low = ParseDec(el[3]),
                    Close = ParseDec(el[4]),
                    Volume = ParseDec(el[5])
                };
                list.Add(c);
            }
            return list;
        }

        private static decimal ParseDec(JsonElement v)
        {
            if (v.ValueKind == JsonValueKind.String &&
                decimal.TryParse(v.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
                return d;

            if (v.ValueKind == JsonValueKind.Number && v.TryGetDecimal(out var d2))
                return d2;

            return 0m;
        }
    }
}
