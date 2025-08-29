using System;
using System.ComponentModel;
using System.Windows.Media;

namespace BinanceUsdtTicker.Models
{
    /// <summary>
    /// Temel vadeli işlem pozisyon bilgisi.
    /// </summary>
    public class FuturesPosition : INotifyPropertyChanged
    {
        public string Symbol { get; set; } = string.Empty;
        public decimal PositionAmt { get; set; }
        public decimal EntryPrice { get; set; }

        private decimal _unrealizedPnl;
        public decimal UnrealizedPnl
        {
            get => _unrealizedPnl;
            set
            {
                if (_unrealizedPnl == value) return;
                _unrealizedPnl = value;
                OnPropertyChanged(nameof(UnrealizedPnl));
                OnPropertyChanged(nameof(PnlBrush));
                OnPropertyChanged(nameof(RoiPercent));
            }
        }

        public int Leverage { get; set; }
        public string MarginType { get; set; } = string.Empty;

        /// <summary>
        /// Pozisyonun kullandığı başlangıç marjı (USDT).
        /// </summary>
        private decimal _initialMargin;
        public decimal InitialMargin
        {
            get => _initialMargin;
            set
            {
                if (_initialMargin == value) return;
                _initialMargin = value;
                OnPropertyChanged(nameof(InitialMargin));
                OnPropertyChanged(nameof(RoiPercent));
            }
        }

        private decimal _markPrice;
        /// <summary>
        /// Pozisyon için mevcut anlık mark fiyatı.
        /// </summary>
        public decimal MarkPrice
        {
            get => _markPrice;
            set
            {
                if (_markPrice == value) return;
                _markPrice = value;
                OnPropertyChanged(nameof(MarkPrice));
            }
        }

        private decimal _liquidationPrice;
        /// <summary>
        /// Pozisyonun tasfiye fiyatı.
        /// </summary>
        public decimal LiquidationPrice
        {
            get => _liquidationPrice;
            set
            {
                if (_liquidationPrice == value) return;
                _liquidationPrice = value;
                OnPropertyChanged(nameof(LiquidationPrice));
            }
        }

        /// <summary>
        /// Pozisyonun Yatırım Getiri yüzdesi.
        /// </summary>
        public decimal RoiPercent =>
            _initialMargin != 0m ? _unrealizedPnl / _initialMargin * 100m : 0m;

        private decimal? _closeLimitPrice;
        /// <summary>
        /// Kapatma işlemi için kullanıcı tarafından girilen limit fiyatı.
        /// </summary>
        public decimal? CloseLimitPrice
        {
            get => _closeLimitPrice;
            set
            {
                if (_closeLimitPrice == value) return;
                _closeLimitPrice = value;
                OnPropertyChanged(nameof(CloseLimitPrice));
            }
        }

        public Brush PnlBrush =>
            _unrealizedPnl >= 0m ? Brushes.ForestGreen : Brushes.Red;

        public string BaseSymbol =>
            Symbol.EndsWith("USDT", StringComparison.OrdinalIgnoreCase)
                ? Symbol[..^4]
                : Symbol;

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
