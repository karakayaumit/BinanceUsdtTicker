using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Media;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Controls.Primitives; // ToggleButton burada
using System.Windows.Threading; // en üstte varsa gerekmez

namespace BinanceUsdtTicker
{
    public enum FilterMode { All, Positive, Negative }
    public enum ThemeKind { Light, Dark }
    public enum QuickFilter { None, Pos3Plus, Neg3Minus }

    public class UiSettings
    {
        public string Theme { get; set; } = "Light";
        public string FilterMode { get; set; } = "All";
        public List<ColumnState> Columns { get; set; } = new();
    }
    public class ColumnState
    {
        public string Header { get; set; } = "";
        public int DisplayIndex { get; set; }
        public double Width { get; set; }
    }

    public partial class MainWindow : Window
    {
        private readonly ObservableCollection<TickerRow> _rows = new();
        private readonly BinanceSpotService _service = new();
        private readonly Dictionary<string, TickerRow> _rowBySymbol = new(StringComparer.OrdinalIgnoreCase);

        private readonly ObservableCollection<PriceAlert> _alerts = new();
        private readonly ObservableCollection<AlertHit> _alertLog = new();
        private const int MaxAlertLog = 500;

        private readonly HashSet<string> _favoriteSymbols = new(StringComparer.OrdinalIgnoreCase);

        private FilterMode _filterMode = FilterMode.All;
        private QuickFilter _quickFilter = QuickFilter.None;

        private static readonly string AppDir =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BinanceUsdtTicker");
        private static readonly string AlertsFile = Path.Combine(AppDir, "alerts.json");
        private static readonly string FavoritesFile = Path.Combine(AppDir, "favorites.json");
        private static readonly string UiSettingsFile = Path.Combine(AppDir, "ui_settings.json");

        private UiSettings _ui = new();
        private ThemeKind _theme = ThemeKind.Light;

        // Top movers modu (%24s / Snapshot)
        private bool _topMoversUse24h = true;

        public MainWindow()
        {
            InitializeComponent();

            // ana grid bağla
            Grid.ItemsSource = _rows;
            CollectionViewSource.GetDefaultView(_rows).Filter = RowFilter;

            // alarm geçmişi bağla
            var alertList = FindName("AlertList") as ListView;
            if (alertList != null)
            {
                alertList.ItemsSource = _alertLog;

                var screenHeight = SystemParameters.PrimaryScreenHeight / 8;

                alertList.Height = screenHeight;
                alertList.MinHeight = screenHeight;
                alertList.MaxHeight = screenHeight;
            }

            // servis
            _service.OnTickersUpdated += OnServiceTickersUpdated;

            LoadUiSettingsSafe();
            ApplyTheme(_themeFromString(_ui.Theme));
            ApplyFilterFromString(_ui.FilterMode);

            Loaded += async (_, __) =>
            {
                ApplyColumnLayoutFromSettings();
                EnsureSpecialColumnsOrder(); // ★ -> Sembol
                await InitializeAsync();
            };

            Closed += async (_, __) =>
            {
                _service.OnTickersUpdated -= OnServiceTickersUpdated;
                await _service.StopAsync();
                SaveFavoritesSafe();
                SaveUiSettingsFromUi();
            };
        }

        // ---------- küçük yardımcılar ----------
        private T? Q<T>(string name) where T : class => FindName(name) as T;

        private string GetSearchText()
        {
            var tb = Q<TextBox>("SearchBox");
            return (tb?.Text ?? string.Empty).Trim();
        }

        private ThemeKind _themeFromString(string s) =>
            string.Equals(s, "Dark", StringComparison.OrdinalIgnoreCase) ? ThemeKind.Dark : ThemeKind.Light;

        // ApplyTheme: remove ONLY Light.xaml/Dark.xaml; keep Styles.Toolbar.xaml
        private void ApplyTheme(ThemeKind kind)
        {
            _theme = kind;
            var name = (kind == ThemeKind.Dark) ? "Dark" : "Light";
            var uri = new Uri($"Themes/{name}.xaml", UriKind.Relative);

            void SwapThemes(Collection<ResourceDictionary> col, Uri newUri)
            {
                for (int i = col.Count - 1; i >= 0; i--)
                {
                    var src = col[i].Source?.OriginalString ?? string.Empty;
                    // only remove theme files; DO NOT remove other dictionaries in Themes/
                    if (src.EndsWith("/Dark.xaml", StringComparison.OrdinalIgnoreCase) ||
                        src.EndsWith("/Light.xaml", StringComparison.OrdinalIgnoreCase))
                    {
                        col.RemoveAt(i);
                    }
                }
                // Insert after base styles so theme overrides colors
                col.Add(new ResourceDictionary { Source = newUri });
            }

            SwapThemes(this.Resources.MergedDictionaries, uri);
            if (Application.Current != null)
                SwapThemes(Application.Current.Resources.MergedDictionaries, uri);

            var themeToggle = Q<ToggleButton>("ThemeToggle");
            if (themeToggle != null)
            {
                themeToggle.IsChecked = (kind == ThemeKind.Dark);
                themeToggle.Content = (kind == ThemeKind.Dark) ? "Açık Tema" : "Koyu Tema";
            }
        }

        private void ThemeToggle_Checked(object sender, RoutedEventArgs e) => ApplyTheme(ThemeKind.Dark);
        private void ThemeToggle_Unchecked(object sender, RoutedEventArgs e) => ApplyTheme(ThemeKind.Light);

        // ---------- servis ----------
        private async Task InitializeAsync()
        {
            try
            {
                LoadAlertsSafe();
                LoadFavoritesSafe();

                await _service.StartAsync();

                TakeSnapshot();

                if (EvaluateAllAlertsNow())
                    SaveAlertsSafe();

                ScheduleInitialAlertCheck();

                ApplySortForMode(_filterMode);

                // TopMovers ilk hesap
                UpdateTopMovers(_topMoversUse24h);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnServiceTickersUpdated(List<TickerRow> latest)
        {
            if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished) return;

            Dispatcher.Invoke(() =>
            {
                ApplyUpdate(latest);

                if (EvaluateAlerts(latest))
                    SaveAlertsSafe();

                var last = Q<TextBlock>("LastUpdateText");
                if (last != null) last.Text = "Son veri: " + DateTime.Now.ToString("HH:mm:ss");

                CollectionViewSource.GetDefaultView(_rows).Refresh();

                // seçili moda göre bir kez hesapla
                UpdateTopMovers(_topMoversUse24h);
            });
        }

        private void ApplyUpdate(List<TickerRow> latest)
        {
            var dict = latest.ToDictionary(x => x.Symbol, StringComparer.OrdinalIgnoreCase);

            foreach (var kv in dict)
            {
                if (_rowBySymbol.TryGetValue(kv.Key, out var row))
                {
                    row.Price = kv.Value.Price;
                    row.Open = kv.Value.Open;
                    row.High = kv.Value.High;
                    row.Low = kv.Value.Low;
                    row.Volume = kv.Value.Volume;
                    row.ChangePercent = kv.Value.ChangePercent;
                    row.LastUpdate = kv.Value.LastUpdate;
                }
                else
                {
                    kv.Value.BaselinePrice ??= kv.Value.Price;
                    kv.Value.IsFavorite = _favoriteSymbols.Contains(kv.Key);
                    _rowBySymbol[kv.Key] = kv.Value;
                    _rows.Add(kv.Value);
                }
            }
        }

        // ---------- filtre ----------
        private bool RowFilter(object obj)
        {
            if (obj is not TickerRow row) return false;

            var qRaw = GetSearchText();
            if (!string.IsNullOrEmpty(qRaw))
            {
                var qU = qRaw.ToUpperInvariant();
                var qClean = qU.Replace("/", "");
                bool match =
                    (row.Symbol?.ToUpperInvariant().Contains(qClean) ?? false) ||
                    (row.BaseSymbol?.ToUpperInvariant().Contains(qU) ?? false);
                if (!match) return false;
            }

            // ana mod
            bool pass = _filterMode switch
            {
                FilterMode.Positive => row.ChangeSinceStartPercent > 0,
                FilterMode.Negative => row.ChangeSinceStartPercent < 0,
                _ => true
            };
            if (!pass) return false;

            // hızlı çip
            return _quickFilter switch
            {
                QuickFilter.Pos3Plus => row.ChangeSinceStartPercent >= 3m,
                QuickFilter.Neg3Minus => row.ChangeSinceStartPercent <= -3m,
                _ => true
            };
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            CollectionViewSource.GetDefaultView(_rows).Refresh();
        }

        private void ApplyFilterFromString(string s)
        {
            _filterMode = s switch
            {
                "Positive" => FilterMode.Positive,
                "Negative" => FilterMode.Negative,
                _ => FilterMode.All
            };

            // UI sync (varsa)
            var rbAll = Q<RadioButton>("RbAll");
            var rbPos = Q<RadioButton>("RbPos");
            var rbNeg = Q<RadioButton>("RbNeg");
            if (rbAll != null) rbAll.IsChecked = _filterMode == FilterMode.All;
            if (rbPos != null) rbPos.IsChecked = _filterMode == FilterMode.Positive;
            if (rbNeg != null) rbNeg.IsChecked = _filterMode == FilterMode.Negative;
        }

        private void FilterModeChanged(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded)
            {
                Dispatcher.BeginInvoke(new Action(() => FilterModeChanged(sender, e)), DispatcherPriority.Loaded);
                return;
            }

            if (sender is RadioButton rb && rb.Tag is string tag)
            {
                _filterMode = tag switch
                {
                    "Positive" => FilterMode.Positive,
                    "Negative" => FilterMode.Negative,
                    _ => FilterMode.All
                };

                ApplySortForMode(_filterMode);
                CollectionViewSource.GetDefaultView(_rows)?.Refresh();
            }
        }


        private void ApplySortForMode(FilterMode mode)
        {
            // Grid henüz hazır değilse, pencere yüklendikten sonra tekrar dene
            if (!IsLoaded || (Grid == null && _rows == null))
            {
                Dispatcher.BeginInvoke(new Action(() => ApplySortForMode(mode)), DispatcherPriority.Loaded);
                return;
            }

            // Hangi koleksiyona bakacağız?
            var itemsSource = Grid?.ItemsSource ?? _rows;
            if (itemsSource == null)
            {
                Dispatcher.BeginInvoke(new Action(() => ApplySortForMode(mode)), DispatcherPriority.Background);
                return;
            }

            var view = CollectionViewSource.GetDefaultView(itemsSource);
            if (view == null)
            {
                Dispatcher.BeginInvoke(new Action(() => ApplySortForMode(mode)), DispatcherPriority.Background);
                return;
            }

            using (view.DeferRefresh())
            {
                view.SortDescriptions.Clear();

                // 1) Favoriler önce
                view.SortDescriptions.Add(
                    new SortDescription(nameof(TickerRow.IsFavorite), ListSortDirection.Descending));

                // 2) Hareket büyüklüğü
                var dir = (mode == FilterMode.Negative)
                            ? ListSortDirection.Ascending
                            : ListSortDirection.Descending;

                // Modelindeki isim "ChangeSinceStartPercent" ise bu kalsın; farklıysa yeniden adlandır.
                view.SortDescriptions.Add(
                    new SortDescription(nameof(TickerRow.ChangeSinceStartPercent), dir));
                // Alternatif isim kullanıyorsan:
                // view.SortDescriptions.Add(new SortDescription(nameof(TickerRow.ChangeFromStartPercent), dir));
            }
        }


        // hızlı filtre çipleri
        private void Chip_Pos3_Click(object sender, RoutedEventArgs e)
        {
            _quickFilter = QuickFilter.Pos3Plus;
            CollectionViewSource.GetDefaultView(_rows).Refresh();
        }
        private void Chip_Neg3_Click(object sender, RoutedEventArgs e)
        {
            _quickFilter = QuickFilter.Neg3Minus;
            CollectionViewSource.GetDefaultView(_rows).Refresh();
        }
        private void Chip_Clear_Click(object sender, RoutedEventArgs e)
        {
            _quickFilter = QuickFilter.None;
            CollectionViewSource.GetDefaultView(_rows).Refresh();
        }

        // ---------- snapshot ----------
        private void RefreshSymbols_Click(object sender, RoutedEventArgs e)
        {
            TakeSnapshot();
            ApplySortForMode(_filterMode);
            CollectionViewSource.GetDefaultView(_rows).Refresh();
            // snapshot mod seçiliyse top movers’ı da yenile
            UpdateTopMovers(_topMoversUse24h);
        }

        private void TakeSnapshot()
        {
            foreach (var r in _rows)
                r.BaselinePrice = r.Price;

            var sb = Q<TextBlock>("SnapshotInfoText");
            if (sb != null) sb.Text = $"Snapshot alındı: {DateTime.Now:HH:mm:ss}";
        }

        // ---------- favori ----------
        private void FavoriteToggle_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is TickerRow row)
            {
                if (row.IsFavorite)
                    _favoriteSymbols.Add(row.Symbol);
                else
                    _favoriteSymbols.Remove(row.Symbol);

                SaveFavoritesSafe();
            }

            ApplySortForMode(_filterMode);
            CollectionViewSource.GetDefaultView(_rows).Refresh();
        }

        private void ChartButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is TickerRow row)
            {
                var win = new ChartWindow(row.Symbol);
                win.Owner = this;
                win.Show();
            }
        }

        // ---------- alarmlar (UI tarafı sizde mevcut) ----------
        private void AddAlert_Click(object sender, RoutedEventArgs e)
        {
            string? prefill = (Grid.SelectedItem as TickerRow)?.BaseSymbol;

            var dlg = new AddAlertWindow(prefill) { Owner = this };
            if (dlg.ShowDialog() == true && dlg.Result is not null)
            {
                _alerts.Add(dlg.Result);
                var sb = Q<TextBlock>("SnapshotInfoText");
                if (sb != null) sb.Text = $"Alarm eklendi: {dlg.Result.Symbol} • {dlg.Result.Type}";
                SaveAlertsSafe();
            }
        }

        private void ManageAlerts_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new ManageAlertsWindow(_alerts) { Owner = this };
            if (dlg.ShowDialog() == true && dlg.IsSaved)
            {
                _alerts.Clear();
                foreach (var a in dlg.ResultAlerts)
                    _alerts.Add(a);

                SaveAlertsSafe();
            }
        }

        private bool EvaluateAlerts(List<TickerRow> updatedRows)
        {
            if (_alerts.Count == 0) return false;

            var nowUtc = DateTime.UtcNow;
            bool anyTriggered = false;

            var updatedMap = updatedRows.ToDictionary(r => r.Symbol, StringComparer.OrdinalIgnoreCase);

            foreach (var alert in _alerts)
                foreach (var kv in updatedMap)
                {
                    if (!_rowBySymbol.TryGetValue(kv.Key, out var row)) continue;

                    if (alert.Evaluate(row, nowUtc, out var msg))
                    {
                        anyTriggered = true;
                        try { SystemSounds.Exclamation.Play(); } catch { }
                        var sb = Q<TextBlock>("SnapshotInfoText");
                        if (sb != null) sb.Text = $"🔔 {msg} • {DateTime.Now:HH:mm:ss}";
                        LogAlert(msg, alert, row, nowUtc);
                    }
                }

            return anyTriggered;
        }

        private bool EvaluateAllAlertsNow()
        {
            if (_alerts.Count == 0 || _rowBySymbol.Count == 0) return false;

            var nowUtc = DateTime.UtcNow;
            bool anyTriggered = false;

            foreach (var alert in _alerts)
            {
                if (!_rowBySymbol.TryGetValue(alert.Symbol, out var row)) continue;

                if (alert.Evaluate(row, nowUtc, out var msg))
                {
                    anyTriggered = true;
                    try { SystemSounds.Exclamation.Play(); } catch { }
                    var sb = Q<TextBlock>("SnapshotInfoText");
                    if (sb != null) sb.Text = $"🔔 {msg} • {DateTime.Now:HH:mm:ss}";
                    LogAlert(msg, alert, row, nowUtc);
                }
            }

            return anyTriggered;
        }

        private async void ScheduleInitialAlertCheck()
        {
            for (int i = 0; i < 3; i++)
            {
                await Task.Delay(1000);
                if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished) return;

                Dispatcher.Invoke(() =>
                {
                    if (EvaluateAllAlertsNow())
                        SaveAlertsSafe();
                });
            }
        }

        private static string AlertTypeToTr(AlertType t) => t switch
        {
            AlertType.PriceAtOrAbove => "Fiyat ≥",
            AlertType.PriceAtOrBelow => "Fiyat ≤",
            AlertType.PriceBetween => "Fiyat [min,max]",
            AlertType.ChangeSinceStartAtOrAbove => "Başlangıca göre % ≥",
            AlertType.ChangeSinceStartAtOrBelow => "Başlangıca göre % ≤",
            _ => t.ToString()
        };

        private void LogAlert(string msg, PriceAlert alert, TickerRow row, DateTime nowUtc)
        {
            _alertLog.Insert(0, new AlertHit
            {
                TimestampUtc = nowUtc,
                Symbol = row.Symbol,
                TypeText = AlertTypeToTr(alert.Type),
                Message = msg,
                Price = (double)row.Price
            });

            if (_alertLog.Count > MaxAlertLog)
                _alertLog.RemoveAt(_alertLog.Count - 1);

            AdjustAlertMsgColumnWidth();
        }

        private void ClearAlertLog_Click(object sender, RoutedEventArgs e)
        {
            _alertLog.Clear();
            AdjustAlertMsgColumnWidth();
        }

        private void ExportAlertLog_Click(object sender, RoutedEventArgs e)
        {
            var sfd = new SaveFileDialog
            {
                Title = "Alarm Geçmişini Dışa Aktar",
                Filter = "CSV Dosyası (*.csv)|*.csv",
                FileName = $"alarm-gecmisi_{DateTime.Now:yyyyMMdd_HHmm}.csv"
            };
            if (sfd.ShowDialog(this) != true) return;

            try
            {
                using var sw = new StreamWriter(sfd.FileName, false, System.Text.Encoding.UTF8);
                sw.WriteLine("Zaman;Sembol;Koşul;Mesaj;Fiyat");
                foreach (var a in _alertLog)
                {
                    sw.WriteLine($"{a.LocalTime};{a.Symbol};{a.TypeText};\"{a.Message}\";{a.Price.ToString(System.Globalization.CultureInfo.CurrentCulture)}");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Dışa aktarma başarısız: " + ex.Message, "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ---------- Top Movers ----------

        private class TopMoverItem
        {
            public string BaseSymbol { get; init; } = "";
            public decimal Metric { get; init; } // % değer
            public bool IsMove1Plus { get; init; }
            public bool IsMove3Plus { get; init; }
            public bool IsMoveMinus1 { get; init; }
            public bool IsMoveMinus3 { get; init; }
        }

        private void UpdateTopMovers(bool use24h)
        {
            var gainersList = Q<ListView>("TopGainersList");
            var losersList = Q<ListView>("TopLosersList");
            if (gainersList == null || losersList == null) return;

            var rows = _rows.ToList();

            IEnumerable<TickerRow> positives = rows.Where(r =>
            {
                var m = use24h ? r.ChangePercent : r.ChangeSinceStartPercent;
                return m > 0m;
            });

            IEnumerable<TickerRow> negatives = rows.Where(r =>
            {
                var m = use24h ? r.ChangePercent : r.ChangeSinceStartPercent;
                return m < 0m;
            });

            var topGainers = positives
                .OrderByDescending(r => use24h ? r.ChangePercent : r.ChangeSinceStartPercent)
                .Take(20)
                .Select(r =>
                {
                    decimal m = use24h ? r.ChangePercent : r.ChangeSinceStartPercent;
                    return new TopMoverItem
                    {
                        BaseSymbol = r.BaseSymbol,
                        Metric = m,
                        IsMove1Plus = m >= 1 && m < 3,
                        IsMove3Plus = m >= 3
                    };
                })
                .ToList();

            var topLosers = negatives
                .OrderBy(r => use24h ? r.ChangePercent : r.ChangeSinceStartPercent)
                .Take(20)
                .Select(r =>
                {
                    decimal m = use24h ? r.ChangePercent : r.ChangeSinceStartPercent;
                    return new TopMoverItem
                    {
                        BaseSymbol = r.BaseSymbol,
                        Metric = m,
                        IsMoveMinus1 = m <= -1 && m > -3,
                        IsMoveMinus3 = m <= -3
                    };
                })
                .ToList();

            gainersList.ItemsSource = topGainers;
            losersList.ItemsSource = topLosers;
        }

        // ---------- kolon düzeni ----------
        private void EnsureSpecialColumnsOrder()
        {
            if (Grid?.Columns == null) return;

            DataGridColumn? star = Grid.Columns.FirstOrDefault(c => (c.Header?.ToString() ?? "") == "★");
            DataGridColumn? chart = Grid.Columns.FirstOrDefault(c => (c.Header?.ToString() ?? "") == "Grafik");
            DataGridColumn? symbol = Grid.Columns.FirstOrDefault(c => (c.Header?.ToString() ?? "") == "Sembol");

            int idx = 0;
            if (star != null) star.DisplayIndex = idx++;
            if (chart != null) chart.DisplayIndex = idx++;
            if (symbol != null) symbol.DisplayIndex = idx++;
        }

        private void SetColumnVisibilityByHeader(string header, Visibility v)
        {
            var col = Grid?.Columns?.FirstOrDefault(c => string.Equals(c.Header?.ToString(), header, StringComparison.OrdinalIgnoreCase));
            if (col != null) col.Visibility = v;
        }

        // Görünüm: Özet (Unchecked) / Detay (Checked)
        private void ViewToggle_Checked(object sender, RoutedEventArgs e)
        {
            var viewToggle = sender as ToggleButton ?? Q<ToggleButton>("ViewToggle");
            if (viewToggle != null) viewToggle.Content = "Detay";
            SetColumnVisibilityByHeader("Açılış", Visibility.Visible);
            SetColumnVisibilityByHeader("Yüksek", Visibility.Visible);
            SetColumnVisibilityByHeader("Düşük", Visibility.Visible);
            EnsureSpecialColumnsOrder();
        }

        private void ViewToggle_Unchecked(object sender, RoutedEventArgs e)
        {
            var viewToggle = sender as ToggleButton ?? Q<ToggleButton>("ViewToggle");
            if (viewToggle != null) viewToggle.Content = "Özet";
            SetColumnVisibilityByHeader("Açılış", Visibility.Collapsed);
            SetColumnVisibilityByHeader("Yüksek", Visibility.Collapsed);
            SetColumnVisibilityByHeader("Düşük", Visibility.Collapsed);
            EnsureSpecialColumnsOrder();
        }

        // ---------- UI settings / favorites ----------
        private void SaveAlertsSafe()
        {
            try
            {
                Directory.CreateDirectory(AppDir);
                var json = JsonSerializer.Serialize(_alerts.ToList(), new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(AlertsFile, json);
            }
            catch { }
        }

        private void LoadAlertsSafe()
        {
            try
            {
                if (!File.Exists(AlertsFile)) return;
                var json = File.ReadAllText(AlertsFile);
                var loaded = JsonSerializer.Deserialize<List<PriceAlert>>(json) ?? new List<PriceAlert>();

                _alerts.Clear();
                foreach (var a in loaded) _alerts.Add(a);
            }
            catch { }
        }

        private void SaveFavoritesSafe()
        {
            try
            {
                Directory.CreateDirectory(AppDir);
                var list = _favoriteSymbols.OrderBy(s => s).ToList();
                var json = JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(FavoritesFile, json);
            }
            catch { }
        }

        private void LoadFavoritesSafe()
        {
            try
            {
                _favoriteSymbols.Clear();

                if (!File.Exists(FavoritesFile)) return;
                var json = File.ReadAllText(FavoritesFile);
                var list = JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
                foreach (var s in list)
                    if (!string.IsNullOrWhiteSpace(s))
                        _favoriteSymbols.Add(s.Trim());
            }
            catch { }
        }

        private void SaveUiSettingsFromUi()
        {
            try
            {
                Directory.CreateDirectory(AppDir);

                _ui.Theme = _theme == ThemeKind.Dark ? "Dark" : "Light";
                _ui.FilterMode = _filterMode.ToString();

                _ui.Columns = Grid.Columns
                    .Select(c => new ColumnState
                    {
                        Header = c.Header?.ToString() ?? "",
                        DisplayIndex = c.DisplayIndex,
                        Width = c.ActualWidth
                    })
                    .ToList();

                var json = JsonSerializer.Serialize(_ui, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(UiSettingsFile, json);
            }
            catch { }
        }

        private void LoadUiSettingsSafe()
        {
            try
            {
                if (!File.Exists(UiSettingsFile)) { _ui = new UiSettings(); return; }
                var json = File.ReadAllText(UiSettingsFile);
                _ui = JsonSerializer.Deserialize<UiSettings>(json) ?? new UiSettings();
            }
            catch
            {
                _ui = new UiSettings();
            }
        }

        private void ApplyColumnLayoutFromSettings()
        {
            if (_ui.Columns == null || _ui.Columns.Count == 0) return;

            var map = _ui.Columns.ToDictionary(x => x.Header ?? "", x => x, StringComparer.OrdinalIgnoreCase);

            foreach (var col in Grid.Columns)
            {
                var key = col.Header?.ToString() ?? "";
                if (map.TryGetValue(key, out var st))
                {
                    if (st.DisplayIndex >= 0 && st.DisplayIndex < Grid.Columns.Count)
                        col.DisplayIndex = st.DisplayIndex;

                    if (st.Width > 20)
                        col.Width = new DataGridLength(st.Width, DataGridLengthUnitType.Pixel);
                }
            }
        }

        private void AlertList_Loaded(object sender, RoutedEventArgs e) => AdjustAlertMsgColumnWidth();
        private void AlertList_SizeChanged(object sender, SizeChangedEventArgs e) => AdjustAlertMsgColumnWidth();

        private void AdjustAlertMsgColumnWidth()
        {
            var alertList = Q<ListView>("AlertList");
            if (alertList?.View is not GridView gv) return;

            // "Mesaj" başlıklı sütunu bul
            GridViewColumn? msgCol = gv.Columns.FirstOrDefault(c =>
                string.Equals(c.Header?.ToString(), "Mesaj", StringComparison.OrdinalIgnoreCase));
            if (msgCol == null) return;

            double fixedSum = 0;
            foreach (var col in gv.Columns)
            {
                if (ReferenceEquals(col, msgCol)) continue;
                var w = col.Width;
                if (double.IsNaN(w) || w <= 0) w = 100;
                fixedSum += w;
            }

            double total = alertList.ActualWidth;
            double padding = 35;

            double scroll = 0;
            var sv = FindDescendant<ScrollViewer>(alertList);
            if (sv != null && sv.ComputedVerticalScrollBarVisibility == Visibility.Visible)
                scroll = SystemParameters.VerticalScrollBarWidth;

            double newWidth = Math.Max(120, total - fixedSum - padding - scroll);
            msgCol.Width = newWidth;
        }

        private static T? FindDescendant<T>(DependencyObject root) where T : DependencyObject
        {
            if (root == null) return null;
            int n = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < n; i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                if (child is T t) return t;
                var sub = FindDescendant<T>(child);
                if (sub != null) return sub;
            }
            return null;
        }
        // using System.Windows.Controls.Primitives; // zaten en üstte var

        private void btnSnapshot_Checked(object sender, RoutedEventArgs e)
        {
            // Snapshot modu
            _topMoversUse24h = false;
            UpdateTopMovers(false);

            // (Opsiyonel) buton/başlık yazısını senkronla
            var t = sender as ToggleButton ?? Q<ToggleButton>("btnSnapshot");
            if (t != null) t.Content = "Top Movers: Snapshot";
            var hdr = Q<TextBlock>("TopMoversHeader");
            if (hdr != null) hdr.Text = "Top Movers — Snapshot";
        }

        private void btnSnapshot_Unchecked(object sender, RoutedEventArgs e)
        {
            // %24s modu
            _topMoversUse24h = true;
            UpdateTopMovers(true);

            // (Opsiyonel) buton/başlık yazısını senkronla
            var t = sender as ToggleButton ?? Q<ToggleButton>("btnSnapshot");
            if (t != null) t.Content = "Top Movers: %24s";
            var hdr = Q<TextBlock>("TopMoversHeader");
            if (hdr != null) hdr.Text = "Top Movers — %24s";
        }

    }
}
