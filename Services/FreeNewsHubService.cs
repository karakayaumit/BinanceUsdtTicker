using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

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
                    var baseUrl = _options.RssBaseUrl?.TrimEnd('/');
                    if (!string.IsNullOrEmpty(baseUrl))
                    {
                        items.AddRange(await FetchRssAsync($"{baseUrl}/rss/bybit-new", "bybit"));
                        items.AddRange(await FetchRssAsync($"{baseUrl}/rss/kucoin-new", "kucoin"));
                        items.AddRange(await FetchRssAsync($"{baseUrl}/rss/okx-new", "okx"));
                    }
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

        private async Task<IList<NewsItem>> FetchRssAsync(string url, string source)
        {
            var list = new List<NewsItem>();
            try
            {
                using var stream = await _httpClient.GetStreamAsync(url);
                var doc = XDocument.Load(stream);
                foreach (var item in doc.Descendants("item"))
                {
                    var link = item.Element("link")?.Value ?? string.Empty;
                    var title = HtmlDecode(item.Element("title")?.Value ?? string.Empty);
                    var pubDate = item.Element("pubDate")?.Value;
                    var ts = DateTime.UtcNow;
                    if (DateTime.TryParse(pubDate, out var dt))
                        ts = dt.ToUniversalTime();
                    var type = Classify(title);
                    list.Add(new NewsItem(id: $"{source}::{link}", source: source, timestamp: ts, title: title, body: null, link: link, type: type, symbols: ExtractSymbols(title)));
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"{source} RSS fetch error: {ex}");
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

        private static readonly Regex UsdtSym = new(@"\b([A-Z0-9]{2,15})(?:/|-)?USDTM?\b", RegexOptions.Compiled);
        private static readonly Regex ParenSym = new(@"\(([A-Z0-9]{2,15})\)", RegexOptions.Compiled);

        private static IReadOnlyList<string> ExtractSymbols(string text)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (Match m in UsdtSym.Matches(text))
                set.Add(m.Groups[1].Value + "USDT");

            foreach (Match m in ParenSym.Matches(text))
            {
                var sym = m.Groups[1].Value;
                if (sym.EndsWith("USDT", StringComparison.OrdinalIgnoreCase))
                    set.Add(sym);
                else if (sym.EndsWith("USDTM", StringComparison.OrdinalIgnoreCase))
                    set.Add(sym.Substring(0, sym.Length - 1));
                else
                    set.Add(sym + "USDT");
            }

            return set.ToList();
        }
    }
}
