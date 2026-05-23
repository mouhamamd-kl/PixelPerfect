namespace PixelPerfect.Models
{
    public class ExportSettings
    {
        public string Format { get; set; } = "PNG";  // "PNG", "JPEG", "BMP"
        public string SavePath { get; set; }
        public int JpegQuality { get; set; } = 90;
    }
}
