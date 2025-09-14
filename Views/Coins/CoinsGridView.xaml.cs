using System.Windows.Controls;
using BinanceUsdtTicker.ViewModels.Coins;

namespace BinanceUsdtTicker.Views.Coins
{
    public partial class CoinsGridView : UserControl
    {
        public CoinsGridView()
        {
            InitializeComponent();
            DataContext = new CoinsGridViewModel();
        }
    }
}
