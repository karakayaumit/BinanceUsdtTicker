using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;

namespace BinanceUsdtTicker
{
    public partial class ChartWindow : Window
    {
        public string Symbol { get; }

        public ChartWindow()
        {
            InitializeComponent();
            Loaded += ChartWindow_Loaded;
            Symbol = string.Empty;
        }

        public ChartWindow(string symbol) : this()
        {
            if (!string.IsNullOrWhiteSpace(symbol))
                Symbol = symbol.Trim().ToUpperInvariant();

            Title = string.IsNullOrEmpty(Symbol) ? "Grafik" : $"Grafik - {Symbol}";
        }

        private async void ChartWindow_Loaded(object? sender, RoutedEventArgs e)
        {
            await LoadChartAsync();
        }

        private async Task LoadChartAsync()
        {
            if (string.IsNullOrWhiteSpace(Symbol)) return;

            await ChartWebView.EnsureCoreWebView2Async();
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var path = Path.Combine(baseDir, "Resources", "chart.html");
            var uri = new Uri(path);
            var builder = new UriBuilder(uri)
            {
                Query = $"symbol={Uri.EscapeDataString(Symbol)}&interval=1m"
            };
            ChartWebView.CoreWebView2.Navigate(builder.Uri.ToString());
        }
    }
}
