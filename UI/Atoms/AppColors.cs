using System;
using System.Drawing;

namespace PixelPerfect.UI.Atoms
{
    /// <summary>
    /// Central color palette derived from Figma design tokens.
    /// Default is the light theme. Call ToggleTheme() to switch.
    /// Subscribe to ThemeChanged to repaint UI on theme switch.
    /// </summary>
    public static class AppColors
    {
        public static bool IsDarkTheme { get; private set; } = false;

        // ── Backgrounds ───────────────────────────────────────────────────────
        public static Color Background   { get; private set; }   // app-level bg
        public static Color PanelBg      { get; private set; }   // sidebar / panel
        public static Color Canvas       { get; private set; }   // image canvas area
        public static Color SurfaceAlt   { get; private set; }   // input fields, chips

        // ── Text ─────────────────────────────────────────────────────────────
        public static Color TextPrimary   { get; private set; }
        public static Color TextSecondary { get; private set; }
        public static Color TextWhite     { get; private set; }
        public static Color TextOrange    { get; private set; }

        // ── Accent ───────────────────────────────────────────────────────────
        public static Color Accent        { get; private set; }   // #FA7B3D orange
        public static Color AccentDark    { get; private set; }   // #DB6A33

        // ── Borders / Strokes ────────────────────────────────────────────────
        public static Color BorderPrimary { get; private set; }
        public static Color BorderDark    { get; private set; }
        public static Color BorderGray    { get; private set; }
        public static Color BorderOrange  { get; private set; }

        // ── Icons ────────────────────────────────────────────────────────────
        public static Color IconNormal    { get; private set; }   // default icon stroke
        public static Color IconHover     { get; private set; }   // hover icon stroke
        public static Color IconActive    { get; private set; }   // active/selected icon

        // ── Ruler ────────────────────────────────────────────────────────────
        public static Color RulerBg       { get; private set; }
        public static Color RulerTick     { get; private set; }

        // ── Status/Channel colors (fixed, not theme-dependent) ────────────────
        public static readonly Color ChannelRed    = Color.FromArgb(0xF0, 0x10, 0x39);
        public static readonly Color ChannelGreen  = Color.FromArgb(0x27, 0xC9, 0x3F);
        public static readonly Color ChannelBlue   = Color.FromArgb(0x46, 0x6B, 0xFF);
        public static readonly Color ChannelYellow = Color.FromArgb(0xFF, 0xBD, 0x2E);
        public static readonly Color ChannelPurple = Color.FromArgb(0xAD, 0x46, 0xFF);
        public static readonly Color ChannelCyan   = Color.FromArgb(0x51, 0xA2, 0xFF);

        // ── Event ─────────────────────────────────────────────────────────────
        public static event EventHandler ThemeChanged;

        static AppColors()
        {
            ApplyLightTheme();
        }

        public static void ToggleTheme()
        {
            if (IsDarkTheme) ApplyLightTheme();
            else ApplyDarkTheme();
            ThemeChanged?.Invoke(null, EventArgs.Empty);
        }

        // ── Light theme (Figma design tokens) ────────────────────────────────

        private static void ApplyLightTheme()
        {
            IsDarkTheme    = false;

            Background     = Color.FromArgb(0xF1, 0xF2, 0xF4);  // Semantic/bg/secondary
            PanelBg        = Color.White;                          // Collection1/Bg/Panels
            Canvas         = Color.FromArgb(0xE5, 0xE7, 0xEB);  // Collection1/Bg/Canvas
            SurfaceAlt     = Color.FromArgb(0xF3, 0xF4, 0xF6);  // Semantic/surface/tertiary

            TextPrimary    = Color.FromArgb(0x1C, 0x1C, 0x1E);  // Collection1/Text/Primary
            TextSecondary  = Color.FromArgb(0x6B, 0x72, 0x80);  // Collection1/Text/Secondary
            TextWhite      = Color.White;
            TextOrange     = Color.FromArgb(0xFA, 0x7B, 0x3D);  // Semantic/text/quaternary

            Accent         = Color.FromArgb(0xFA, 0x7B, 0x3D);  // Collection1/PrimaryOrange
            AccentDark     = Color.FromArgb(0xDB, 0x6A, 0x33);  // Semantic/text/senary

            BorderPrimary  = Color.FromArgb(0xE5, 0xE7, 0xEB);  // Semantic/border/primary
            BorderDark     = Color.FromArgb(0x1C, 0x27, 0x4C);  // Semantic/border/quaternary
            BorderGray     = Color.FromArgb(0x6B, 0x72, 0x80);  // Semantic/border/secondary
            BorderOrange   = Color.FromArgb(0xFA, 0x7B, 0x3D);  // Semantic/border/senary

            IconNormal     = Color.FromArgb(0x6B, 0x72, 0x80);  // Semantic/icon/primary
            IconHover      = Color.FromArgb(0x1C, 0x1C, 0x1E);  // TextPrimary on hover
            IconActive     = Color.FromArgb(0xFA, 0x7B, 0x3D);  // Accent orange when active

            RulerBg        = Color.White;
            RulerTick      = Color.FromArgb(0x6B, 0x72, 0x80);
        }

        // ── Dark theme (inverted) ─────────────────────────────────────────────

        private static void ApplyDarkTheme()
        {
            IsDarkTheme    = true;

            Background     = Color.FromArgb(0x12, 0x12, 0x12);
            PanelBg        = Color.FromArgb(0x1E, 0x1E, 0x1E);
            Canvas         = Color.FromArgb(0x32, 0x32, 0x32);
            SurfaceAlt     = Color.FromArgb(0x2A, 0x2A, 0x2A);

            TextPrimary    = Color.FromArgb(0xE6, 0xE6, 0xE6);
            TextSecondary  = Color.FromArgb(0x8C, 0x8C, 0x8C);
            TextWhite      = Color.White;
            TextOrange     = Color.FromArgb(0xFA, 0x7B, 0x3D);

            Accent         = Color.FromArgb(0xFA, 0x7B, 0x3D);
            AccentDark     = Color.FromArgb(0xFF, 0x95, 0x55);

            BorderPrimary  = Color.FromArgb(0x37, 0x37, 0x37);
            BorderDark     = Color.FromArgb(0x55, 0x55, 0x55);
            BorderGray     = Color.FromArgb(0x55, 0x55, 0x55);
            BorderOrange   = Color.FromArgb(0xFA, 0x7B, 0x3D);

            IconNormal     = Color.FromArgb(0x6B, 0x72, 0x80);
            IconHover      = Color.FromArgb(0xE6, 0xE6, 0xE6);
            IconActive     = Color.FromArgb(0xFA, 0x7B, 0x3D);

            RulerBg        = Color.FromArgb(0x26, 0x26, 0x26);
            RulerTick      = Color.FromArgb(0x6E, 0x6E, 0x6E);
        }
    }
}
