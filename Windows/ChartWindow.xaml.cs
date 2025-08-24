using System;
using System.Globalization;
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
        private readonly BinanceSpotService? _service;

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

        public ChartWindow(string symbol, BinanceSpotService service) : this(symbol)
        {
            _service = service;
            _service.OnCandle += Service_OnCandle;
            Closed += (_, __) => _service.OnCandle -= Service_OnCandle;
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
            string grid = GetColor("Divider");
            string subtle = GetColor("SubtleText");
            string up = GetColor("Up1Bg");
            string down = GetColor("Down1Bg");

            return $@"<!DOCTYPE html>
<html>
<head>
    <meta charset='UTF-8'/>
    <script src='ms-appx-web:///Resources/lightweight-charts.standalone.production.js'></script>
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
            grid: {{
                vertLines: {{ color: '{grid}' }},
                horzLines: {{ color: '{grid}' }}
            }},
            crosshair: {{
                mode: LightweightCharts.CrosshairMode.Normal,
                vertLine: {{ color: '{subtle}', width: 1, style: 0 }},
                horzLine: {{ color: '{subtle}', width: 1, style: 0 }}
            }},
            rightPriceScale: {{ borderColor: '{grid}' }},
            timeScale: {{ borderColor: '{grid}' }},
            localization: {{ priceFormatter: fmt }}
        }});
    const series = chart.addCandlestickSeries({{
        upColor: '{up}',
        downColor: '{down}',
        borderUpColor: '{up}',
        borderDownColor: '{down}',
        wickUpColor: '{up}',
        wickDownColor: '{down}',
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

        private void Service_OnCandle(string sym, Candle candle)
        {
            if (!string.Equals(sym, Symbol, StringComparison.OrdinalIgnoreCase)) return;
            if (ChartWebView?.CoreWebView2 == null) return;

            string js = $"series.update({{ time: {candle.Time}, open: {candle.Open.ToString(CultureInfo.InvariantCulture)}, high: {candle.High.ToString(CultureInfo.InvariantCulture)}, low: {candle.Low.ToString(CultureInfo.InvariantCulture)}, close: {candle.Close.ToString(CultureInfo.InvariantCulture)} }});";

            _ = Dispatcher.InvokeAsync(() => ChartWebView.CoreWebView2.ExecuteScriptAsync(js));
        }
    }
}
