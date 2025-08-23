using System;
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

        private void ChartWindow_Loaded(object? sender, RoutedEventArgs e)
        {
            // Basit placeholder çizimi (örnek dalga)
            DrawPlaceholder();
        }

        private void IntervalBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Şimdilik sadece tekrar placeholder çiz
            DrawPlaceholder();
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            // Şimdilik sadece tekrar placeholder çiz
            DrawPlaceholder();
        }

        private void DrawPlaceholder()
        {
            if (ChartArea == null) return;
            ChartArea.Children.Clear();

            double w = Math.Max(ActualWidth - 60, 300);
            double h = Math.Max(ActualHeight - 140, 200);

            var poly = new Polyline
            {
                Stroke = (Brush)FindResource("WindowFg"),
                StrokeThickness = 1.5
            };

            int n = 60;
            for (int i = 0; i < n; i++)
            {
                double x = i * (w / (n - 1));
                double y = h / 2.0 + Math.Sin(i * 0.3) * (h * 0.3);
                poly.Points.Add(new Point(x, y));
            }

            // Çerçeve
            var rect = new Rectangle
            {
                Width = w,
                Height = h,
                Stroke = (Brush)FindResource("GridLine"),
                StrokeThickness = 1
            };

            Canvas.SetLeft(rect, 0);
            Canvas.SetTop(rect, 0);
            Canvas.SetLeft(poly, 0);
            Canvas.SetTop(poly, 0);

            ChartArea.Children.Add(rect);
            ChartArea.Children.Add(poly);
        }
    }
}
