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
using BinanceUsdtTicker.Models;

namespace BinanceUsdtTicker;

public partial class App : Application
{
    private FreeNewsHubService? _freeNewsHub;
    private NewsDbService? _newsHub;
    private readonly ISymbolExtractor _symbolExtractor = new RegexSymbolExtractor();
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
        var settings = LoadUiSettings();
        await UpdateNewsBaseUrl(settings.BaseUrl);
        await UpdateDbConnectionAsync(settings);
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
                    titleTranslate: null,
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

    internal async Task UpdateNewsBaseUrl(string? baseUrl)
    {
        if (_freeNewsHub != null)
        {
            await _freeNewsHub.DisposeAsync();
            _freeNewsHub.NewsReceived -= OnNewsReceived;
            _freeNewsHub = null;
        }

        if (!string.IsNullOrWhiteSpace(baseUrl))
        {
            var opts = new FreeNewsOptions { RssBaseUrl = baseUrl };
            _freeNewsHub = new FreeNewsHubService(opts, _symbolExtractor);
            _freeNewsHub.NewsReceived += OnNewsReceived;
            await _freeNewsHub.StartAsync();
        }
    }

    private static UiSettings LoadUiSettings()
    {
        try
        {
            var appDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "BinanceUsdtTicker");
            var settingsPath = Path.Combine(appDir, "ui_settings.json");
            if (!File.Exists(settingsPath))
                settingsPath = Path.Combine(appDir, "ui_defaults.json");
            if (File.Exists(settingsPath))
            {
                var json = File.ReadAllText(settingsPath);
                return JsonSerializer.Deserialize<UiSettings>(json) ?? new UiSettings();
            }
        }
        catch { }
        return new UiSettings();
    }

    internal async Task UpdateDbConnectionAsync(UiSettings settings)
    {
        if (_newsHub != null)
        {
            await _newsHub.DisposeAsync();
            _newsHub = null;
        }

        var cs = settings.GetConnectionString();
        if (!string.IsNullOrEmpty(cs))
        {
            _newsHub = new NewsDbService(cs, TimeSpan.FromSeconds(5));
            _newsHub.NewsReceived += OnNewsReceived;
            await _newsHub.StartAsync();
        }
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
                titleTranslate: item.TitleTranslate,
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
        if (_freeNewsHub != null)
            await _freeNewsHub.DisposeAsync();
        if (_newsHub != null)
            await _newsHub.DisposeAsync();
        if (_listingApp != null)
            await _listingApp.StopAsync();
        base.OnExit(e);
    }

    public record ListingNotification(string Id, string Source, string Title, string? Url, string[]? Symbols);
}
