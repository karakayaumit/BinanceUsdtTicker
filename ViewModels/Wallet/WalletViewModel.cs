using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Threading;
using BinanceUsdtTicker.Models;
using BinanceUsdtTicker;

namespace BinanceUsdtTicker.ViewModels.Wallet
{
    public class WalletViewModel
    {
        public ObservableCollection<WalletRow> Items { get; } = new();
        private readonly DispatcherTimer _timer = new();
        private readonly BinanceApiService _api = new();

        public WalletViewModel(UiSettings? settings = null)
        {
            if (settings != null &&
                !string.IsNullOrEmpty(settings.BinanceApiKey) &&
                !string.IsNullOrEmpty(settings.BinanceApiSecret))
            {
                _api.SetApiCredentials(settings.BinanceApiKey, settings.BinanceApiSecret);
            }
            else
            {
                var apiKey = Environment.GetEnvironmentVariable("BINANCE_API_KEY") ?? string.Empty;
                var secret = Environment.GetEnvironmentVariable("BINANCE_API_SECRET") ?? string.Empty;
                if (!string.IsNullOrEmpty(apiKey) && !string.IsNullOrEmpty(secret))
                    _api.SetApiCredentials(apiKey, secret);
            }

            _timer.Interval = TimeSpan.FromMilliseconds(1000);
            _timer.Tick += async (s, e) => await UpdateFromFuturesAsync();
            _timer.Start();
        }

        public async Task UpdateFromFuturesAsync()
        {
            decimal balance = 0m, available = 0m, used = 0m;
            try
            {
                (balance, available) = await _api.GetUsdtWalletBalanceAsync();
            }
            catch
            {
            }

            try
            {
                var accountUsed = await _api.GetUsedMarginAsync();
                used = accountUsed;
                if (used == 0m)
                    used = balance - available;
            }
            catch
            {
                used = balance - available;
            }

            ApplySnapshot(balance, available, used);
        }

        public void ApplySnapshot(decimal balance, decimal available, decimal used)
        {
            var row = Items.FirstOrDefault() ?? new WalletRow { Asset = "USDT" };
            row.Balance = balance;
            row.Available = available;
            row.Used = used;
            if (!Items.Any())
                Items.Add(row);
        }
    }
}
