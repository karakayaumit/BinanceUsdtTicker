using System.Collections.Concurrent;
using System.Text.Json;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using System.Linq;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BinanceUsdtTicker;

/// <summary>
/// Background worker that polls exchange listing APIs and logs new items.
/// </summary>
public sealed class ListingWatcherService : BackgroundService
{
    private readonly ILogger<ListingWatcherService> _logger;
    private readonly HttpClient _http;
    private readonly ConcurrentDictionary<string, byte> _seen = new();
    private static readonly Regex UsdtSym = new(@"\b([A-Z0-9]{2,15})(?:/|-)?USDTM?\b", RegexOptions.Compiled);
    private static readonly Regex ParenSym = new(@"\(([A-Z0-9]{2,15})\)", RegexOptions.Compiled);
    private readonly Uri _notifyUri;

    public ListingWatcherService(ILogger<ListingWatcherService> logger)
    {
        _logger = logger;

        var notifyUrl = Environment.GetEnvironmentVariable("NEWS_NOTIFY_URL")
            ?? "http://localhost:5005/news";
        _notifyUri = new Uri(notifyUrl);

        _http = new HttpClient(new HttpClientHandler
        {
            AutomaticDecompression = System.Net.DecompressionMethods.All
        })
        {
            Timeout = TimeSpan.FromSeconds(10)
        };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("ListingWatcher/1.0");
        _logger.LogInformation("Sending notifications to {NotifyUrl}", _notifyUri);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PollBybitAsync(stoppingToken);
                await PollKucoinAsync(stoppingToken);
                await PollOkxAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Polling failed");
            }

            try
            {
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }
    }

    private async Task PollBybitAsync(CancellationToken ct)
    {
        var url = "https://api.bybit.com/v5/public/announcements?locale=en-US&category=listing&pageSize=20&page=1";

        using var resp = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        resp.EnsureSuccessStatusCode();

        using var stream = await resp.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        var items = doc.RootElement.GetProperty("result").GetProperty("list");
        foreach (var el in items.EnumerateArray())
        {
            var id = el.TryGetProperty("id", out var pId) ? pId.GetString() ?? Guid.NewGuid().ToString("n") : Guid.NewGuid().ToString("n");
            if (_seen.TryAdd(id, 0))
            {
                var title = el.TryGetProperty("title", out var pTitle) ? pTitle.GetString() ?? "(no title)" : "(no title)";
                var urlItem = el.TryGetProperty("link", out var pUrl) ? pUrl.GetString() : null;
                _logger.LogInformation("Bybit new listing: {Title} {Url}", title, urlItem);
                await NotifyAsync("bybit", id, title, urlItem);
            }
        }
    }

    private async Task PollKucoinAsync(CancellationToken ct)
    {
        var url = "https://api.kucoin.com/api/v3/announcements?annType=new-listings&lang=en_US&pageSize=20&currentPage=1";
        using var resp = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        resp.EnsureSuccessStatusCode();

        using var stream = await resp.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        var items = doc.RootElement.GetProperty("data").GetProperty("items");
        foreach (var el in items.EnumerateArray())
        {
            var id = el.TryGetProperty("annId", out var pId) ? pId.GetInt64().ToString() : Guid.NewGuid().ToString("n");
            if (_seen.TryAdd(id, 0))
            {
                var title = el.TryGetProperty("annTitle", out var pTitle) ? pTitle.GetString() ?? "(no title)" : "(no title)";
                var urlItem = el.TryGetProperty("annUrl", out var pUrl) ? pUrl.GetString() : null;
                _logger.LogInformation("KuCoin new listing: {Title} {Url}", title, urlItem);
                await NotifyAsync("kucoin", id, title, urlItem);
            }
        }
    }

    private async Task PollOkxAsync(CancellationToken ct)
    {
        var url = "https://www.okx.com/api/v5/public/announcements?lang=en-US&category=listing&pageSize=20&page=1";
        using var resp = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        resp.EnsureSuccessStatusCode();

        using var stream = await resp.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        var items = doc.RootElement.GetProperty("data");
        foreach (var el in items.EnumerateArray())
        {
            var id = el.TryGetProperty("id", out var pId) ? pId.GetString() ?? Guid.NewGuid().ToString("n") : Guid.NewGuid().ToString("n");
            if (_seen.TryAdd(id, 0))
            {
                var title = el.TryGetProperty("title", out var pTitle) ? pTitle.GetString() ?? "(no title)" : "(no title)";
                var urlItem = el.TryGetProperty("url", out var pUrl) ? pUrl.GetString() : null;
                _logger.LogInformation("OKX new listing: {Title} {Url}", title, urlItem);
                await NotifyAsync("okx", id, title, urlItem);
            }
        }
    }

    private async Task NotifyAsync(string source, string id, string title, string? url)
    {
        try
        {
            var payload = new ListingNotification(id, source, title, url, ExtractSymbols(title));
            await _http.PostAsJsonAsync(_notifyUri, payload);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Notification failed");
        }
    }

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

    private record ListingNotification(string Id, string Source, string Title, string? Url, IReadOnlyList<string> Symbols);
}
