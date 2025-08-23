using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace BinanceUsdtTicker
{
    public class AlertTypeOption
    {
        public AlertType Value { get; set; }
        public string Text { get; set; } = "";
    }

    public partial class ManageAlertsWindow : Window
    {
        // Dialog sonucu
        public bool IsSaved { get; private set; }
        public List<PriceAlert> ResultAlerts { get; private set; } = new();

        // Ekranda düzenlenen geçici liste (orijinalin klonu)
        public ObservableCollection<PriceAlert> WorkingAlerts { get; }

        // Koşul seçenekleri (Türkçe)
        public ObservableCollection<AlertTypeOption> AlertTypeOptions { get; } =
            new ObservableCollection<AlertTypeOption>
            {
                new AlertTypeOption { Value = AlertType.PriceAtOrAbove,            Text = "Fiyat ≥" },
                new AlertTypeOption { Value = AlertType.PriceAtOrBelow,            Text = "Fiyat ≤" },
                new AlertTypeOption { Value = AlertType.PriceBetween,              Text = "Fiyat [min,max]" },
                new AlertTypeOption { Value = AlertType.ChangeSinceStartAtOrAbove, Text = "Başlangıca göre % ≥" },
                new AlertTypeOption { Value = AlertType.ChangeSinceStartAtOrBelow, Text = "Başlangıca göre % ≤" }
            };

        // Orijinal listeyi al, klonla
        public ManageAlertsWindow(IEnumerable<PriceAlert> source)
        {
            InitializeComponent();

            WorkingAlerts = new ObservableCollection<PriceAlert>(
                source.Select(CloneAlert)
            );

            DataContext = this;
        }

        private static PriceAlert CloneAlert(PriceAlert a) => new PriceAlert
        {
            Symbol = a.Symbol,
            Type = a.Type,
            A = a.A,
            B = a.B,
            OneShot = a.OneShot,
            CooldownSeconds = a.CooldownSeconds,
            Enabled = a.Enabled
        };

        // Virgül tuşunu anında noktaya çevir (TextBox içinde)
        private void Decimal_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (e.Text == ",")
            {
                if (sender is System.Windows.Controls.TextBox tb)
                {
                    int caret = tb.CaretIndex;
                    tb.Text = tb.Text.Insert(caret, ".");
                    tb.CaretIndex = caret + 1;
                    e.Handled = true;
                }
            }
            // "." doğal olarak gelir
        }

        // NumPad Del ile satır silinmesini önle (TextBox içindeyse izin ver)
        private void Grid_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete)
            {
                if (Keyboard.FocusedElement is System.Windows.Controls.TextBox) return;
                e.Handled = true;
            }
        }

        private void New_Click(object sender, RoutedEventArgs e)
        {
            WorkingAlerts.Add(new PriceAlert
            {
                Symbol = "",
                Type = AlertType.PriceAtOrAbove,
                A = 0,
                B = null,
                OneShot = true,
                CooldownSeconds = 0,
                Enabled = true
            });
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            if (Grid.SelectedItems is not IList selected || selected.Count == 0) return;
            var toRemove = selected.Cast<PriceAlert>().ToList();
            foreach (var a in toRemove) WorkingAlerts.Remove(a);
        }

        private static string NormalizeToUsdt(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;
            var u = input.Trim().ToUpperInvariant().Replace("/", "").Replace(" ", "");
            if (!u.EndsWith("USDT")) u += "USDT";
            return u;
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            Grid.CommitEdit();
            Grid.CommitEdit();

            // Kaydet: normalize edip sonucu döndür
            ResultAlerts = WorkingAlerts
                .Select(a => CloneAlert(a)) // kopya
                .Select(a => { a.Symbol = NormalizeToUsdt(a.Symbol ?? ""); return a; })
                .ToList();

            IsSaved = true;
            DialogResult = true; // MainWindow tarafında kontrol edeceğiz
            Close();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            // Kaydetme—sadece kapat
            DialogResult = false;
            Close();
        }
    }
}
