using System;
using System.Windows.Media;

namespace BinanceUsdtTicker.Models
{
    /// <summary>
    /// Temel vadeli işlem pozisyon bilgisi.
    /// </summary>
    public class FuturesPosition : BindableBase
    {
        public string Symbol { get; set; } = string.Empty;
        /// <summary>
        /// Position side reported by Binance (BOTH/LONG/SHORT).
        /// </summary>
        public string PositionSide { get; set; } = string.Empty;
        private decimal _positionAmt;
        public decimal PositionAmt
        {
            get => _positionAmt;
            set
            {
                if (_positionAmt == value) return;
                _positionAmt = value;
                OnPropertyChanged(nameof(PositionAmt));
                OnPropertyChanged(nameof(PositionSize));
                OnPropertyChanged(nameof(SideBrush));
            }
        }
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
        /// Pozisyonun girişte kullanılan tutarı (USDT).
        /// </summary>
        private decimal _entryAmount;
        public decimal EntryAmount
        {
            get => _entryAmount;
            set
            {
                if (_entryAmount == value) return;
                _entryAmount = value;
                OnPropertyChanged(nameof(EntryAmount));
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
                OnPropertyChanged(nameof(PositionSize));
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
        /// Pozisyon boyutu (notional değeri).
        /// </summary>
        public decimal PositionSize => Math.Abs(_markPrice * _positionAmt);

        /// <summary>
        /// Pozisyonun Yatırım Getiri yüzdesi.
        /// </summary>
        public decimal RoiPercent =>
            _entryAmount != 0m ? _unrealizedPnl / _entryAmount * 100m : 0m;

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

        /// <summary>
        /// Pozisyon yönüne göre sembol rengi.
        /// </summary>
        public Brush SideBrush =>
            _positionAmt >= 0m ? Brushes.Green : Brushes.Red;

        public string BaseSymbol => Symbol.ToBaseSymbol();
    }
}
