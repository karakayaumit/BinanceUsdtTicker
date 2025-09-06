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
to the base address of the running script. The desktop app now defaults to
`http://localhost:5005`, matching the feed script's default port. Adjust this
value from the settings window if your service runs elsewhere.

## Listing Watcher Windows Service

The `ListingWatcherService` project polls several exchange announcement APIs
and writes new listings directly to a SQL database. Configure the database
connection string with the `BINANCE_DB_CONNECTION` environment variable;
otherwise it connects to `KARAKAYA-MSI\\KARAKAYADB` using the SQL login
`sa` with password `Lhya!812`. If you also set `CRYPTO_PANIC_TOKEN` the
service will poll CryptoPanic for general news and store those items in the
same table.

You can run the service as a console app for testing:

```bash
cd ListingWatcherService
dotnet run
```

Install it as a Windows service with `sc create` or other tooling as needed.
