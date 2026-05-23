using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using PixelPerfect.UI.Atoms;

namespace PixelPerfect.UI.Molecules
{
    /// <summary>
    /// A rounded-rectangle color swatch. Used to display picked colors and
    /// quantized palette results.
    /// </summary>
    public class ColorSwatch : Control, IThemeable
    {
        private Color _swatchColor = Color.Gray;

        public Color SwatchColor
        {
            get => _swatchColor;
            set { _swatchColor = value; Invalidate(); }
        }

        public bool ShowBorder { get; set; } = true;
        public int  CornerRadius { get; set; } = AppSpacing.RadiusSm;

        public ColorSwatch()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.UserPaint |
                ControlStyles.DoubleBuffer, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var rect = new RectangleF(1, 1, Width - 2, Height - 2);

            using (var brush = new SolidBrush(_swatchColor))
                g.FillRoundedRect(brush, rect, CornerRadius);

            if (ShowBorder)
            {
                using var pen = new Pen(AppColors.BorderPrimary, 1f);
                g.DrawRoundedRect(pen, rect, CornerRadius);
            }
        }

        public void ApplyTheme() => Invalidate();
    }
}
