namespace PixelPerfect.Models
{
    public enum ColorSpaceMode
    {
        RGB,
        HSV,
        CMYK,
        LAB,
        YUV,
        YCbCr
    }

    /// <summary>
    /// Holds per-channel adjustment values for the active color space.
    /// Channel semantics depend on ActiveColorSpace:
    ///   RGB    → Channel1=R, Channel2=G, Channel3=B  (range -255..+255)
    ///   HSV    → Channel1=H(-180..+180), Channel2=S(-100..+100), Channel3=V(-100..+100)
    ///   CMYK   → Channel1=C, Channel2=M, Channel3=Y, Channel4=K (-100..+100)
    ///   LAB    → Channel1=L(-100..+100), Channel2=a(-128..+128), Channel3=b(-128..+128)
    ///   YUV    → Channel1=Y(-255..+255), Channel2=U(-112..+112), Channel3=V(-157..+157)
    ///   YCbCr  → Channel1=Y(-255..+255), Channel2=Cb(-128..+128), Channel3=Cr(-128..+128)
    /// </summary>
    public class ColorSettings
    {
        public ColorSpaceMode ActiveColorSpace { get; set; } = ColorSpaceMode.RGB;

        // Generic channel adjustments — semantics defined by ActiveColorSpace above
        public double Channel1 { get; set; } = 0;
        public double Channel2 { get; set; } = 0;
        public double Channel3 { get; set; } = 0;
        public double Channel4 { get; set; } = 0; // CMYK K channel only

        public int QuantizeCount { get; set; } = 0; // 0 = disabled; 2/4/8/16/64/256

        public bool IsDefault =>
            Channel1 == 0 && Channel2 == 0 && Channel3 == 0 && Channel4 == 0 && QuantizeCount == 0;

        public ColorSettings Clone() => new ColorSettings
        {
            ActiveColorSpace = ActiveColorSpace,
            Channel1         = Channel1,
            Channel2         = Channel2,
            Channel3         = Channel3,
            Channel4         = Channel4,
            QuantizeCount    = QuantizeCount
        };

        // Convenience accessors for RGB mode
        public double RedAdjust   { get => Channel1; set => Channel1 = value; }
        public double GreenAdjust { get => Channel2; set => Channel2 = value; }
        public double BlueAdjust  { get => Channel3; set => Channel3 = value; }

        // Convenience accessors for HSV mode
        public double HueAdjust        { get => Channel1; set => Channel1 = value; }
        public double SaturationAdjust { get => Channel2; set => Channel2 = value; }
        public double ValueAdjust      { get => Channel3; set => Channel3 = value; }

        // Convenience accessors for CMYK mode
        public double CyanAdjust    { get => Channel1; set => Channel1 = value; }
        public double MagentaAdjust { get => Channel2; set => Channel2 = value; }
        public double YellowAdjust  { get => Channel3; set => Channel3 = value; }
        public double BlackAdjust   { get => Channel4; set => Channel4 = value; }

        // Convenience accessors for LAB mode
        public double LAdjust { get => Channel1; set => Channel1 = value; }
        public double AAdjust { get => Channel2; set => Channel2 = value; }
        public double BAdjust { get => Channel3; set => Channel3 = value; }

        // Convenience accessors for YUV / YCbCr mode
        public double YAdjust  { get => Channel1; set => Channel1 = value; }
        public double UOrCbAdjust { get => Channel2; set => Channel2 = value; }
        public double VOrCrAdjust { get => Channel3; set => Channel3 = value; }
    }
}
