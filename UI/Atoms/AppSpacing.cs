namespace PixelPerfect.UI.Atoms
{
    /// <summary>
    /// Spacing and radius constants from Figma design tokens.
    /// </summary>
    public static class AppSpacing
    {
        // Gap scale
        public const int GapXs  = 2;
        public const int GapSm  = 4;
        public const int GapXl  = 8;
        public const int Gap3Xl = 10;
        public const int Gap6Xl = 16;
        public const int Gap11X = 24;
        public const int Gap12X = 32;

        // Padding scale
        public const int PadSm  = 4;
        public const int PadMd  = 8;
        public const int PadLg  = 16;
        public const int PadXl  = 24;
        public const int Pad2Xl = 32;

        // Border radius
        public const int RadiusXs  = 2;
        public const int RadiusSm  = 4;
        public const int RadiusMd  = 5;
        public const int RadiusLg  = 6;
        public const int RadiusXl  = 8;
        public const int Radius2Xl = 9;
        public const int Radius4Xl = 12;
        public const int Radius11X = 20;
        public const int Radius12X = 24;
        public const int RadiusPill = int.MaxValue / 2; // fully rounded

        // Layout constants
        public const int LeftToolbarWidth    = 48;
        public const int TopBarHeight        = 44;
        public const int BottomToolbarHeight = 56;
        public const int RightPanelWidth     = 240;
        public const int RulerSize           = 20;
        public const int IconSize            = 24;
    }
}
