using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using PixelPerfect.Helpers;
using PixelPerfect.Models;

namespace PixelPerfect.Services
{
    public class ImageService : IImageService
    {
        public ImageModel LoadImage(string filePath)
        {
            var raw = new Bitmap(filePath);
            var model = new ImageModel();
            model.LoadFrom(raw, filePath);
            raw.Dispose(); // LoadFrom clones it

            model.Format       = DetectFormat(filePath);
            model.FileSizeBytes = new FileInfo(filePath).Length;
            model.ColorMode    = DetectColorMode(model.OriginalBitmap);
            return model;
        }

        public void ExportImage(ImageModel model, ExportSettings settings)
        {
            if (model == null || model.WorkingBitmap == null) return;

            string dir = Path.GetDirectoryName(settings.SavePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            switch (settings.Format.ToUpperInvariant())
            {
                case "JPEG":
                case "JPG":
                    SaveJpeg(model.WorkingBitmap, settings.SavePath, settings.JpegQuality);
                    break;
                case "BMP":
                    model.WorkingBitmap.Save(settings.SavePath, ImageFormat.Bmp);
                    break;
                default: // PNG
                    model.WorkingBitmap.Save(settings.SavePath, ImageFormat.Png);
                    break;
            }
        }

        public Bitmap ApplyColorSettings(Bitmap source, ColorSettings settings)
        {
            if (settings.IsDefault) return new Bitmap(source);

            Bitmap result = BitmapHelper.ApplyColorSettings(source, settings);

            if (settings.QuantizeCount > 0)
            {
                Bitmap quantized = BitmapHelper.ApplyQuantization(result, settings.QuantizeCount);
                result.Dispose();
                result = quantized;
            }

            return result;
        }

        public Color SamplePixel(Bitmap bmp, int x, int y)
            => BitmapHelper.SamplePixel(bmp, x, y);

        public IReadOnlyList<Color> GetDominantColors(Bitmap bmp, int count)
        {
            // Re-use the quantization palette extraction
            if (bmp == null || count <= 0) return Array.Empty<Color>();

            // Apply quantization to extract palette via median-cut
            using var quantized = BitmapHelper.ApplyQuantization(bmp, count);

            // Collect unique colors from the quantized bitmap (just sample the palette)
            var set = new HashSet<int>();
            var colors = new List<Color>(count);
            var data = quantized.LockBits(
                new Rectangle(0, 0, quantized.Width, quantized.Height),
                ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);

            int bytes = Math.Abs(data.Stride) * quantized.Height;
            byte[] arr = new byte[bytes];
            Marshal.Copy(data.Scan0, arr, 0, bytes);
            quantized.UnlockBits(data);

            int step = Math.Max(4, bytes / (count * 100));
            for (int i = 0; i < bytes && colors.Count < count * 4; i += step)
            {
                int argb = BitConverter.ToInt32(arr, i & ~3);
                if (set.Add(argb))
                    colors.Add(Color.FromArgb(argb));
            }

            return colors.Take(count).ToList();
        }

        // ── Private helpers ───────────────────────────────────────────────────

        private static string DetectFormat(string filePath)
        {
            string ext = Path.GetExtension(filePath).ToUpperInvariant();
            switch (ext)
            {
                case ".JPG":
                case ".JPEG": return "JPEG";
                case ".BMP":  return "BMP";
                case ".GIF":  return "GIF";
                case ".TIFF":
                case ".TIF":  return "TIFF";
                default:      return "PNG";
            }
        }

        private static string DetectColorMode(Bitmap bmp)
        {
            // Quick grayscale check: sample up to 1000 pixels
            int step = Math.Max(1, (bmp.Width * bmp.Height) / 1000);
            var data = bmp.LockBits(
                new Rectangle(0, 0, bmp.Width, bmp.Height),
                ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            byte[] arr = new byte[Math.Abs(data.Stride) * bmp.Height];
            Marshal.Copy(data.Scan0, arr, 0, arr.Length);
            bmp.UnlockBits(data);

            for (int i = 0; i < arr.Length; i += 4 * step)
            {
                if (arr[i] != arr[i + 1] || arr[i + 1] != arr[i + 2])
                    return "RGB / 8-bit";
            }
            return "Grayscale / 8-bit";
        }

        private static void SaveJpeg(Bitmap bmp, string path, int quality)
        {
            var codec = GetJpegCodec();
            if (codec == null) { bmp.Save(path, ImageFormat.Jpeg); return; }

            using var ep = new EncoderParameters(1);
            ep.Param[0] = new EncoderParameter(Encoder.Quality, (long)quality);
            bmp.Save(path, codec, ep);
        }

        private static ImageCodecInfo GetJpegCodec()
        {
            foreach (var c in ImageCodecInfo.GetImageEncoders())
                if (c.MimeType == "image/jpeg") return c;
            return null;
        }
    }
}
