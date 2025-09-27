using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using BinanceUsdtTicker;

namespace BinanceUsdtTicker.ViewModels.Coins
{
    public class CoinsGridViewModel : INotifyPropertyChanged
    {
        private readonly Dictionary<string, TickerRow> _bySymbol = new(StringComparer.OrdinalIgnoreCase);
        private ObservableCollection<TickerRow> _items = new();

        public CoinsGridViewModel()
        {
            Items = new ObservableCollection<TickerRow>();

#if DEBUG
            EnsureDebugItems();
#endif
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public ObservableCollection<TickerRow> Items
        {
            get => _items;
            private set
            {
                if (ReferenceEquals(_items, value))
                    return;

                if (_items != null)
                    _items.CollectionChanged -= Items_CollectionChanged;

                _items = value;
                _items.CollectionChanged += Items_CollectionChanged;

                RebuildMap();
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Items)));
            }
        }

        public void SetItems(ObservableCollection<TickerRow> items)
        {
            Items = items ?? throw new ArgumentNullException(nameof(items));
        }

        public void ApplyTick(TickerUpdate update)
        {
            if (string.IsNullOrWhiteSpace(update.Symbol))
                return;

            if (!_bySymbol.TryGetValue(update.Symbol, out var row))
            {
                row = Add(update.Symbol);
            }

            row.Price = update.Price;
            row.ChangePct = update.ChangePct;
            row.Volume = update.Volume;

            if (update.Open.HasValue)
                row.Open = update.Open.Value;
            if (update.High.HasValue)
                row.High = update.High.Value;
            if (update.Low.HasValue)
                row.Low = update.Low.Value;
            if (update.LastUpdate.HasValue)
                row.LastUpdate = update.LastUpdate.Value;
            if (update.BaselinePrice.HasValue && !row.BaselinePrice.HasValue)
                row.BaselinePrice = update.BaselinePrice.Value;
            if (!row.BaselinePrice.HasValue && update.Price > 0m)
                row.BaselinePrice = update.Price;
        }

        private TickerRow Add(string symbol)
        {
            var row = new TickerRow { Symbol = symbol };
            _items.Add(row);
            return row;
        }

        private void Items_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                RebuildMap();
                return;
            }

            if (e.NewItems != null)
            {
                foreach (TickerRow row in e.NewItems)
                {
                    _bySymbol[row.Symbol] = row;
                }
            }

            if (e.OldItems != null)
            {
                foreach (TickerRow row in e.OldItems)
                {
                    if (_bySymbol.TryGetValue(row.Symbol, out var existing) && ReferenceEquals(existing, row))
                        _bySymbol.Remove(row.Symbol);
                }
            }
        }

        private void RebuildMap()
        {
            _bySymbol.Clear();
            foreach (var row in _items)
                _bySymbol[row.Symbol] = row;
        }

#if DEBUG
        private void EnsureDebugItems()
        {
            if (_items.Count > 0)
                return;

            var samples = new[]
            {
                new TickerRow { Symbol = "BTCUSDT", Price = 65000m, ChangePct = 0.012, Volume = 1234m },
                new TickerRow { Symbol = "ETHUSDT", Price = 3200m, ChangePct = -0.008, Volume = 845m },
                new TickerRow { Symbol = "BNBUSDT", Price = 580m, ChangePct = 0.015, Volume = 210m },
                new TickerRow { Symbol = "ADAUSDT", Price = 0.45m, ChangePct = -0.022, Volume = 53400m },
                new TickerRow { Symbol = "SOLUSDT", Price = 155m, ChangePct = 0.034, Volume = 1890m }
            };

            foreach (var row in samples)
            {
                row.BaselinePrice = row.Price;
                _items.Add(row);
            }
        }
#endif
    }
}
