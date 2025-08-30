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
        await _newsHub.StartAsync();
        _newsHub.NewsReceived += OnNewsReceived;
    }

    private void OnNewsReceived(object? sender, NewsItem item)
    {
        // TODO: handle incoming news items (e.g., update UI or log)
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_newsHub != null)
            await _newsHub.DisposeAsync();
        base.OnExit(e);
    }
}
