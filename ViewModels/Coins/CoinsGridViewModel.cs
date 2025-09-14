using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Windows.Threading;
using DevExpress.Xpf.Grid;

namespace BinanceUsdtTicker.ViewModels.Coins
{
    public class CoinsGridViewModel
    {
        public ObservableCollection<TickerRow> Items { get; } = new();
        private readonly GridControl _grid;
        private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(150) };
        private readonly BinanceFuturesService _service = new();
        private int _pending;

        public CoinsGridViewModel(GridControl grid)
        {
            _grid = grid;
            _timer.Tick += (_, __) =>
            {
                if (Interlocked.Exchange(ref _pending, 0) > 0)
                    _grid.RefreshData();
            };
            _timer.Start();

            _service.OnTickersUpdated += OnServiceTickersUpdated;
            _ = _service.StartAsync();
        }

        private void OnServiceTickersUpdated(System.Collections.Generic.List<TickerRow> list)
        {
            if (_grid.Dispatcher.HasShutdownStarted || _grid.Dispatcher.HasShutdownFinished)
                return;

            _ = _grid.Dispatcher.InvokeAsync(() =>
            {
                _grid.BeginDataUpdate();
                try
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
                }
                finally
                {
                    _grid.EndDataUpdate();
                }
                Interlocked.Exchange(ref _pending, 1);
            }, DispatcherPriority.Background);
        }
    }
}
