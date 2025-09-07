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
                "bybit" => CreateBybitLogo(),
                "kucoin" => CreateKucoinLogo(),
                "okx" => CreateOkxLogo(),
                _ => null,
            };
        }

        private static ImageSource CreateBybitLogo()
        {
            const int size = 64;
            var dv = new DrawingVisual();
            using (var ctx = dv.RenderOpen())
            {
                ctx.DrawRectangle(Brushes.Black, null, new Rect(0, 0, size, size));

                var typeface = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
                var dpi = VisualTreeHelper.GetDpi(dv).PixelsPerDip;

                var part1 = new FormattedText("BYB", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, 24, Brushes.White, dpi);
                var part2 = new FormattedText("I", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, 24, new SolidColorBrush(Color.FromRgb(0xF7, 0x93, 0x1A)), dpi);
                var part3 = new FormattedText("T", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, 24, Brushes.White, dpi);

                var total = part1.Width + part2.Width + part3.Width;
                var x = (size - total) / 2;
                var y = (size - part1.Height) / 2;

                ctx.DrawText(part1, new Point(x, y));
                ctx.DrawText(part2, new Point(x + part1.Width, y));
                ctx.DrawText(part3, new Point(x + part1.Width + part2.Width, y));
            }

            var bmp = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
            bmp.Render(dv);
            bmp.Freeze();
            return bmp;
        }

        private static ImageSource CreateKucoinLogo()
        {
            var dv = new DrawingVisual();
            var brush = new SolidColorBrush(Color.FromRgb(0x28, 0xD1, 0xA7));
            var typeface = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);

            double width, height;
            using (var ctx = dv.RenderOpen())
            {
                var dpi = VisualTreeHelper.GetDpi(dv).PixelsPerDip;
                const string text = "KUCOIN";
                var ft = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, 48, brush, dpi);

                const double padding = 10;
                width = ft.Width + padding * 2;
                height = ft.Height + padding * 2;

                ctx.DrawText(ft, new Point(padding, padding));
            }

            var bmp = new RenderTargetBitmap((int)Math.Ceiling(width), (int)Math.Ceiling(height), 96, 96, PixelFormats.Pbgra32);
            bmp.Render(dv);
            bmp.Freeze();
            return bmp;
        }

        private static ImageSource CreateOkxLogo()
        {
            const int size = 64;
            var dv = new DrawingVisual();

            // Predefine commonly reused values to avoid "variable does not exist"
            // compiler errors when rendering the logo text.
            var background = Brushes.Black;
            var typeface = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
            var dpi = VisualTreeHelper.GetDpi(dv).PixelsPerDip;

            using (var ctx = dv.RenderOpen())
            {
                var ft = new FormattedText("OKX", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, 24, Brushes.White, dpi);
                var p = new Point((size - ft.Width) / 2, (size - ft.Height) / 2);
                ctx.DrawText(ft, p);
            }

            var bmp = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
            bmp.Render(dv);
            bmp.Freeze();
            return bmp;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
