using System;
using System.Windows;

namespace BinanceUsdtTicker;

public partial class App : Application
{
    private FreeNewsHubService? _newsHub;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _newsHub = new FreeNewsHubService(new FreeNewsOptions
        {
            PollInterval = TimeSpan.FromSeconds(5),
            CryptoPanicToken = string.Empty
        });
        _newsHub.NewsReceived += OnNewsReceived;
        await _newsHub.StartAsync();
    }

    private void OnNewsReceived(object? sender, NewsItem item)
    {
        if (Current.Dispatcher.CheckAccess())
        {
            if (Current.MainWindow is MainWindow mw)
                mw.AddNewsItem(item);
        }
        else
        {
            _ = Current.Dispatcher.InvokeAsync(() =>
            {
                if (Current.MainWindow is MainWindow mw)
                    mw.AddNewsItem(item);
            });
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_newsHub != null)
            await _newsHub.DisposeAsync();
        base.OnExit(e);
    }
}
