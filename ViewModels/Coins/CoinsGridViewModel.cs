using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Threading;

namespace BinanceUsdtTicker.ViewModels.Coins
{
    public class CoinsGridViewModel
    {
        public ObservableCollection<TickerRow> Items { get; } = new();
        private readonly BinanceFuturesService _service = new();
        private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;

        public CoinsGridViewModel()
        {
            _service.OnTickersUpdated += OnServiceTickersUpdated;
            _ = _service.StartAsync();
        }

        private void OnServiceTickersUpdated(System.Collections.Generic.List<TickerRow> list)
        {
            if (_dispatcher.HasShutdownStarted || _dispatcher.HasShutdownFinished)
                return;

            _ = _dispatcher.InvokeAsync(() =>
            {
                foreach (var u in list)
                {
                    var existing = Items.FirstOrDefault(x => x.Symbol == u.Symbol);
                    if (existing == null)
                    {
                        Items.Add(u);
                    }
                    else
                    {
                        existing.Price = u.Price;
                        existing.ChangePct = u.ChangePct;
                        existing.Volume = u.Volume;
                    }
                }
            }, DispatcherPriority.Background);
        }
    }
}
