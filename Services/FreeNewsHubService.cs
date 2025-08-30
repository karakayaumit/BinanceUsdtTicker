using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace BinanceUsdtTicker
{
    public class FreeNewsHubService : IAsyncDisposable
    {
        private readonly FreeNewsOptions _options;
        private readonly HttpClient _httpClient = new();
        private readonly HashSet<string> _dedup = new(StringComparer.Ordinal);
        private CancellationTokenSource? _cts;
        private Task? _loop;

        public event EventHandler<NewsItem>? NewsReceived;

        public FreeNewsHubService(FreeNewsOptions options)
        {
            _options = options;
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (compatible; BinanceUsdtTicker)");
        }

        public Task StartAsync()
        {
            _cts = new CancellationTokenSource();
            _loop = Task.Run(() => PollAsync(_cts.Token));
            return Task.CompletedTask;
        }

        private async Task PollAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var items = new List<NewsItem>();
                    items.AddRange(await FetchBybitAsync());
                    items.AddRange(await FetchKuCoinAsync());
                    items.AddRange(await FetchOkxAsync());
                    if (!string.IsNullOrEmpty(_options.CryptoPanicToken))
                        items.AddRange(await FetchCryptoPanicAsync(_options.CryptoPanicToken));

                    foreach (var item in items)
                    {
                        if (_dedup.Add(item.Id))
                            NewsReceived?.Invoke(this, item);
                    }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Error while fetching news: {ex}");
                }

                try
                {
                    await Task.Delay(_options.PollInterval, ct);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (_cts != null)
            {
                _cts.Cancel();
                if (_loop != null)
                {
                    try { await _loop; } catch { }
                }
                _cts.Dispose();
            }
            _httpClient.Dispose();
        }

        private async Task<IList<NewsItem>> FetchBybitAsync()
        {
            var list = new List<NewsItem>();
            try
            {
                var url = "https://api.bybit.com/v5/announcements/index?locale=en-US&type=new_crypto&limit=20";
                var json = await _httpClient.GetStringAsync(url);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("result", out var result) &&
                    result.TryGetProperty("list", out var arr))
                {
                    foreach (var el in arr.EnumerateArray())
                    {
                        var link = el.GetProperty("url").GetString() ?? string.Empty;
                        var title = HtmlDecode(el.GetProperty("title").GetString() ?? string.Empty);

                        long tsMillis = 0;
                        if (el.TryGetProperty("publishTime", out var publishTime))
                        {
                            if (publishTime.ValueKind == JsonValueKind.Number)
                                tsMillis = publishTime.GetInt64();
                            else if (publishTime.ValueKind == JsonValueKind.String && long.TryParse(publishTime.GetString(), out var v))
                                tsMillis = v;
                        }
                        else if (el.TryGetProperty("dateTimestamp", out var dateTimestamp))
                        {
                            if (dateTimestamp.ValueKind == JsonValueKind.Number)
                                tsMillis = dateTimestamp.GetInt64();
                            else if (dateTimestamp.ValueKind == JsonValueKind.String && long.TryParse(dateTimestamp.GetString(), out var v))
                                tsMillis = v;
                        }

                        var ts = tsMillis > 0
                            ? DateTimeOffset.FromUnixTimeMilliseconds(tsMillis).UtcDateTime
                            : DateTime.UtcNow;

                        var type = Classify(title);
                        list.Add(new NewsItem(id: $"bybit::{link}", source: "bybit", timestamp: ts, title: title, body: null, link: link, type: type, symbols: ExtractSymbols(title)));
                    }
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Bybit fetch error: {ex}");
            }
            return list;
        }

        private async Task<IList<NewsItem>> FetchKuCoinAsync()
        {
            var list = new List<NewsItem>();
            try
            {
                var url = "https://api.kucoin.com/api/v3/announcements?annType=new-listings&lang=en_US&pageSize=20&currentPage=1";
                var json = await _httpClient.GetStringAsync(url);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("data", out var data) &&
                    data.TryGetProperty("items", out var items))
                {
                    foreach (var it in items.EnumerateArray())
                    {
                        var link = it.TryGetProperty("annUrl", out var pUrl) ? pUrl.GetString() ?? string.Empty : string.Empty;
                        var title = HtmlDecode(it.TryGetProperty("annTitle", out var pTitle) ? pTitle.GetString() ?? string.Empty : string.Empty);
                        var tsMs = it.TryGetProperty("cTime", out var pTime) ? pTime.GetInt64() : 0L;
                        var ts = tsMs > 0 ? DateTimeOffset.FromUnixTimeMilliseconds(tsMs).UtcDateTime : DateTime.UtcNow;
                        var type = Classify(title);
                        list.Add(new NewsItem(id: $"kucoin::{link}", source: "kucoin", timestamp: ts, title: title, body: null, link: link, type: type, symbols: ExtractSymbols(title)));
                    }
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"KuCoin fetch error: {ex}");
            }
            return list;
        }

        private async Task<IList<NewsItem>> FetchOkxAsync()
        {
            var list = new List<NewsItem>();
            try
            {
                var html = await _httpClient.GetStringAsync("https://www.okx.com/announcements/category/listing");
                var rx = new Regex(
                    @"<a[^>]+href=""(?<link>[^""]+)""[^>]*class=""[^""]*announcement-item[^""]*""[^>]*>\s*<div[^>]*class=""[^""]*title[^""]*"">(?<title>[^<]+)</div>\s*<div[^>]*class=""[^""]*time[^""]*"">(?<time>[^<]+)</div>",
                    RegexOptions.IgnoreCase | RegexOptions.Singleline);
                foreach (Match m in rx.Matches(html))
                {
                    var link = "https://www.okx.com" + m.Groups["link"].Value;
                    var title = HtmlDecode(m.Groups["title"].Value.Trim());
                    if (DateTime.TryParse(m.Groups["time"].Value, out var ts))
                    {
                        var type = Classify(title);
                        list.Add(new NewsItem(id: $"okx::{link}", source: "okx", timestamp: ts, title: title, body: null, link: link, type: type, symbols: ExtractSymbols(title)));
                    }
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"OKX fetch error: {ex}");
            }
            return list;
        }

        private async Task<IList<NewsItem>> FetchCryptoPanicAsync(string token)
        {
            var list = new List<NewsItem>();
            try
            {
                var url = $"https://cryptopanic.com/api/v1/posts/?auth_token={token}&public=true";
                var json = await _httpClient.GetStringAsync(url);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("results", out var results))
                {
                    foreach (var el in results.EnumerateArray())
                    {
                        var id = el.GetProperty("id").GetInt32();
                        var title = el.GetProperty("title").GetString() ?? string.Empty;
                        var link = el.GetProperty("url").GetString() ?? string.Empty;
                        var publishedAt = el.GetProperty("published_at").GetDateTime();
                        var type = Classify(title);
                        list.Add(new NewsItem(id: $"cp::{id}", source: "cryptopanic", timestamp: publishedAt, title: title, body: null, link: link, type: type, symbols: ExtractSymbols(title)));
                    }
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"CryptoPanic fetch error: {ex}");
            }
            return list;
        }

        private static string HtmlDecode(string text) => System.Net.WebUtility.HtmlDecode(text);

        private static NewsType Classify(string t)
        {
            var tl = t.ToLowerInvariant();
            if (tl.Contains("delist")) return NewsType.Delisting;
            if (tl.Contains("list")) return NewsType.Listing;
            if (tl.Contains("mainten")) return NewsType.Maintenance;
            if (tl.Contains("sec") || tl.Contains("lawsuit") || tl.Contains("regulat")) return NewsType.Regulatory;
            return NewsType.General;
        }

        private static readonly Regex UsdtSym = new(@"\b([A-Z0-9]{2,15})(?:/|-)?USDT\b", RegexOptions.Compiled);
        private static IReadOnlyList<string> ExtractSymbols(string text)
            => UsdtSym.Matches(text).Select(x => x.Groups[1].Value + "USDT").Distinct().ToList();
    }
}
