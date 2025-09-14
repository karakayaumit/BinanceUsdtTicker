namespace BinanceUsdtTicker.Models
{
    public class WalletAsset : BindableBase
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

        private decimal _used;
        public decimal Used
        {
            get => _used;
            set { if (_used != value) { _used = value; OnPropertyChanged(); } }
        }

    }
}
