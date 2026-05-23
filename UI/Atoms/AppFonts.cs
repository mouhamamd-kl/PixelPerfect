using System.Drawing;

namespace PixelPerfect.UI.Atoms
{
    /// <summary>
    /// Shared font instances. Call DisposeAll() on application exit.
    /// Sizes derived from Figma spacing scale (gap/3xl=10, gap/5xl=14.4, etc.)
    /// </summary>
    public static class AppFonts
    {
        public static readonly Font Small   = new Font("Segoe UI", 7.5f,  FontStyle.Regular);
        public static readonly Font Label   = new Font("Segoe UI", 8.5f,  FontStyle.Regular);
        public static readonly Font Value   = new Font("Segoe UI", 8.5f,  FontStyle.Bold);
        public static readonly Font Body    = new Font("Segoe UI", 9f,    FontStyle.Regular);
        public static readonly Font Header  = new Font("Segoe UI", 10f,   FontStyle.Regular);
        public static readonly Font Version = new Font("Segoe UI", 7f,    FontStyle.Regular);
        public static readonly Font Mono    = new Font("Consolas",  8.5f, FontStyle.Regular);

        public static void DisposeAll()
        {
            Small.Dispose();
            Label.Dispose();
            Value.Dispose();
            Body.Dispose();
            Header.Dispose();
            Version.Dispose();
            Mono.Dispose();
        }
    }
}
