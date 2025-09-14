using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace BinanceUsdtTicker.Models
{
    public class WalletRow : INotifyPropertyChanged
    {
        string _asset = "";
        decimal _balance, _available, _used;

        public string Asset
        {
            get => _asset;
            set { if (_asset != value) { _asset = value; OnChanged(); } }
        }

        public decimal Balance
        {
            get => _balance;
            set { if (_balance != value) { _balance = value; OnChanged(); } }
        }

        public decimal Available
        {
            get => _available;
            set { if (_available != value) { _available = value; OnChanged(); } }
        }

        public decimal Used
        {
            get => _used;
            set { if (_used != value) { _used = value; OnChanged(); } }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        void OnChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
