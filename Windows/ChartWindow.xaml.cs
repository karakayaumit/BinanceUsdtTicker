using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace BinanceUsdtTicker
{
    public partial class ChartWindow : Window
    {
        public string Symbol { get; }

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

        private async void ChartWindow_Loaded(object? sender, RoutedEventArgs e)
        {
            string interval = (IntervalBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "5m";
            await LoadChartAsync(interval);
        }

        private async void IntervalBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            string interval = (IntervalBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "5m";
            await LoadChartAsync(interval);
        }

        private async void Refresh_Click(object sender, RoutedEventArgs e)
        {
            string interval = (IntervalBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "5m";
            await LoadChartAsync(interval);
        }

        private async Task LoadChartAsync(string interval)
        {
            if (string.IsNullOrWhiteSpace(Symbol)) return;

            await ChartWebView.EnsureCoreWebView2Async();
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var path = Path.Combine(baseDir, "Resources", "chart.html");
            var uri = new Uri(path);
            var builder = new UriBuilder(uri)
            {
                Query = $"symbol={Uri.EscapeDataString(Symbol)}&interval={Uri.EscapeDataString(interval)}"
            };
            ChartWebView.CoreWebView2.Navigate(builder.Uri.ToString());
        }
    }
}
