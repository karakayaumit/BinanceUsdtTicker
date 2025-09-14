using System.Windows.Controls;
using BinanceUsdtTicker.ViewModels.Wallet;

namespace BinanceUsdtTicker.Views.Wallet
{
    public partial class WalletView : UserControl
    {
        public WalletView()
        {
            InitializeComponent();
            DataContext = new WalletViewModel(WalletGrid);
        }
    }
}
