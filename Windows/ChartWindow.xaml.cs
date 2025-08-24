using System;
using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using WinFormsChart = System.Windows.Forms.DataVisualization.Charting.Chart;
using WinFormsSeries = System.Windows.Forms.DataVisualization.Charting.Series;

namespace BinanceUsdtTicker
{
    public partial class ChartWindow : Window
    {
        public string Symbol { get; private set; } = "";

        private readonly BinanceSpotService? _service;
        private readonly WinFormsChart _chart = new();
        private readonly WinFormsSeries _series;

        public ChartWindow()
        {
            InitializeComponent();
            Loaded += ChartWindow_Loaded;

            _chart.ChartAreas.Add(new System.Windows.Forms.DataVisualization.Charting.ChartArea("Main"));
            _series = new WinFormsSeries("Price")
            {
                ChartType = System.Windows.Forms.DataVisualization.Charting.SeriesChartType.Candlestick,
                XValueType = System.Windows.Forms.DataVisualization.Charting.ChartValueType.DateTime,
                YValuesPerPoint = 4
            };
            _chart.Series.Add(_series);
            _chart.Dock = DockStyle.Fill;
            if (ChartHost != null) ChartHost.Child = _chart;
        }

        public ChartWindow(string symbol) : this()
        {
            if (!string.IsNullOrWhiteSpace(symbol))
                Symbol = symbol.ToUpperInvariant();

            Title = string.IsNullOrEmpty(Symbol) ? "Grafik" : $"Grafik - {Symbol}";
            if (SymbolText != null) SymbolText.Text = Symbol;
        }

        public ChartWindow(string symbol, BinanceSpotService service) : this(symbol)
        {
            _service = service;
        }

        private async void ChartWindow_Loaded(object? sender, RoutedEventArgs e)
        {
            string interval = (IntervalBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "5m";
            await LoadDataAsync(interval);
        }

        private async void IntervalBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            string interval = (IntervalBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "5m";
            await LoadDataAsync(interval);
        }

        private async void Refresh_Click(object sender, RoutedEventArgs e)
        {
            string interval = (IntervalBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "5m";
            await LoadDataAsync(interval);
        }

        private async Task LoadDataAsync(string interval)
        {
            try
            {
                if (InfoText != null)
                {
                    InfoText.Text = "Yükleniyor...";
                    InfoText.Visibility = Visibility.Visible;
                }

                var url = $"https://api.binance.com/api/v3/klines?symbol={Symbol}&interval={interval}&limit=200";
                using var http = new HttpClient();
                using var stream = await http.GetStreamAsync(url);
                using var doc = await JsonDocument.ParseAsync(stream);
                _series.Points.Clear();
                foreach (var el in doc.RootElement.EnumerateArray())
                {
                    long time = el[0].GetInt64();
                    double open = double.Parse(el[1].GetString() ?? "0", CultureInfo.InvariantCulture);
                    double high = double.Parse(el[2].GetString() ?? "0", CultureInfo.InvariantCulture);
                    double low = double.Parse(el[3].GetString() ?? "0", CultureInfo.InvariantCulture);
                    double close = double.Parse(el[4].GetString() ?? "0", CultureInfo.InvariantCulture);
                    _series.Points.AddXY(DateTimeOffset.FromUnixTimeMilliseconds(time).DateTime, high, low, open, close);
                }

                if (InfoText != null)
                    InfoText.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                if (InfoText != null)
                {
                    InfoText.Text = "Hata: " + ex.Message;
                    InfoText.Visibility = Visibility.Visible;
                }
            }
        }
    }
}
