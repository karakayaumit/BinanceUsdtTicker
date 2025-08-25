using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace BinanceUsdtTicker
{
    /// <summary>
    /// Converts trade side (BUY/SELL) to a foreground brush.
    /// BUY -> Green, SELL -> Red.
    /// </summary>
    public class SideToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var side = value as string;
            if (string.Equals(side, "BUY", StringComparison.OrdinalIgnoreCase))
                return Brushes.Green;
            if (string.Equals(side, "SELL", StringComparison.OrdinalIgnoreCase))
                return Brushes.Red;
            return Brushes.Gray;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
