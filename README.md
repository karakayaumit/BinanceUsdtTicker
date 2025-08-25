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
