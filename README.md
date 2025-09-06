# Binance USDT Canlı Fiyat Gösterici (WPF, .NET 8)

Bu uygulama, Binance **USDT futures** borsasındaki aktif paritelerin anlık fiyatlarını gösterir.
- WebSocket üzerinden `wss://fstream.binance.com/stream?streams=!miniTicker@arr/!bookTicker@arr` akışına bağlanır.
- Yalnızca USDT paritelerini filtreleyip listeler.
- Arama kutusu ve pozitif değişim filtresi vardır.

## Kurulum / Çalıştırma

1. **.NET 8 SDK** kurulu olmalı.  
2. Repo’yu klonla veya indir:  
   ```bash
    git clone https://github.com/<kullanici-adi>/BinanceUsdtTicker.git
    cd BinanceUsdtTicker
   ```

## RSS New Listings Feeds

This repository includes a minimal API script (`KuCoinNewListingsRss.cs`) that aggregates
new listing announcements from multiple exchanges and exposes them as RSS feeds.

Run the script with the .NET CLI:

```bash
dotnet run KuCoinNewListingsRss.cs
```

Then access the feeds:

- http://localhost:5000/rss/kucoin-new
- http://localhost:5000/rss/bybit-new
- http://localhost:5000/rss/okx-new

To consume these feeds in the main application, set `FreeNewsOptions.RssBaseUrl`
to the base address of the running script. This setting is empty by default, so
the app skips querying the RSS endpoints and no connection attempts are made
until you provide a valid URL in the settings window.

## Listing Watcher Windows Service

The `ListingWatcherService` project polls several exchange announcement APIs
and sends new listings to the main application via HTTP. By default the service
posts updates to `http://localhost:5005/news`, which is where the desktop app
listens for incoming items.  When running the service on another machine, set
the destination with the `NEWS_NOTIFY_URL` environment variable:

```bash
set NEWS_NOTIFY_URL=http://app-host:5005/news   # Windows
export NEWS_NOTIFY_URL=http://app-host:5005/news # Linux/macOS
```

If the desktop application itself needs to accept connections from other
machines, configure its listener address with `NEWS_LISTEN_URL` before
launching the app. For example, to listen on all interfaces:

```bash
set NEWS_LISTEN_URL=http://0.0.0.0:5005   # Windows
export NEWS_LISTEN_URL=http://0.0.0.0:5005 # Linux/macOS
```

You can run the service as a console app for testing:

```bash
cd ListingWatcherService
dotnet run
```

Install it as a Windows service with `sc create` or other tooling as needed.
