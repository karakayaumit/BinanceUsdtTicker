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
        private readonly List<Candle> _candles = new();

        public ChartWindow()
        {
            InitializeComponent();
            Loaded += ChartWindow_Loaded;
        }

        public ChartWindow(string symbol) : this()
        {
            if (!string.IsNullOrWhiteSpace(symbol))
                Symbol = symbol.Trim().ToUpperInvariant();

            Title = string.IsNullOrEmpty(Symbol) ? "Grafik" : $"Grafik - {Symbol}";
            if (SymbolText != null) SymbolText.Text = Symbol;
        }

        public ChartWindow(string symbol, BinanceSpotService service) : this(symbol)
        {
            _service = service;
            _service.OnCandle += Service_OnCandle;
            Closed += ChartWindow_Closed;
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

                if (string.IsNullOrWhiteSpace(Symbol))
                {
                    if (InfoText != null)
                    {
                        InfoText.Text = "Geçersiz sembol.";
                        InfoText.Visibility = Visibility.Visible;
                    }
                    return;
                }

                var encodedSymbol = Uri.EscapeDataString(Symbol);
                var encodedInterval = Uri.EscapeDataString(interval);
                var url = $"https://api.binance.com/api/v3/klines?symbol={encodedSymbol}&interval={encodedInterval}&limit=200";
                using var http = new HttpClient();
                using var response = await http.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    string msg = await response.Content.ReadAsStringAsync();
                    if (InfoText != null)
                    {
                        InfoText.Text = $"Hata: {(int)response.StatusCode} {msg}";
                        InfoText.Visibility = Visibility.Visible;
                    }
                    return;
                }

                using var stream = await response.Content.ReadAsStreamAsync();
                using var doc = await JsonDocument.ParseAsync(stream);
                var items = new List<Candle>();
                foreach (var el in doc.RootElement.EnumerateArray())
                {
                    long time = el[0].GetInt64() / 1000;
                    decimal open = decimal.Parse(el[1].GetString() ?? "0", CultureInfo.InvariantCulture);
                    decimal high = decimal.Parse(el[2].GetString() ?? "0", CultureInfo.InvariantCulture);
                    decimal low = decimal.Parse(el[3].GetString() ?? "0", CultureInfo.InvariantCulture);
                    decimal close = decimal.Parse(el[4].GetString() ?? "0", CultureInfo.InvariantCulture);
                    items.Add(new Candle { Time = time, Open = open, High = high, Low = low, Close = close });
                }

                _candles.Clear();
                _candles.AddRange(items);
                DrawCandles(_candles);

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
        private void DrawCandles(IReadOnlyList<Candle> items)
        {
            if (ChartCanvas == null) return;
            ChartCanvas.Children.Clear();
            if (items.Count == 0) return;

            double width = ChartCanvas.ActualWidth;
            double height = ChartCanvas.ActualHeight;
            if (width <= 0 || height <= 0) return;

            double candleWidth = width / items.Count;
            double min = (double)items.Min(k => k.Low);
            double max = (double)items.Max(k => k.High);
            double scale = (height - 20) / (max - min);

            for (int i = 0; i < items.Count; i++)
            {
                var k = items[i];
                double open = (double)k.Open;
                double close = (double)k.Close;
                double high = (double)k.High;
                double low = (double)k.Low;
                double x = i * candleWidth + candleWidth / 2;
                double yOpen = height - (open - min) * scale;
                double yClose = height - (close - min) * scale;
                double yHigh = height - (high - min) * scale;
                double yLow = height - (low - min) * scale;

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
                    Fill = close >= open ? Brushes.Green : Brushes.Red,
                    Stroke = Brushes.Black,
                    StrokeThickness = 1
                };
                Canvas.SetLeft(rect, x - rect.Width / 2);
                Canvas.SetTop(rect, Math.Min(yOpen, yClose));
                ChartCanvas.Children.Add(rect);
            }
        }

        private void Service_OnCandle(string symbol, Candle candle)
        {
            if (!string.Equals(symbol, Symbol, StringComparison.OrdinalIgnoreCase)) return;

            if ((IntervalBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() != "1m")
                return;

            Dispatcher.Invoke(() =>
            {
                if (_candles.Count > 0 && _candles[^1].Time == candle.Time)
                    _candles[^1] = candle;
                else
                {
                    _candles.Add(candle);
                    if (_candles.Count > 200)
                        _candles.RemoveAt(0);
                }

                DrawCandles(_candles);
            });
        }

        private void ChartWindow_Closed(object? sender, EventArgs e)
        {
            if (_service != null)
                _service.OnCandle -= Service_OnCandle;
        }
    }
}
