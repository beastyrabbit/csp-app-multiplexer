using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ZXing;
using ZXing.Common;
using ZXing.QrCode;

namespace CspMultiplexer.App;

internal static class ProxyQrRenderer
{
    /// <summary>Encodes the pairing URL as a frozen 1-pixel-per-module BitmapSource.</summary>
    public static BitmapSource Render(string pairingUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pairingUrl);

        var writer = new BarcodeWriterGeneric
        {
            Format = BarcodeFormat.QR_CODE,
            Options = new QrCodeEncodingOptions
            {
                // Width/Height deliberately left at 0 so Encode returns the natural
                // module matrix. Setting them makes ZXing upscale to a pixel bitmap.
                Margin = 4,                 // QR spec requires a 4-module quiet zone.
                CharacterSet = "UTF-8",
            },
        };

        BitMatrix matrix = writer.Encode(pairingUrl);
        int w = matrix.Width, h = matrix.Height;

        var dark = ((SolidColorBrush)Application.Current.Resources["QrModuleBrush"]).Color;
        var light = ((SolidColorBrush)Application.Current.Resources["QrPaperBrush"]).Color;

        var pixels = new byte[w * h * 4];                       // Bgra32
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                var c = matrix[x, y] ? dark : light;
                int i = (y * w + x) * 4;
                pixels[i + 0] = c.B;
                pixels[i + 1] = c.G;
                pixels[i + 2] = c.R;
                pixels[i + 3] = 255;
            }
        }

        var bitmap = BitmapSource.Create(
            w, h, 96, 96, PixelFormats.Bgra32, null, pixels, w * 4);
        bitmap.Freeze();                                        // cross-thread safe, cheaper to render
        return bitmap;
    }
}
