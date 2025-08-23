using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace BinanceUsdtTicker
{
    /// <summary>
    /// Nokta/virg�l esnek ondal�k d�n��t�r�c�.
    /// Yazarken bozmamak i�in parse edilemeyen veya "tamamlanmam��"
    /// (1., 1,) girdilerde kayna�� g�ncellemez (Binding.DoNothing).
    /// </summary>
    public class FlexibleDoubleConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double d)
            {
                // Ekranda yerel ondal�k ay�rac�yla g�ster
                var s = d.ToString("0.########", CultureInfo.InvariantCulture);
                var dec = culture.NumberFormat.NumberDecimalSeparator;
                return s.Replace(".", dec);
            }
            return "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var s = (value?.ToString() ?? "").Trim();
            if (s.Length == 0) return Binding.DoNothing;

            // "tamamlanmam��" durumlar: sona . veya , eklenmi�se �imdi yazmayal�m
            if (s.EndsWith(".") || s.EndsWith(",")) return Binding.DoNothing;

            // Hem . hem , varsa en sondaki ondal�kt�r, di�eri binlik kabul edilir
            int iDot = s.LastIndexOf('.');
            int iCom = s.LastIndexOf(',');
            if (iDot >= 0 && iCom >= 0)
            {
                if (iCom > iDot) { s = s.Replace(".", ""); s = s.Replace(',', '.'); }
                else { s = s.Replace(",", ""); }
            }
            else if (iCom >= 0)
            {
                s = s.Replace(',', '.');
            }

            if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
                return d;

            // Ge�ici olarak kabul etme; kullan�c� yazmaya devam edebilir
            return Binding.DoNothing;
        }
    }
}
