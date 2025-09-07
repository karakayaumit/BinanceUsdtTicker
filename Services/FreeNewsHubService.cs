using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
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
        private readonly ISymbolExtractor _symbolExtractor;
        private CancellationTokenSource? _cts;
        private Task? _loop;

        public event EventHandler<NewsItem>? NewsReceived;

        public FreeNewsHubService(FreeNewsOptions options, ISymbolExtractor symbolExtractor)
        {
            _options = options;
            _symbolExtractor = symbolExtractor;
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
                    var symbols = _symbolExtractor.ExtractUsdtPairs(title);
                    list.Add(new NewsItem(id: $"{source}::{link}", source: source, timestamp: ts, title: title, titleTranslate: null, body: null, link: link, type: type, symbols: symbols));
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"{source} RSS fetch error: {ex}");
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

        // symbol extraction delegated to ISymbolExtractor
    }
}
