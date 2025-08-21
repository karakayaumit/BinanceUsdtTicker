using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;

namespace BinanceUsdtTicker;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<TickerRow> _rows = new();
    private readonly BinanceSpotService _service = new();
    private readonly Dictionary<string, TickerRow> _rowBySymbol = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();
    private CancellationTokenSource? _cts;
    private HashSet<string> _activeUsdtSymbols = new(StringComparer.OrdinalIgnoreCase);

    public MainWindow()
    {
        InitializeComponent();
        DataContext = CollectionViewSource.GetDefaultView(_rows);
        ((ICollectionView)DataContext).Filter = RowFilter;

        Loaded += async (_, __) =>
        {
            await InitializeAsync();
        };
        Closed += (_, __) => _cts?.Cancel();
    }

    private bool RowFilter(object obj)
    {
        if (obj is not TickerRow row) return false;
        var q = SearchBox.Text?.Trim().ToUpperInvariant() ?? string.Empty;
        if (!string.IsNullOrEmpty(q) && !row.Symbol.ToUpperInvariant().Contains(q))
            return false;
        if (OnlyPositiveChange.IsChecked == true && row.ChangePercent <= 0) 
            return false;
        return true;
    }

    private async Task InitializeAsync()
    {
        try
        {
            StatusText.Text = "Semboller alınıyor...";
            _activeUsdtSymbols = await _service.GetActiveUsdtSymbolsAsync();
            StatusText.Text = $"Aktif USDT parite sayısı: {_activeUsdtSymbols.Count}";

            _rows.Clear();
            _rowBySymbol.Clear();

            _cts?.Cancel();
            _cts = new CancellationTokenSource();

            _ = Task.Run(() => ListenMiniTickerAsync(_cts.Token));
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            StatusText.Text = "Hata: " + ex.Message;
        }
    }

    private async Task ListenMiniTickerAsync(CancellationToken ct)
    {
        await foreach (var mt in _service.StreamAllMiniTickersAsync(ct))
        {
            if (ct.IsCancellationRequested) break;

            if (!_activeUsdtSymbols.Contains(mt.Symbol)) continue;

            Dispatcher.Invoke(() =>
            {
                if (!_rowBySymbol.TryGetValue(mt.Symbol, out var row))
                {
                    row = new TickerRow { Symbol = mt.Symbol };
                    _rowBySymbol[mt.Symbol] = row;
                    _rows.Add(row);
                }
                row.Price = mt.Close;
                row.Open = mt.Open;
                row.High = mt.High;
                row.Low = mt.Low;
                row.Volume = mt.Volume;
                row.ChangePercent = SafeChangePercent(mt.Open, mt.Close);
                row.LastUpdate = DateTimeOffset.FromUnixTimeMilliseconds(mt.EventTime).UtcDateTime;
                LastUpdateText.Text = "Son veri: " + DateTime.Now.ToString("HH:mm:ss");
            });

            ((ICollectionView)DataContext).Refresh();
        }
    }

    private static double SafeChangePercent(double open, double close)
    {
        if (open == 0) return 0;
        return (close - open) / open * 100.0;
    }

    private async void RefreshSymbols_Click(object sender, RoutedEventArgs e)
    {
        await InitializeAsync();
    }

    private void SearchBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        ((ICollectionView)DataContext).Refresh();
    }

    private void FilterChanged(object sender, RoutedEventArgs e)
    {
        ((ICollectionView)DataContext).Refresh();
    }
}
