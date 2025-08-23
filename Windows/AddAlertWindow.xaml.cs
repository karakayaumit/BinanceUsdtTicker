using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace BinanceUsdtTicker
{
    public partial class AddAlertWindow : Window
    {
        public PriceAlert? Result { get; private set; }

        public AddAlertWindow(string? prefillSymbol = null, AlertType preselect = AlertType.PriceAtOrAbove)
        {
            InitializeComponent();

            SymbolBox.Text = prefillSymbol ?? string.Empty;

            foreach (var item in TypeBox.Items)
            {
                if (item is ComboBoxItem cbi && (string)cbi.Tag == preselect.ToString())
                {
                    TypeBox.SelectedItem = cbi;
                    break;
                }
            }

            TypeBox.SelectionChanged += (_, __) =>
            {
                var tag = (TypeBox.SelectedItem as ComboBoxItem)?.Tag as string;
                BBox.Visibility = tag == AlertType.PriceBetween.ToString()
                                  ? Visibility.Visible : Visibility.Collapsed;
            };
        }

        // "BTC", "btc/usdt", " BTCUSDT " -> "BTCUSDT"
        private static string NormalizeToUsdt(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;
            var u = input.Trim().ToUpperInvariant().Replace("/", "").Replace(" ", "");
            if (!u.EndsWith("USDT")) u += "USDT";
            return u;
        }

        // Ondalık girişini esnek ve kültürden bağımsız parse et (DECIMAL)
        private static bool TryParseFlexibleDecimal(string? input, out decimal value)
        {
            value = 0m;
            var s = (input ?? string.Empty).Trim();
            if (s.Length == 0) return false;

            int iDot = s.LastIndexOf('.');
            int iCom = s.LastIndexOf(',');

            if (iDot >= 0 && iCom >= 0)
            {
                // İki ayırıcı da var: sağdaki ondalık, soldaki binliktir
                if (iCom > iDot)
                {
                    // "1.234,56" -> "1234.56"
                    s = s.Replace(".", "");
                    s = s.Replace(',', '.');
                }
                else
                {
                    // "1,234.56" -> "1234.56"
                    s = s.Replace(",", "");
                    // dot zaten ondalık
                }
            }
            else if (iCom >= 0)
            {
                // Sadece virgül var: ondalık olarak kabul
                s = s.Replace(',', '.');
            }
            // else: ya sadece nokta var ya da hiç yok → olduğu gibi kalsın

            return decimal.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            var symInput = SymbolBox.Text ?? string.Empty;
            var sym = NormalizeToUsdt(symInput);
            if (string.IsNullOrWhiteSpace(sym))
            {
                MessageBox.Show("Lütfen bir sembol girin.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var tag = (TypeBox.SelectedItem as ComboBoxItem)?.Tag as string ?? AlertType.PriceAtOrAbove.ToString();
            var type = Enum.Parse<AlertType>(tag);

            if (!TryParseFlexibleDecimal(ABox.Text, out var A))
            {
                MessageBox.Show("Geçerli bir değer girin (A).", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            decimal? B = null;
            if (type == AlertType.PriceBetween)
            {
                if (!TryParseFlexibleDecimal(BBox.Text, out var Bv))
                {
                    MessageBox.Show("Bant için ikinci değeri (B) girin.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                B = Bv;
            }

            int cooldown = 0;
            int.TryParse(CooldownBox.Text, out cooldown);

            Result = new PriceAlert
            {
                Symbol = sym,
                Type = type,
                A = A,
                B = B,
                OneShot = OneShotBox.IsChecked == true,
                CooldownSeconds = Math.Max(0, cooldown),
                Enabled = true
            };

            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
    }
}
