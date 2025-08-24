using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Web.WebView2.Wpf;

namespace BinanceUsdtTicker
{
    public partial class ChartWindow : Window
    {
        public string Symbol { get; private set; } = "";

        private bool _isInitialized;

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
            if (_isInitialized || InfoText == null || ChartWebView == null)
                return;

            try
            {
                InfoText.Text = "Yükleniyor...";
                InfoText.Visibility = Visibility.Visible;

                string interval = (IntervalBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "5m";

                await ChartWebView.EnsureCoreWebView2Async();
                ChartWebView.CoreWebView2.NavigationCompleted += (_, __) =>
                {
                    if (InfoText != null) InfoText.Visibility = Visibility.Collapsed;
                };

                string html = BuildHtml(Symbol, interval);
                ChartWebView.NavigateToString(html);
                _isInitialized = true;
            }
            catch (Exception ex)
            {
                InfoText.Text = "Hata: " + ex.Message;
                InfoText.Visibility = Visibility.Visible;
            }
        }

        private async void IntervalBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isInitialized)
                return;

            string interval = (IntervalBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "5m";
            await LoadCandlesAsync(interval);
        }

        private async void Refresh_Click(object sender, RoutedEventArgs e)
        {
            if (!_isInitialized)
                return;

            string interval = (IntervalBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "5m";
            await LoadCandlesAsync(interval);
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

            const string scriptTag = "<script src='https://unpkg.com/lightweight-charts/dist/lightweight-charts.standalone.production.js'></script>";

            return $@"<!DOCTYPE html>
<html>
<head>
    <meta charset='UTF-8'/>
    {scriptTag}
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

    window.setCandles = function(candles) {{
        series.setData(candles);
        chart.timeScale().fitContent();
    }};

    async function loadCandles(symbol, interval) {{
        const url = 'https://api.binance.com/api/v3/klines?symbol=' + symbol + '&interval=' + interval + '&limit=200';
        const data = await fetch(url).then(r => r.json());
        const candles = data.map(c => ({{ time: c[0] / 1000, open: parseFloat(c[1]), high: parseFloat(c[2]), low: parseFloat(c[3]), close: parseFloat(c[4]) }}));
        setCandles(candles);
    }}
    window.loadCandles = loadCandles;
    loadCandles('{symbol}','{interval}');

    window.addEventListener('resize', () => {{
        chart.applyOptions({{ width: window.innerWidth, height: window.innerHeight }});
    }});
</script>
</body>
</html>";
        }

        private async Task LoadCandlesAsync(string interval)
        {
            if (ChartWebView?.CoreWebView2 == null) return;
            string js = $"loadCandles('{Symbol}','{interval}')";
            await ChartWebView.CoreWebView2.ExecuteScriptAsync(js);
        }
    }
}
