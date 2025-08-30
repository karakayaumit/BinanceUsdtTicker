using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace BinanceUsdtTicker;

public partial class App : Application
{
    private FreeNewsHubService? _newsHub;
    private readonly HashSet<string> _usdtSymbols = new(StringComparer.OrdinalIgnoreCase);
    private static readonly string SymbolsFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "BinanceUsdtTicker", "usdt_symbols.txt");

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        Dispatcher.InvokeAsync(StartNewsHubAsync, DispatcherPriority.ApplicationIdle);
    }

    private async Task StartNewsHubAsync()
    {
        await UpdateUsdtSymbolsFileAsync();

        _newsHub = new FreeNewsHubService(new FreeNewsOptions
        {
            PollInterval = TimeSpan.FromSeconds(5),
            CryptoPanicToken = string.Empty
        });
        _newsHub.NewsReceived += OnNewsReceived;
        await _newsHub.StartAsync();
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
        base.OnExit(e);
    }
}
