using System.Drawing;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AI_AOI.Utils {
    internal class Convertor {
        public static BitmapSource Bitmap2BitmapSource(Bitmap bitmap) {
            var pixelFormat = bitmap.PixelFormat;
            PixelFormat format = PixelFormats.Bgr24;
            switch (pixelFormat) {
                case System.Drawing.Imaging.PixelFormat.Format8bppIndexed:
                    format = PixelFormats.Gray8;
                    break;
                case System.Drawing.Imaging.PixelFormat.Format24bppRgb:
                    format = PixelFormats.Bgr24;
                    break;
                case System.Drawing.Imaging.PixelFormat.Format32bppArgb:
                    format = PixelFormats.Bgra32;
                    break;
                default:
                    break;
            }
            var bitmapData = bitmap.LockBits(
                new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height),
                System.Drawing.Imaging.ImageLockMode.ReadOnly, bitmap.PixelFormat);
            var bitmapSource = BitmapSource.Create(
                bitmapData.Width, bitmapData.Height,
                bitmap.HorizontalResolution, bitmap.VerticalResolution,
                format, null,
                bitmapData.Scan0, bitmapData.Stride * bitmapData.Height, bitmapData.Stride);
            bitmap.UnlockBits(bitmapData);
            return bitmapSource;
        }

        public static Bitmap BitmapSourceToBitmap(BitmapSource bitmapSource) {
            using (MemoryStream memoryStream = new MemoryStream()) {
                // Save the BitmapSource to the MemoryStream
                if (bitmapSource == null)
                    return null;
                BitmapEncoder encoder = new BmpBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmapSource));
                encoder.Save(memoryStream);

                // Create a Bitmap from the MemoryStream
                memoryStream.Seek(0, SeekOrigin.Begin);
                Bitmap bitmap = new Bitmap(memoryStream);
                return bitmap;
            }
        }
    }
}
