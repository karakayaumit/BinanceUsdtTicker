using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.Defaults;

namespace BinanceUsdtTicker
{
    public partial class ChartWindow : Window
    {
        public string Symbol { get; private set; } = "";

        public ObservableCollection<ISeries> Series { get; } = new();
        private readonly ObservableCollection<FinancialPoint> _values = new();

        // Parametresiz ctor (XAML designer/InitializeComponent için)
        public ChartWindow()
        {
            InitializeComponent();
            DataContext = this;
            Loaded += ChartWindow_Loaded;

            Series.Add(new CandlesticksSeries<FinancialPoint>
            {
                Values = _values
            });
        }

        // MainWindow'dan çağrılan ctor
        public ChartWindow(string symbol) : this()
        {
            if (!string.IsNullOrWhiteSpace(symbol))
                Symbol = symbol.ToUpperInvariant();

            Title = string.IsNullOrEmpty(Symbol) ? "Grafik" : $"Grafik - {Symbol}";
            if (SymbolText != null) SymbolText.Text = Symbol;
        }

        private async void ChartWindow_Loaded(object? sender, RoutedEventArgs e)
        {
            await LoadAndDrawAsync();
        }

        private async void IntervalBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            await LoadAndDrawAsync();
        }

        private async void Refresh_Click(object sender, RoutedEventArgs e)
        {
            await LoadAndDrawAsync();
        }

        private async Task LoadAndDrawAsync()
        {
            if (InfoText == null) return;

            try
            {
                InfoText.Text = "Yükleniyor...";
                InfoText.Visibility = Visibility.Visible;

                string interval = (IntervalBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "5m";
                var candles = await BinanceRestService.GetKlinesAsync(Symbol, interval, 120);

                _values.Clear();
                foreach (var c in candles)
                {
                    _values.Add(new FinancialPoint
                    {
                        Date = c.OpenTimeUtc,
                        Open = (double)c.Open,
                        High = (double)c.High,
                        Low = (double)c.Low,
                        Close = (double)c.Close
                    });
                }

                if (_values.Count == 0)
                {
                    InfoText.Text = "Veri bulunamadı";
                    InfoText.Visibility = Visibility.Visible;
                }
                else
                {
                    InfoText.Visibility = Visibility.Collapsed;
                }
            }
            catch (Exception ex)
            {
                InfoText.Text = "Hata: " + ex.Message;
                InfoText.Visibility = Visibility.Visible;
            }
        }
    }
}

