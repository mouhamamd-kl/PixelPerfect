using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using PixelPerfect.Models;

namespace PixelPerfect.Helpers
{
    public static class BitmapHelper
    {
        // ── Color Adjustment ─────────────────────────────────────────────────

        /// <summary>
        /// Returns a new Bitmap with per-channel adjustments applied in the
        /// active color space. Uses LockBits for performance.
        /// </summary>
        public static Bitmap ApplyColorSettings(Bitmap source, ColorSettings settings)
        {
            if (settings.IsDefault) return new Bitmap(source);

            var result = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb);
            var srcData = source.LockBits(
                new Rectangle(0, 0, source.Width, source.Height),
                ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            var dstData = result.LockBits(
                new Rectangle(0, 0, result.Width, result.Height),
                ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

            int bytes = Math.Abs(srcData.Stride) * source.Height;
            byte[] src = new byte[bytes];
            byte[] dst = new byte[bytes];
            Marshal.Copy(srcData.Scan0, src, 0, bytes);

            for (int i = 0; i < bytes; i += 4)
            {
                // BGRA order
                byte b = src[i], g = src[i + 1], r = src[i + 2], a = src[i + 3];
                Color adjusted = AdjustPixel(Color.FromArgb(a, r, g, b), settings);
                dst[i]     = adjusted.B;
                dst[i + 1] = adjusted.G;
                dst[i + 2] = adjusted.R;
                dst[i + 3] = adjusted.A;
            }

            Marshal.Copy(dst, 0, dstData.Scan0, bytes);
            source.UnlockBits(srcData);
            result.UnlockBits(dstData);
            return result;
        }

        private static Color AdjustPixel(Color c, ColorSettings s)
        {
            switch (s.ActiveColorSpace)
            {
                case Models.ColorSpaceMode.RGB:
                    return ColorMath.AdjustRgb(c, s.Channel1, s.Channel2, s.Channel3);
                case Models.ColorSpaceMode.HSV:
                    return ColorMath.AdjustHsv(c, s.Channel1, s.Channel2, s.Channel3);
                case Models.ColorSpaceMode.CMYK:
                    return ColorMath.AdjustCmyk(c, s.Channel1, s.Channel2, s.Channel3, s.Channel4);
                case Models.ColorSpaceMode.LAB:
                    return ColorMath.AdjustLab(c, s.Channel1, s.Channel2, s.Channel3);
                case Models.ColorSpaceMode.YUV:
                    return ColorMath.AdjustYuv(c, s.Channel1, s.Channel2, s.Channel3);
                case Models.ColorSpaceMode.YCbCr:
                    return ColorMath.AdjustYCbCr(c, s.Channel1, s.Channel2, s.Channel3);
                default:
                    return c;
            }
        }

        // ── Safe pixel sampling ───────────────────────────────────────────────

        public static Color SamplePixel(Bitmap bmp, int x, int y)
        {
            if (bmp == null) return Color.Transparent;
            if (x < 0 || x >= bmp.Width || y < 0 || y >= bmp.Height) return Color.Transparent;
            return bmp.GetPixel(x, y);
        }

        // ── Color Quantization (Median-Cut) ───────────────────────────────────

        public static Bitmap ApplyQuantization(Bitmap source, int targetColorCount)
        {
            if (targetColorCount <= 0) return new Bitmap(source);

            Color[] samples = SampleColors(source, 50000);
            Color[] palette = MedianCut(samples, targetColorCount);

            var result = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb);
            var srcData = source.LockBits(
                new Rectangle(0, 0, source.Width, source.Height),
                ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            var dstData = result.LockBits(
                new Rectangle(0, 0, result.Width, result.Height),
                ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

            int bytes = Math.Abs(srcData.Stride) * source.Height;
            byte[] src = new byte[bytes];
            byte[] dst = new byte[bytes];
            Marshal.Copy(srcData.Scan0, src, 0, bytes);

            for (int i = 0; i < bytes; i += 4)
            {
                Color original = Color.FromArgb(src[i + 3], src[i + 2], src[i + 1], src[i]);
                Color nearest  = FindNearest(original, palette);
                dst[i]     = nearest.B;
                dst[i + 1] = nearest.G;
                dst[i + 2] = nearest.R;
                dst[i + 3] = src[i + 3]; // preserve alpha
            }

            Marshal.Copy(dst, 0, dstData.Scan0, bytes);
            source.UnlockBits(srcData);
            result.UnlockBits(dstData);
            return result;
        }

        private static Color[] SampleColors(Bitmap bmp, int maxSamples)
        {
            int total = bmp.Width * bmp.Height;
            int step  = Math.Max(1, total / maxSamples);
            var list  = new List<Color>(maxSamples);

            var data = bmp.LockBits(
                new Rectangle(0, 0, bmp.Width, bmp.Height),
                ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            int bytes = Math.Abs(data.Stride) * bmp.Height;
            byte[] arr = new byte[bytes];
            Marshal.Copy(data.Scan0, arr, 0, bytes);
            bmp.UnlockBits(data);

            for (int i = 0; i < bytes; i += 4 * step)
                list.Add(Color.FromArgb(arr[i + 2], arr[i + 1], arr[i]));

            return list.ToArray();
        }

        private static Color[] MedianCut(Color[] colors, int targetCount)
        {
            var boxes = new List<List<Color>> { new List<Color>(colors) };

            while (boxes.Count < targetCount)
            {
                // Find the box with the largest range
                int splitIdx = 0;
                int maxRange = -1;
                for (int i = 0; i < boxes.Count; i++)
                {
                    int range = GetLargestRange(boxes[i]);
                    if (range > maxRange) { maxRange = range; splitIdx = i; }
                }
                if (maxRange == 0) break;

                var box = boxes[splitIdx];
                boxes.RemoveAt(splitIdx);
                int axis = GetWidestAxis(box);
                box.Sort((a, b) => GetChannel(a, axis).CompareTo(GetChannel(b, axis)));
                int mid = box.Count / 2;
                boxes.Add(box.GetRange(0, mid));
                boxes.Add(box.GetRange(mid, box.Count - mid));
            }

            var palette = new Color[boxes.Count];
            for (int i = 0; i < boxes.Count; i++)
                palette[i] = AverageColor(boxes[i]);
            return palette;
        }

        private static int GetLargestRange(List<Color> box)
        {
            int rMin = 255, rMax = 0, gMin = 255, gMax = 0, bMin = 255, bMax = 0;
            foreach (var c in box)
            {
                if (c.R < rMin) rMin = c.R; if (c.R > rMax) rMax = c.R;
                if (c.G < gMin) gMin = c.G; if (c.G > gMax) gMax = c.G;
                if (c.B < bMin) bMin = c.B; if (c.B > bMax) bMax = c.B;
            }
            return Math.Max(rMax - rMin, Math.Max(gMax - gMin, bMax - bMin));
        }

        private static int GetWidestAxis(List<Color> box)
        {
            int rMin = 255, rMax = 0, gMin = 255, gMax = 0, bMin = 255, bMax = 0;
            foreach (var c in box)
            {
                if (c.R < rMin) rMin = c.R; if (c.R > rMax) rMax = c.R;
                if (c.G < gMin) gMin = c.G; if (c.G > gMax) gMax = c.G;
                if (c.B < bMin) bMin = c.B; if (c.B > bMax) bMax = c.B;
            }
            int rRange = rMax - rMin, gRange = gMax - gMin, bRange = bMax - bMin;
            if (rRange >= gRange && rRange >= bRange) return 0;
            if (gRange >= rRange && gRange >= bRange) return 1;
            return 2;
        }

        private static int GetChannel(Color c, int axis)
        {
            switch (axis) { case 0: return c.R; case 1: return c.G; default: return c.B; }
        }

        private static Color AverageColor(List<Color> box)
        {
            if (box.Count == 0) return Color.Black;
            long r = 0, g = 0, b = 0;
            foreach (var c in box) { r += c.R; g += c.G; b += c.B; }
            int n = box.Count;
            return Color.FromArgb((int)(r / n), (int)(g / n), (int)(b / n));
        }

        private static Color FindNearest(Color c, Color[] palette)
        {
            Color best = palette[0];
            long bestDist = long.MaxValue;
            foreach (var p in palette)
            {
                long dr = c.R - p.R, dg = c.G - p.G, db = c.B - p.B;
                long dist = dr * dr + dg * dg + db * db;
                if (dist < bestDist) { bestDist = dist; best = p; }
            }
            return best;
        }
    }
}
