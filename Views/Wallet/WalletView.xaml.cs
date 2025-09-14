using System.Windows.Controls;
using BinanceUsdtTicker.ViewModels.Wallet;
using BinanceUsdtTicker.Models;
using BinanceUsdtTicker;

namespace BinanceUsdtTicker.Views.Wallet
{
    public partial class WalletView : UserControl
    {
        public WalletView()
        {
            InitializeComponent();
            UiSettings settings = MainWindow.LoadDefaultUiSettings();
            DataContext = new WalletViewModel(settings);
        }
    }
}
