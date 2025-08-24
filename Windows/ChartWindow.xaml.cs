using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

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
            await LoadChartAsync();
        }

        private async void IntervalBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            await LoadChartAsync();
        }

        private async void Refresh_Click(object sender, RoutedEventArgs e)
        {
            await LoadChartAsync();
        }

        private async Task LoadChartAsync()
        {
            if (InfoText == null || ChartWebView == null)
                return;

            try
            {
                InfoText.Text = "Yükleniyor...";
                InfoText.Visibility = Visibility.Visible;

                string interval = (IntervalBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "5m";

                await ChartWebView.EnsureCoreWebView2Async();
                ChartWebView.CoreWebView2.WebMessageReceived -= CoreWebView2_WebMessageReceived;
                ChartWebView.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;

                string html = BuildHtml(Symbol, interval);
                ChartWebView.NavigateToString(html);

                InfoText.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                InfoText.Text = "Hata: " + ex.Message;
                InfoText.Visibility = Visibility.Visible;
            }
        }

        private void CoreWebView2_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                if (InfoText == null) return;
                InfoText.Text = "Hata: " + e.TryGetWebMessageAsString();
                InfoText.Visibility = Visibility.Visible;
            });
        }

        private static string BuildHtml(string symbol, string interval)
        {
            string GetColor(string key)
            {
                if (Application.Current.Resources[key] is SolidColorBrush brush)
                {
                    var c = brush.Color;
                    return $"#{c.R:X2}{c.G:X2}{c.B:X2}";
                }
                return "#ffffff";
            }

            string bg = GetColor("SurfaceAlt");
            string fg = GetColor("OnSurface");

            return $@"<!DOCTYPE html>
<html>
<head>
    <meta charset='UTF-8'/>
    <script src='https://unpkg.com/lightweight-charts@4.2.1/dist/lightweight-charts.standalone.production.js'></script>
</head>
<body style='margin:0;background:{bg};color:{fg};'>
<div id='chart' style='width:100%;height:100%;'></div>
<script>
    const fmt = p => p.toLocaleString(undefined, {{ maximumFractionDigits: 8 }});
    const chart = LightweightCharts.createChart(
        document.getElementById('chart'),
        {{
            width: window.innerWidth,
            height: window.innerHeight,
            layout: {{ background: {{ color: '{bg}' }}, textColor: '{fg}' }},
            localization: {{ priceFormatter: fmt }}
        }});
    const series = chart.addCandlestickSeries({{
        priceFormat: {{ type: 'custom', minMove: 0.00000001, formatter: fmt }}
    }});
    fetch('https://api.binance.com/api/v3/klines?symbol={symbol}&interval={interval}&limit=200')
        .then(r => r.json())
        .then(data => {{
            const candles = data.map(d => ({{ time: Math.floor(d[0]/1000), open: parseFloat(d[1]), high: parseFloat(d[2]), low: parseFloat(d[3]), close: parseFloat(d[4]) }}));
            series.setData(candles);
        }})
        .catch(e => window.chrome.webview.postMessage(e.message));
    window.addEventListener('resize', () => {{
        chart.applyOptions({{ width: window.innerWidth, height: window.innerHeight }});
    }});
</script>
</body>
</html>";
        }
    }
}
