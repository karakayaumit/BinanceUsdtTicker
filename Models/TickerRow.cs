using System;
namespace BinanceUsdtTicker
{ 
    public class TickerRow : BindableBase
    {
        public string Symbol { get; set; } = string.Empty;

        public string BaseSymbol => Symbol.ToBaseSymbol();

        private decimal _price;
        public decimal Price
        {
            get => _price;
            set
            {
                if (_price == value) return;
                _price = value;
                OnPropertyChanged(nameof(Price));
                RecomputeChangeSinceStart();
            }
        }

        private decimal _open;
        public decimal Open
        {
            get => _open;
            set { if (_open == value) return; _open = value; OnPropertyChanged(nameof(Open)); }
        }

        private decimal _high;
        public decimal High
        {
            get => _high;
            set { if (_high == value) return; _high = value; OnPropertyChanged(nameof(High)); }
        }

        private decimal _low;
        public decimal Low
        {
            get => _low;
            set { if (_low == value) return; _low = value; OnPropertyChanged(nameof(Low)); }
        }

        private decimal _volume;
        public decimal Volume
        {
            get => _volume;
            set { if (_volume == value) return; _volume = value; OnPropertyChanged(nameof(Volume)); }
        }

        // 24s değişim %
        private decimal _changePercent;
        public decimal ChangePercent
        {
            get => _changePercent;
            set { if (_changePercent == value) return; _changePercent = value; OnPropertyChanged(nameof(ChangePercent)); }
        }

        private DateTime _lastUpdate;
        public DateTime LastUpdate
        {
            get => _lastUpdate;
            set
            {
                if (_lastUpdate == value) return;
                _lastUpdate = value;
                OnPropertyChanged(nameof(LastUpdate));
                OnPropertyChanged(nameof(LastUpdateLocal));
            }
        }

        public string LastUpdateLocal =>
            LastUpdate == default ? string.Empty : LastUpdate.ToLocalTime().ToString("HH:mm:ss");

        private decimal? _baselinePrice;
        public decimal? BaselinePrice
        {
            get => _baselinePrice;
            set
            {
                if (_baselinePrice == value) return;
                _baselinePrice = value;
                OnPropertyChanged(nameof(BaselinePrice));
                RecomputeChangeSinceStart();
            }
        }

        private decimal _changeSinceStartPercent;
        public decimal ChangeSinceStartPercent
        {
            get => _changeSinceStartPercent;
            private set
            {
                if (_changeSinceStartPercent == value) return;
                _changeSinceStartPercent = value;
                OnPropertyChanged(nameof(ChangeSinceStartPercent));
                // Türetilmiş bayraklar için de bildirim
                OnPropertyChanged(nameof(IsMove1Plus));
                OnPropertyChanged(nameof(IsMove3Plus));
                OnPropertyChanged(nameof(IsMoveMinus1));
                OnPropertyChanged(nameof(IsMoveMinus3));
            }
        }

        // Favori
        private bool _isFavorite;
        public bool IsFavorite
        {
            get => _isFavorite;
            set { if (_isFavorite == value) return; _isFavorite = value; OnPropertyChanged(nameof(IsFavorite)); }
        }

        // --- Renk bayrakları (XAML bunlara bağlı) ---
        public bool IsMove1Plus => ChangeSinceStartPercent >= 1m && ChangeSinceStartPercent < 3m;
        public bool IsMove3Plus => ChangeSinceStartPercent >= 3m;
        public bool IsMoveMinus1 => ChangeSinceStartPercent <= -1m && ChangeSinceStartPercent > -3m;
        public bool IsMoveMinus3 => ChangeSinceStartPercent <= -3m;

        private void RecomputeChangeSinceStart()
        {
            if (BaselinePrice is decimal b && b > 0m)
            {
                ChangeSinceStartPercent = (Price - b) / b * 100m;
            }
            else
            {
                ChangeSinceStartPercent = 0m;
            }
        }
    }
}
