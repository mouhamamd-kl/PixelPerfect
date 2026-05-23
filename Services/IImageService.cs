using System.Collections.Generic;
using System.Drawing;
using PixelPerfect.Models;

namespace PixelPerfect.Services
{
    public interface IImageService
    {
        /// <summary>Loads a bitmap from disk and wraps it in an ImageModel.</summary>
        ImageModel LoadImage(string filePath);

        /// <summary>Saves the working bitmap to disk in the requested format.</summary>
        void ExportImage(ImageModel model, ExportSettings settings);

        /// <summary>
        /// Returns a new Bitmap with per-channel color adjustments applied
        /// in the active color space. Does not mutate the source.
        /// </summary>
        Bitmap ApplyColorSettings(Bitmap source, ColorSettings settings);

        /// <summary>Returns the color of a single pixel, bounds-checked.</summary>
        Color SamplePixel(Bitmap bmp, int x, int y);

        /// <summary>
        /// Extracts the N most dominant colors from the bitmap
        /// using the median-cut algorithm.
        /// </summary>
        IReadOnlyList<Color> GetDominantColors(Bitmap bmp, int count);
    }
}
