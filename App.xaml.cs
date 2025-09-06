using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using System.Text.Json;

namespace BinanceUsdtTicker;

public partial class App : Application
{
    private FreeNewsHubService? _newsHub;
    private readonly FreeNewsOptions _newsOptions = new();
    private readonly HashSet<string> _usdtSymbols = new(StringComparer.OrdinalIgnoreCase);
    private WebApplication? _listingApp;
    private static readonly string SymbolsFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "BinanceUsdtTicker", "usdt_symbols.txt");

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        Dispatcher.InvokeAsync(StartNewsHubAsync, DispatcherPriority.ApplicationIdle);
        Dispatcher.InvokeAsync(StartListingListenerAsync, DispatcherPriority.ApplicationIdle);
    }

    private async Task StartNewsHubAsync()
    {
        await UpdateUsdtSymbolsFileAsync();

        // Use the fully qualified type name to avoid resolving to the
        // Application.MainWindow property which is of type Window and
        // does not expose LoadDefaultUiSettings().
        var settings = global::BinanceUsdtTicker.MainWindow.LoadDefaultUiSettings();
        _newsOptions.PollInterval = TimeSpan.FromSeconds(5);
        _newsOptions.CryptoPanicToken = string.Empty;
        _newsOptions.RssBaseUrl = string.IsNullOrWhiteSpace(settings.BaseUrl)
            ? "http://localhost:5005"
            : settings.BaseUrl;

        _newsHub = new FreeNewsHubService(_newsOptions);

        _newsHub.NewsReceived += OnNewsReceived;
        await _newsHub.StartAsync();
    }

    private async Task StartListingListenerAsync()
    {
        var builder = WebApplication.CreateBuilder(Array.Empty<string>());
        builder.Logging.ClearProviders();

        var listenUrl = Environment.GetEnvironmentVariable("NEWS_LISTEN_URL")
            ?? "http://localhost:5005";
        builder.WebHost.UseUrls(listenUrl);
        var app = builder.Build();

        app.MapPost("/news", async (HttpContext ctx) =>
        {
            ListingNotification? payload;
            try
            {
                payload = await ctx.Request.ReadFromJsonAsync<ListingNotification>();
            }
            catch (JsonException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }

            if (payload != null)
            {
                var item = new NewsItem(
                    id: payload.Id,
                    source: payload.Source,
                    timestamp: DateTime.UtcNow,
                    title: payload.Title,
                    body: null,
                    link: payload.Url ?? string.Empty,
                    type: NewsType.Listing,
                    symbols: payload.Symbols ?? Array.Empty<string>());

                if (Current.MainWindow is MainWindow mw)
                    mw.AddNewsItem(item);
            }
            return Results.Ok();
        });

        _listingApp = app;
        await app.StartAsync();
    }

    internal void UpdateNewsBaseUrl(string? baseUrl)
    {
        _newsOptions.RssBaseUrl = baseUrl;
    }

    private async Task UpdateUsdtSymbolsFileAsync()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SymbolsFile)!);
            var api = new BinanceApiService();
            var info = await api.GetExchangeInfoAsync();
            var symbols = info.Symbols
                .Select(s => s.Symbol)
                .Where(s => s.EndsWith("USDT", StringComparison.OrdinalIgnoreCase))
                .OrderBy(s => s)
                .ToList();

            await File.WriteAllLinesAsync(SymbolsFile, symbols);
            _usdtSymbols.Clear();
            foreach (var s in File.ReadLines(SymbolsFile))
                _usdtSymbols.Add(s);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to update USDT symbols: {ex}");
        }
    }

    private void OnNewsReceived(object? sender, NewsItem item)
    {
        foreach (var sym in item.Symbols)
        {
            if (!_usdtSymbols.Contains(sym))
                continue;

            var singleItem = new NewsItem(
                id: item.Id + "::" + sym,
                source: item.Source,
                timestamp: item.Timestamp,
                title: item.Title,
                body: item.Body,
                link: item.Link,
                type: item.Type,
                symbols: new[] { sym });

            if (Current.Dispatcher.CheckAccess())
            {
                if (Current.MainWindow is MainWindow mw)
                    mw.AddNewsItem(singleItem);
            }
            else
            {
                _ = Current.Dispatcher.InvokeAsync(() =>
                {
                    if (Current.MainWindow is MainWindow mw)
                        mw.AddNewsItem(singleItem);
                });
            }
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_newsHub != null)
            await _newsHub.DisposeAsync();
        if (_listingApp != null)
            await _listingApp.StopAsync();
        base.OnExit(e);
    }

    public record ListingNotification(string Id, string Source, string Title, string? Url, string[]? Symbols);
}
