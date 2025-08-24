using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace BinanceUsdtTicker
{
    public partial class ChartWindow : Window
    {
        public string Symbol { get; private set; } = "";

        private readonly BinanceSpotService? _service;

        public ChartWindow()
        {
            InitializeComponent();
            Loaded += ChartWindow_Loaded;
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
                var items = new List<Kline>();
                foreach (var el in doc.RootElement.EnumerateArray())
                {
                    double open = double.Parse(el[1].GetString() ?? "0", CultureInfo.InvariantCulture);
                    double high = double.Parse(el[2].GetString() ?? "0", CultureInfo.InvariantCulture);
                    double low = double.Parse(el[3].GetString() ?? "0", CultureInfo.InvariantCulture);
                    double close = double.Parse(el[4].GetString() ?? "0", CultureInfo.InvariantCulture);
                    items.Add(new Kline(open, high, low, close));
                }

                DrawCandles(items);

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
        private void DrawCandles(IReadOnlyList<Kline> items)
        {
            if (ChartCanvas == null) return;
            ChartCanvas.Children.Clear();
            if (items.Count == 0) return;

            double width = ChartCanvas.ActualWidth;
            double height = ChartCanvas.ActualHeight;
            if (width <= 0 || height <= 0) return;

            double candleWidth = width / items.Count;
            double min = items.Min(k => k.Low);
            double max = items.Max(k => k.High);
            double scale = (height - 20) / (max - min);

            for (int i = 0; i < items.Count; i++)
            {
                var k = items[i];
                double x = i * candleWidth + candleWidth / 2;
                double yOpen = height - (k.Open - min) * scale;
                double yClose = height - (k.Close - min) * scale;
                double yHigh = height - (k.High - min) * scale;
                double yLow = height - (k.Low - min) * scale;

                var line = new Line
                {
                    X1 = x,
                    X2 = x,
                    Y1 = yHigh,
                    Y2 = yLow,
                    Stroke = Brushes.Gray,
                    StrokeThickness = 1
                };
                ChartCanvas.Children.Add(line);

                var rect = new Rectangle
                {
                    Width = Math.Max(1, candleWidth * 0.7),
                    Height = Math.Abs(yClose - yOpen),
                    Fill = k.Close >= k.Open ? Brushes.Green : Brushes.Red,
                    Stroke = Brushes.Black,
                    StrokeThickness = 1
                };
                Canvas.SetLeft(rect, x - rect.Width / 2);
                Canvas.SetTop(rect, Math.Min(yOpen, yClose));
                ChartCanvas.Children.Add(rect);
            }
        }

        private readonly record struct Kline(double Open, double High, double Low, double Close);
    }
}
