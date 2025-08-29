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
            }
        }

        public int Leverage { get; set; }
        public string MarginType { get; set; } = string.Empty;

        /// <summary>
        /// Pozisyonun kullandığı başlangıç marjı (USDT).
        /// </summary>
        public decimal InitialMargin { get; set; }

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
