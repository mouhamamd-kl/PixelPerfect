using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text.RegularExpressions;
using Svg;

namespace PixelPerfect.Helpers
{
    /// <summary>
    /// Loads SVG icon files from the icons\ directory, recolors them, and renders
    /// them to a Bitmap at the requested size. Results are cached by file+size+color.
    /// </summary>
    public static class SvgIconHelper
    {
        private static readonly Dictionary<string, Bitmap> _cache = new();
        private static readonly string _iconsDir = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "icons");

        /// <summary>
        /// Returns a cached Bitmap for the named icon at the given size and color.
        /// The color replaces all stroke and fill attributes in the SVG.
        /// Returns null if the file is not found.
        /// </summary>
        public static Bitmap? Load(string svgFileName, int size, Color targetColor)
        {
            string key = $"{svgFileName}_{size}_{targetColor.ToArgb()}";
            if (_cache.TryGetValue(key, out Bitmap? cached))
                return cached;

            string path = Path.Combine(_iconsDir, svgFileName);
            if (!File.Exists(path))
                return null;

            try
            {
                string svg = File.ReadAllText(path);
                svg = RecolorSvg(svg, targetColor);

                using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(svg));
                var doc = SvgDocument.Open<SvgDocument>(stream);
                doc.Width  = new SvgUnit(SvgUnitType.Pixel, size);
                doc.Height = new SvgUnit(SvgUnitType.Pixel, size);

                var bmp = new Bitmap(size, size);
                using (var g = Graphics.FromImage(bmp))
                {
                    g.Clear(Color.Transparent);
                    doc.Draw(g);
                }

                _cache[key] = bmp;
                return bmp;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>Clears all cached bitmaps (call on theme change).</summary>
        public static void ClearCache()
        {
            foreach (var bmp in _cache.Values)
                bmp?.Dispose();
            _cache.Clear();
        }

        // ── Recolor ───────────────────────────────────────────────────────────

        /// <summary>
        /// Replaces all stroke="#..." and fill="#..." color values in the SVG XML
        /// with the target color, preserving fill="none" entries.
        /// </summary>
        private static string RecolorSvg(string svg, Color color)
        {
            string hex = $"#{color.R:X2}{color.G:X2}{color.B:X2}";

            // Replace stroke="<any hex color>" — but not stroke="none"
            svg = Regex.Replace(svg,
                @"stroke=""#[0-9A-Fa-f]{3,8}""",
                $"stroke=\"{hex}\"");

            // Replace fill="<any hex color>" — but not fill="none"
            svg = Regex.Replace(svg,
                @"fill=""#[0-9A-Fa-f]{3,8}""",
                $"fill=\"{hex}\"");

            return svg;
        }
    }
}
