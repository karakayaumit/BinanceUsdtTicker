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
