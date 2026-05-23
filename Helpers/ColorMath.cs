using System;
using System.Drawing;

namespace PixelPerfect.Helpers
{
    /// <summary>
    /// Pure static color-space conversion and math utilities.
    /// No GDI+ drawing here — only numeric conversions.
    /// </summary>
    public static class ColorMath
    {
        // ── Formatting ────────────────────────────────────────────────────────

        public static string ToHex(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";

        // ── RGB ↔ HSV ─────────────────────────────────────────────────────────

        public static (double H, double S, double V) ToHsv(Color c)
        {
            double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
            double max = Math.Max(r, Math.Max(g, b));
            double min = Math.Min(r, Math.Min(g, b));
            double delta = max - min;

            double h = 0;
            if (delta > 0)
            {
                if (max == r)      h = 60 * (((g - b) / delta) % 6);
                else if (max == g) h = 60 * (((b - r) / delta) + 2);
                else               h = 60 * (((r - g) / delta) + 4);
                if (h < 0) h += 360;
            }

            double s = max == 0 ? 0 : delta / max;
            return (h, s * 100, max * 100);
        }

        public static Color FromHsv(double h, double s, double v)
        {
            s /= 100.0; v /= 100.0;
            if (s == 0) { int gray = Clamp((int)(v * 255)); return Color.FromArgb(gray, gray, gray); }

            h %= 360; if (h < 0) h += 360;
            double sector = h / 60.0;
            int i = (int)sector;
            double f = sector - i;
            double p = v * (1 - s);
            double q = v * (1 - s * f);
            double t = v * (1 - s * (1 - f));

            double r, g, b;
            switch (i)
            {
                case 0:  r = v; g = t; b = p; break;
                case 1:  r = q; g = v; b = p; break;
                case 2:  r = p; g = v; b = t; break;
                case 3:  r = p; g = q; b = v; break;
                case 4:  r = t; g = p; b = v; break;
                default: r = v; g = p; b = q; break;
            }
            return Color.FromArgb(Clamp((int)(r * 255)), Clamp((int)(g * 255)), Clamp((int)(b * 255)));
        }

        // ── RGB ↔ CMYK ────────────────────────────────────────────────────────

        public static (double C, double M, double Y, double K) ToCmyk(Color c)
        {
            double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
            double k = 1 - Math.Max(r, Math.Max(g, b));
            if (k >= 1.0) return (0, 0, 0, 100);
            double d = 1 - k;
            return ((1 - r - k) / d * 100, (1 - g - k) / d * 100, (1 - b - k) / d * 100, k * 100);
        }

        public static Color FromCmyk(double c, double m, double y, double k)
        {
            c /= 100; m /= 100; y /= 100; k /= 100;
            int r = Clamp((int)((1 - c) * (1 - k) * 255));
            int g = Clamp((int)((1 - m) * (1 - k) * 255));
            int b = Clamp((int)((1 - y) * (1 - k) * 255));
            return Color.FromArgb(r, g, b);
        }

        // ── RGB ↔ CIE LAB ─────────────────────────────────────────────────────

        public static (double L, double A, double B) ToLab(Color c)
        {
            var (x, y, z) = RgbToXyz(c.R, c.G, c.B);
            // D65 reference white
            x /= 95.047; y /= 100.0; z /= 108.883;
            x = LabF(x); y = LabF(y); z = LabF(z);
            return (116 * y - 16, 500 * (x - y), 200 * (y - z));
        }

        public static Color FromLab(double l, double a, double b)
        {
            double fy = (l + 16) / 116.0;
            double fx = a / 500.0 + fy;
            double fz = fy - b / 200.0;
            double x = LabFInv(fx) * 95.047;
            double y = LabFInv(fy) * 100.0;
            double z = LabFInv(fz) * 108.883;
            return XyzToRgb(x, y, z);
        }

        // ── RGB ↔ YUV (BT.601) ───────────────────────────────────────────────

        public static (double Y, double U, double V) ToYuv(Color c)
        {
            double r = c.R, g = c.G, b = c.B;
            double y =  0.299 * r + 0.587 * g + 0.114 * b;
            double u = -0.14713 * r - 0.28886 * g + 0.436 * b;
            double v =  0.615 * r - 0.51499 * g - 0.10001 * b;
            return (y, u, v);
        }

        public static Color FromYuv(double y, double u, double v)
        {
            int r = Clamp((int)(y + 1.13983 * v));
            int g = Clamp((int)(y - 0.39465 * u - 0.58060 * v));
            int b = Clamp((int)(y + 2.03211 * u));
            return Color.FromArgb(r, g, b);
        }

        // ── RGB ↔ YCbCr (BT.601 digital) ─────────────────────────────────────

        public static (double Y, double Cb, double Cr) ToYCbCr(Color c)
        {
            double r = c.R, g = c.G, b = c.B;
            double y  =  16 + 0.257 * r + 0.504 * g + 0.098 * b;
            double cb = 128 - 0.148 * r - 0.291 * g + 0.439 * b;
            double cr = 128 + 0.439 * r - 0.368 * g - 0.071 * b;
            return (y, cb, cr);
        }

        public static Color FromYCbCr(double y, double cb, double cr)
        {
            double c = y - 16, d = cb - 128, e = cr - 128;
            int r = Clamp((int)(1.164 * c + 1.596 * e));
            int g = Clamp((int)(1.164 * c - 0.392 * d - 0.813 * e));
            int b = Clamp((int)(1.164 * c + 2.017 * d));
            return Color.FromArgb(r, g, b);
        }

        // ── Color adjustment (apply per-channel delta in a given color space) ──

        public static Color AdjustRgb(Color c, double dr, double dg, double db)
        {
            return Color.FromArgb(
                Clamp((int)(c.R + dr)),
                Clamp((int)(c.G + dg)),
                Clamp((int)(c.B + db)));
        }

        public static Color AdjustHsv(Color c, double dh, double ds, double dv)
        {
            var (h, s, v) = ToHsv(c);
            return FromHsv(h + dh, Math.Max(0, Math.Min(100, s + ds)), Math.Max(0, Math.Min(100, v + dv)));
        }

        public static Color AdjustCmyk(Color c, double dc, double dm, double dy, double dk)
        {
            var (cv, mv, yv, kv) = ToCmyk(c);
            return FromCmyk(
                Math.Max(0, Math.Min(100, cv + dc)),
                Math.Max(0, Math.Min(100, mv + dm)),
                Math.Max(0, Math.Min(100, yv + dy)),
                Math.Max(0, Math.Min(100, kv + dk)));
        }

        public static Color AdjustLab(Color c, double dl, double da, double db)
        {
            var (l, a, b) = ToLab(c);
            return FromLab(l + dl, a + da, b + db);
        }

        public static Color AdjustYuv(Color c, double dy, double du, double dv)
        {
            var (y, u, v) = ToYuv(c);
            return FromYuv(y + dy, u + du, v + dv);
        }

        public static Color AdjustYCbCr(Color c, double dy, double dcb, double dcr)
        {
            var (y, cb, cr) = ToYCbCr(c);
            return FromYCbCr(y + dy, cb + dcb, cr + dcr);
        }

        // ── Utility ───────────────────────────────────────────────────────────

        public static byte Clamp(int v) => (byte)Math.Max(0, Math.Min(255, v));

        // ── Private XYZ helpers ───────────────────────────────────────────────

        private static (double X, double Y, double Z) RgbToXyz(int r, int g, int b)
        {
            double rd = GammaExpand(r / 255.0);
            double gd = GammaExpand(g / 255.0);
            double bd = GammaExpand(b / 255.0);
            return (
                rd * 41.24 + gd * 35.76 + bd * 18.05,
                rd * 21.26 + gd * 71.52 + bd * 7.22,
                rd * 1.93  + gd * 11.92 + bd * 95.05);
        }

        private static Color XyzToRgb(double x, double y, double z)
        {
            x /= 100; y /= 100; z /= 100;
            double r =  x * 3.2406 - y * 1.5372 - z * 0.4986;
            double g = -x * 0.9689 + y * 1.8758 + z * 0.0415;
            double b =  x * 0.0557 - y * 0.2040 + z * 1.0570;
            return Color.FromArgb(
                Clamp((int)(GammaCompress(r) * 255)),
                Clamp((int)(GammaCompress(g) * 255)),
                Clamp((int)(GammaCompress(b) * 255)));
        }

        private static double GammaExpand(double v)
            => v > 0.04045 ? Math.Pow((v + 0.055) / 1.055, 2.4) : v / 12.92;

        private static double GammaCompress(double v)
            => v > 0.0031308 ? 1.055 * Math.Pow(v, 1.0 / 2.4) - 0.055 : 12.92 * v;

        private static double LabF(double t)
            => t > 0.008856 ? Math.Pow(t, 1.0 / 3.0) : 7.787 * t + 16.0 / 116.0;

        private static double LabFInv(double t)
            => t > 0.2069 ? t * t * t : (t - 16.0 / 116.0) / 7.787;
    }
}
