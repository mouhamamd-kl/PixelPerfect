using System;
using System.Drawing;

namespace PixelPerfect.Models
{
    public class ImageModel : IDisposable
    {
        private Bitmap _originalBitmap;
        private bool _disposed;

        public Bitmap OriginalBitmap => _originalBitmap;
        public Bitmap WorkingBitmap { get; set; }
        public string FilePath { get; private set; }
        public string Format { get; set; }
        public long FileSizeBytes { get; set; }
        public string ColorMode { get; set; }

        public int Width => _originalBitmap?.Width ?? 0;
        public int Height => _originalBitmap?.Height ?? 0;

        public void LoadFrom(Bitmap bmp, string filePath)
        {
            _originalBitmap?.Dispose();
            WorkingBitmap?.Dispose();

            _originalBitmap = new Bitmap(bmp);
            WorkingBitmap = new Bitmap(bmp);
            FilePath = filePath;
        }

        public void ResetToOriginal()
        {
            WorkingBitmap?.Dispose();
            WorkingBitmap = new Bitmap(_originalBitmap);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _originalBitmap?.Dispose();
            WorkingBitmap?.Dispose();
        }
    }
}
