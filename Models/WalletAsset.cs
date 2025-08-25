using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace BinanceUsdtTicker.Models
{
    public class WalletAsset : INotifyPropertyChanged
    {
        private string _asset = string.Empty;
        public string Asset
        {
            get => _asset;
            set { if (_asset != value) { _asset = value; OnPropertyChanged(); } }
        }

        private decimal _balance;
        public decimal Balance
        {
            get => _balance;
            set { if (_balance != value) { _balance = value; OnPropertyChanged(); } }
        }

        private decimal _available;
        public decimal Available
        {
            get => _available;
            set { if (_available != value) { _available = value; OnPropertyChanged(); } }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
