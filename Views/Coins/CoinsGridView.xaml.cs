using System.Windows.Controls;
using DevExpress.Xpf.Grid;
using BinanceUsdtTicker.ViewModels.Coins;

namespace BinanceUsdtTicker.Views.Coins
{
    public partial class CoinsGridView : UserControl
    {
        public CoinsGridView()
        {
            InitializeComponent();
            DataContext = new CoinsGridViewModel(CoinsGrid);
            var view = (TableView)CoinsGrid.View;
            view.CellValueChanged += (s, e) =>
            {
                if (e.Column.FieldName == "IsFavorite")
                {
                    var item = CoinsGrid.GetRow(e.RowHandle);
                    var pos = (bool?)e.Value == true ? FixedRowPosition.Top : FixedRowPosition.None;
                    view.FixItem(item, pos);
                }
            };
        }
    }
}
