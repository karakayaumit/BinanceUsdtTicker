using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using BinanceUsdtTicker.ViewModels.Coins;
using BinanceUsdtTicker;

namespace BinanceUsdtTicker.Views.Coins
{
    public partial class CoinsGridView : UserControl
    {
        public CoinsGridView()
        {
            InitializeComponent();

            ViewModel = new CoinsGridViewModel();
            DataContext = ViewModel;

            CoinsGrid.SelectionChanged += (_, __) => SelectedItemChanged?.Invoke(this, SelectedItem);
            Loaded += (_, __) => SelectedItemChanged?.Invoke(this, SelectedItem);
        }

        public CoinsGridViewModel ViewModel { get; }

        public DataGrid GridControl => CoinsGrid;

        public event EventHandler<TickerRow?>? SelectedItemChanged;

        public TickerRow? SelectedItem => CoinsGrid.SelectedItem as TickerRow;

        public void BindItems(ObservableCollection<TickerRow> items)
        {
            ViewModel.SetItems(items);
            RefreshSort();
        }

        private void FavoriteCheckChanged(object sender, RoutedEventArgs e)
        {
            RefreshSort();
        }

        private void RefreshSort()
        {
            var source = CoinsGrid.ItemsSource ?? ViewModel.Items;
            var view = CollectionViewSource.GetDefaultView(source);
            view?.Refresh();
        }
    }
}
