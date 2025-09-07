using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace BinanceUsdtTicker
{
    public class SourceToLogoConverter : IValueConverter
    {
        public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not string src)
                return null;

            src = src.ToLowerInvariant();
            return src switch
            {
                "bybit" => CreateLogo(Color.FromRgb(0xF7, 0x93, 0x1A)),
                "kucoin" => CreateLogo(Color.FromRgb(0x28, 0xD1, 0xA7)),
                "okx" => CreateLogo(Colors.Black),
                _ => null,
            };
        }

        private static ImageSource CreateLogo(Color background)
        {
            const int size = 32;
            var dv = new DrawingVisual();
            using (var ctx = dv.RenderOpen())
            {
                ctx.DrawRectangle(new SolidColorBrush(background), null, new Rect(0, 0, size, size));
            }

            var bmp = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
            bmp.Render(dv);
            return bmp;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
