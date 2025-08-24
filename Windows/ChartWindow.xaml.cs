using System;
using System.Collections.Generic;
using System.Linq;
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

        // Parametresiz ctor (XAML designer/InitializeComponent için)
        public ChartWindow()
        {
            InitializeComponent();
            Loaded += ChartWindow_Loaded;
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
            if (ChartArea == null || InfoText == null) return;

            try
            {
                InfoText.Text = "Yükleniyor...";
                InfoText.Visibility = Visibility.Visible;

                string interval = (IntervalBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "5m";
                var candles = await BinanceRestService.GetKlinesAsync(Symbol, interval, 120);

                if (candles.Count == 0)
                {
                    InfoText.Text = "Veri bulunamadı";
                    return;
                }

                InfoText.Visibility = Visibility.Collapsed;
                DrawCandles(candles);
            }
            catch (Exception ex)
            {
                InfoText.Text = "Hata: " + ex.Message;
                InfoText.Visibility = Visibility.Visible;
            }
        }

        private void DrawCandles(IReadOnlyList<Candle> candles)
        {
            ChartArea.Children.Clear();

            double w = Math.Max(ChartArea.ActualWidth, 100);
            double h = Math.Max(ChartArea.ActualHeight, 100);

            decimal max = candles.Max(c => c.High);
            decimal min = candles.Min(c => c.Low);
            decimal diff = max - min;
            if (diff == 0) diff = 1;

            double step = w / candles.Count;
            double bodyWidth = step * 0.6;

            var upBrush = (Brush)FindResource("Up1Bg");
            var downBrush = (Brush)FindResource("Down1Bg");
            var lineBrush = (Brush)FindResource("OnSurface");
            var borderBrush = (Brush)FindResource("Divider");

            // Çerçeve
            var frame = new Rectangle
            {
                Width = w,
                Height = h,
                Stroke = borderBrush,
                StrokeThickness = 1
            };
            Canvas.SetLeft(frame, 0);
            Canvas.SetTop(frame, 0);
            ChartArea.Children.Add(frame);

            for (int i = 0; i < candles.Count; i++)
            {
                var c = candles[i];
                double xCenter = i * step + step / 2;
                double yHigh = (double)(max - c.High) / (double)diff * h;
                double yLow = (double)(max - c.Low) / (double)diff * h;
                double yOpen = (double)(max - c.Open) / (double)diff * h;
                double yClose = (double)(max - c.Close) / (double)diff * h;

                var line = new Line
                {
                    X1 = xCenter,
                    X2 = xCenter,
                    Y1 = yHigh,
                    Y2 = yLow,
                    Stroke = lineBrush,
                    StrokeThickness = 1
                };
                ChartArea.Children.Add(line);

                double bodyTop = Math.Min(yOpen, yClose);
                double bodyHeight = Math.Max(Math.Abs(yClose - yOpen), 1);
                var rect = new Rectangle
                {
                    Width = bodyWidth,
                    Height = bodyHeight,
                    Fill = c.Close >= c.Open ? upBrush : downBrush,
                    Stroke = lineBrush,
                    StrokeThickness = 1
                };
                Canvas.SetLeft(rect, xCenter - bodyWidth / 2);
                Canvas.SetTop(rect, bodyTop);
                ChartArea.Children.Add(rect);
            }
        }
    }
}

