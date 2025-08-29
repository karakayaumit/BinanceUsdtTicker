using System;
using System.ComponentModel;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using BinanceUsdtTicker.Trading;

namespace BinanceUsdtTicker.ViewModels.Trading
{
    public class OrderPreviewViewModel : INotifyPropertyChanged
    {
        private string _symbol = "BTCUSDT";
        private string _orderType = "Market";
        private string _side = "Buy";
        private string _margin = "Cross";
        private string _posSide = "OneWay";
        private int _leverage = 10;
        private decimal _walletPercent = 0.25m;
        private decimal? _limitPrice;
        private decimal _slipBps = 10m;

        public string Symbol { get => _symbol; set { _symbol = value; OnPropertyChanged(); } }
        public string OrderType { get => _orderType; set { _orderType = value; OnPropertyChanged(); } }
        public string Side { get => _side; set { _side = value; OnPropertyChanged(); } }
        public string MarginMode { get => _margin; set { _margin = value; OnPropertyChanged(); } }
        public string PositionSide { get => _posSide; set { _posSide = value; OnPropertyChanged(); } }
        public int Leverage { get => _leverage; set { _leverage = value; OnPropertyChanged(); } }
        public decimal WalletPercent { get => _walletPercent; set { _walletPercent = value; OnPropertyChanged(); } }
        public decimal? LimitPrice { get => _limitPrice; set { _limitPrice = value; OnPropertyChanged(); } }
        public decimal MarketSlippageBps { get => _slipBps; set { _slipBps = value; OnPropertyChanged(); } }
        public bool IsLimit => OrderType == "Limit";

        private OrderPreview? _preview;
        public OrderPreview? Preview { get => _preview; set { _preview = value; OnPropertyChanged(); } }

        private readonly OrderPreviewService _service;
        public ICommand RecalcCommand { get; }

        public OrderPreviewViewModel(string apiKey, string apiSecret)
        {
            var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            var client = new BinanceApiService(http);
            client.SetApiCredentials(apiKey, apiSecret);
            _service = new OrderPreviewService(client);
            RecalcCommand = new AsyncCommand(RecalcAsync);
        }

        private async Task RecalcAsync()
        {
            try
            {
                var req = new OrderPreviewRequest(
                    Symbol,
                    OrderType == "Market" ? BinanceUsdtTicker.Trading.OrderType.Market : BinanceUsdtTicker.Trading.OrderType.Limit,
                    Side == "Buy" ? OrderSide.Buy : OrderSide.Sell,
                    PositionSide == "Long" ? BinanceUsdtTicker.Trading.PositionSide.Long :
                        PositionSide == "Short" ? BinanceUsdtTicker.Trading.PositionSide.Short : BinanceUsdtTicker.Trading.PositionSide.OneWay,
                    MarginMode == "Isolated" ? BinanceUsdtTicker.Trading.MarginMode.Isolated : BinanceUsdtTicker.Trading.MarginMode.Cross,
                    Leverage,
                    WalletPercent,
                    LimitPrice,
                    MarketSlippageBps
                );

                Preview = await _service.ComputeAsync(req, CancellationToken.None);
            }
            catch (Exception ex)
            {
                Preview = new OrderPreview
                {
                    Symbol = Symbol,
                    Warnings = new() { ex.Message }
                };
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public sealed class AsyncCommand : ICommand
    {
        private readonly Func<Task> _handler;
        private bool _busy;
        public AsyncCommand(Func<Task> handler) => _handler = handler;
        public bool CanExecute(object? parameter) => !_busy;
        public event EventHandler? CanExecuteChanged;
        public async void Execute(object? parameter)
        {
            if (_busy) return; _busy = True(); CanExecuteChanged?.Invoke(this, EventArgs.Empty);
            try { await _handler(); } finally { _busy = False(); CanExecuteChanged?.Invoke(this, EventArgs.Empty); }
        }
        private bool True() => true;
        private bool False() => false;
    }
}
