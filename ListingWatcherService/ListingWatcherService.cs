using System.Collections.Concurrent;
using System.Text.Json;
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

    public ListingWatcherService(ILogger<ListingWatcherService> logger)
    {
        _logger = logger;
        _http = new HttpClient(new HttpClientHandler
        {
            AutomaticDecompression = System.Net.DecompressionMethods.All
        })
        {
            Timeout = TimeSpan.FromSeconds(10)
        };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("ListingWatcher/1.0");
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
            }
        }
    }
}
