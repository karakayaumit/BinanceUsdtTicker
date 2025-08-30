using System.Collections.Generic;

namespace BinanceUsdtTicker.Models
{
    public class UiSettings : BindableBase
    {
        private string _theme = "Light";
        public string Theme
        {
            get => _theme;
            set { if (_theme != value) { _theme = value; OnPropertyChanged(); } }
        }

        private string _filterMode = "All";
        public string FilterMode
        {
            get => _filterMode;
            set { if (_filterMode != value) { _filterMode = value; OnPropertyChanged(); } }
        }

        private List<ColumnState> _columns = new();
        public List<ColumnState> Columns
        {
            get => _columns;
            set { if (_columns != value) { _columns = value; OnPropertyChanged(); } }
        }

        private string _themeColor = string.Empty;
        public string ThemeColor
        {
            get => _themeColor;
            set { if (_themeColor != value) { _themeColor = value; OnPropertyChanged(); } }
        }

        private string _textColor = string.Empty;
        public string TextColor
        {
            get => _textColor;
            set { if (_textColor != value) { _textColor = value; OnPropertyChanged(); } }
        }

        private string _up1Color = string.Empty;
        public string Up1Color
        {
            get => _up1Color;
            set { if (_up1Color != value) { _up1Color = value; OnPropertyChanged(); } }
        }

        private string _up3Color = string.Empty;
        public string Up3Color
        {
            get => _up3Color;
            set { if (_up3Color != value) { _up3Color = value; OnPropertyChanged(); } }
        }

        private string _down1Color = string.Empty;
        public string Down1Color
        {
            get => _down1Color;
            set { if (_down1Color != value) { _down1Color = value; OnPropertyChanged(); } }
        }

        private string _down3Color = string.Empty;
        public string Down3Color
        {
            get => _down3Color;
            set { if (_down3Color != value) { _down3Color = value; OnPropertyChanged(); } }
        }

        private string _dividerColor = string.Empty;
        public string DividerColor
        {
            get => _dividerColor;
            set { if (_dividerColor != value) { _dividerColor = value; OnPropertyChanged(); } }
        }

        private string _controlColor = string.Empty;
        public string ControlColor
        {
            get => _controlColor;
            set { if (_controlColor != value) { _controlColor = value; OnPropertyChanged(); } }
        }

        private string _marginMode = "Isolated";
        public string MarginMode
        {
            get => _marginMode;
            set { if (_marginMode != value) { _marginMode = value; OnPropertyChanged(); } }
        }

        private bool _windowsNotification;
        public bool WindowsNotification
        {
            get => _windowsNotification;
            set { if (_windowsNotification != value) { _windowsNotification = value; OnPropertyChanged(); } }
        }

        private string _baseUrl = string.Empty;
        public string BaseUrl
        {
            get => _baseUrl;
            set { if (_baseUrl != value) { _baseUrl = value; OnPropertyChanged(); } }
        }

        private string _binanceApiKey = string.Empty;
        public string BinanceApiKey
        {
            get => _binanceApiKey;
            set { if (_binanceApiKey != value) { _binanceApiKey = value; OnPropertyChanged(); } }
        }

        private string _binanceApiSecret = string.Empty;
        public string BinanceApiSecret
        {
            get => _binanceApiSecret;
            set { if (_binanceApiSecret != value) { _binanceApiSecret = value; OnPropertyChanged(); } }
        }
    }
}
