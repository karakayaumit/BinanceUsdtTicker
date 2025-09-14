using System.Text;
using System.Windows;
using System.Windows.Media;
using WinForms = System.Windows.Forms;
using BinanceUsdtTicker.Models;
using BinanceUsdtTicker.Data;
using BinanceUsdtTicker.Runtime;
using BinanceUsdtTicker.Security;
using System.Threading.Tasks;

namespace BinanceUsdtTicker
{
    public partial class SettingsWindow : Window
    {
        private readonly UiSettings _settings;
        private readonly UiSettings _work;
        private readonly SecretRepository _secretRepo;
        private readonly ISecretCache _cache;

        public SettingsWindow(UiSettings settings, SecretRepository secretRepo, ISecretCache cache)
        {
            InitializeComponent();
            _settings = settings;
            _secretRepo = secretRepo;
            _cache = cache;
            _work = new UiSettings
            {
                Theme = settings.Theme,
                FilterMode = settings.FilterMode,
                Columns = settings.Columns,
                ThemeColor = settings.ThemeColor,
                TextColor = settings.TextColor,
                Up1Color = settings.Up1Color,
                Up3Color = settings.Up3Color,
                Down1Color = settings.Down1Color,
                Down3Color = settings.Down3Color,
                DividerColor = settings.DividerColor,
                ControlColor = settings.ControlColor,
                MarginMode = settings.MarginMode,
                WindowsNotification = settings.WindowsNotification,
                BaseUrl = settings.BaseUrl,
                TranslateRegion = settings.TranslateRegion,
                DbServer = settings.DbServer,
                DbName = settings.DbName,
                DbUser = settings.DbUser
            };
            DataContext = _work;
        }

        private void ThemeColor_Click(object sender, RoutedEventArgs e)
        {
            var c = PickColor(_work.ThemeColor);
            if (c != null) _work.ThemeColor = c;
        }

        private void TextColor_Click(object sender, RoutedEventArgs e)
        {
            var c = PickColor(_work.TextColor);
            if (c != null) _work.TextColor = c;
        }

        private void Up1Color_Click(object sender, RoutedEventArgs e)
        {
            var c = PickColor(_work.Up1Color);
            if (c != null) _work.Up1Color = c;
        }

        private void Up3Color_Click(object sender, RoutedEventArgs e)
        {
            var c = PickColor(_work.Up3Color);
            if (c != null) _work.Up3Color = c;
        }

        private void Down1Color_Click(object sender, RoutedEventArgs e)
        {
            var c = PickColor(_work.Down1Color);
            if (c != null) _work.Down1Color = c;
        }

        private void Down3Color_Click(object sender, RoutedEventArgs e)
        {
            var c = PickColor(_work.Down3Color);
            if (c != null) _work.Down3Color = c;
        }

    private void DividerColor_Click(object sender, RoutedEventArgs e)
    {
        var c = PickColor(_work.DividerColor);
        if (c != null) _work.DividerColor = c;
    }

        private void ControlColor_Click(object sender, RoutedEventArgs e)
        {
            var c = PickColor(_work.ControlColor);
            if (c != null) _work.ControlColor = c;
        }

        private void ResetDefaults_Click(object sender, RoutedEventArgs e)
        {
            var def = MainWindow.LoadDefaultUiSettings();
            _work.ThemeColor = def.ThemeColor;
            _work.TextColor = def.TextColor;
            _work.Up1Color = def.Up1Color;
            _work.Up3Color = def.Up3Color;
            _work.Down1Color = def.Down1Color;
            _work.Down3Color = def.Down3Color;
            _work.DividerColor = def.DividerColor;
            _work.ControlColor = def.ControlColor;
            _work.WindowsNotification = def.WindowsNotification;
            _work.BaseUrl = def.BaseUrl;
            _work.TranslateKey = def.TranslateKey;
            _work.TranslateRegion = def.TranslateRegion;
            _work.DbServer = def.DbServer;
            _work.DbName = def.DbName;
            _work.DbUser = def.DbUser;
            _work.DbPassword = def.DbPassword;
        }

        private static string? PickColor(string current)
        {
            var dlg = new WinForms.ColorDialog
            {
                FullOpen = true,
                AllowFullOpen = true,
                AnyColor = true
            };
            try
            {
                if (!string.IsNullOrWhiteSpace(current))
                {
                    var col = (Color)ColorConverter.ConvertFromString(current);
                    dlg.Color = System.Drawing.Color.FromArgb(col.A, col.R, col.G, col.B);
                }
            }
            catch { }
            return dlg.ShowDialog() == WinForms.DialogResult.OK
                ? $"#{dlg.Color.A:X2}{dlg.Color.R:X2}{dlg.Color.G:X2}{dlg.Color.B:X2}"
                : null;
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            _settings.ThemeColor = _work.ThemeColor;
            _settings.TextColor = _work.TextColor;
            _settings.Up1Color = _work.Up1Color;
            _settings.Up3Color = _work.Up3Color;
            _settings.Down1Color = _work.Down1Color;
            _settings.Down3Color = _work.Down3Color;
            _settings.DividerColor = _work.DividerColor;
            _settings.ControlColor = _work.ControlColor;
            _settings.MarginMode = _work.MarginMode;
            _settings.WindowsNotification = _work.WindowsNotification;
            _settings.BaseUrl = _work.BaseUrl;
            _settings.TranslateRegion = _work.TranslateRegion;
            _settings.DbServer = _work.DbServer;
            _settings.DbName = _work.DbName;
            _settings.DbUser = _work.DbUser;
            DialogResult = true;
        }

        private async void ChangeBinanceApiKey_Click(object sender, RoutedEventArgs e) =>
            await ChangeSecret(SecretNames.BinanceApiKey);

        private async void ChangeBinanceSecret_Click(object sender, RoutedEventArgs e) =>
            await ChangeSecret(SecretNames.BinanceSecret);

        private async void ChangeTranslateKey_Click(object sender, RoutedEventArgs e) =>
            await ChangeSecret(SecretNames.AzureKey);

        private async void ChangeDbPassword_Click(object sender, RoutedEventArgs e) =>
            await ChangeSecret(SecretNames.DbPassword);

        private async Task ChangeSecret(string name)
        {
            var dlg = new ChangeSecretWindow(name) { Owner = this };
            if (dlg.ShowDialog() == true && dlg.SecretValue != null)
            {
                var enc = DpapiProtector.Protect(Encoding.UTF8.GetBytes(dlg.SecretValue));
                await _secretRepo.UpsertAsync(name, enc);
                _cache.Set(name, dlg.SecretValue);
            }
        }
    }
}
