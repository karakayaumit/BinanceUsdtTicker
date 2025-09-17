using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using BinanceUsdtTicker.ViewModels.Coins;
using BinanceUsdtTicker;
using DevExpress.Xpf.Grid;

namespace BinanceUsdtTicker.Views.Coins
{
    public partial class CoinsGridView : UserControl
    {
        public CoinsGridView()
        {
            InitializeComponent();

            ViewModel = new CoinsGridViewModel(CoinsGrid);
            DataContext = ViewModel;

            var view = (TableView)CoinsGrid.View;
            view.CellValueChanged += (_, e) =>
            {
                if (e.Column.FieldName == nameof(TickerRow.IsFavorite))
                {
                    var item = CoinsGrid.GetRow(e.RowHandle);
                    var fix = (bool?)e.Value == true ? FixedRowPosition.Top : FixedRowPosition.None;
                    view.FixItem(item, fix);
                }
            };
            view.FocusedRowChanged += (_, __) => SelectedItemChanged?.Invoke(this, SelectedItem);

            Loaded += (_, __) =>
            {
                Debug.WriteLine($"[DEVEXPRESS] CoinsGrid loaded. Columns={CoinsGrid.Columns.Count}");
                SelectedItemChanged?.Invoke(this, SelectedItem);
            };
        }

        public CoinsGridViewModel ViewModel { get; }

        public GridControl GridControl => CoinsGrid;

        public event EventHandler<TickerRow?>? SelectedItemChanged;

        public TickerRow? SelectedItem => CoinsGrid.SelectedItem as TickerRow;

        public void BindItems(ObservableCollection<TickerRow> items)
        {
            ViewModel.SetItems(items);
        }
    }
}
