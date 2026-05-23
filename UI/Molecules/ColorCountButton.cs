using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;
using PixelPerfect.UI.Atoms;

namespace PixelPerfect.UI.Molecules
{
    /// <summary>
    /// Capsule chip displaying a count value (2, 4, 8, 16, 64, 256).
    /// Matches the ColorSpaceBar chip style exactly:
    ///   selected  → solid orange fill, white text
    ///   unselected → outlined border, secondary text
    ///   hover      → orange border + text
    /// Width is auto-sized from the label; height is fixed at 36px.
    /// </summary>
    public class ColorCountButton : Control, IThemeable
    {
        private bool _isSelected;
        private bool _isHovered;
        private int  _countValue;

        public int CountValue
        {
            get => _countValue;
            set
            {
                _countValue = value;
                AutoSizeWidth();
                Invalidate();
            }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; Invalidate(); }
        }

        public event EventHandler Selected;

        public ColorCountButton()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint         |
                ControlStyles.UserPaint                    |
                ControlStyles.DoubleBuffer                 |
                ControlStyles.ResizeRedraw                 |
                ControlStyles.SupportsTransparentBackColor, true);

            Height    = 36;
            BackColor = Color.Transparent;
            Cursor    = Cursors.Hand;
        }

        // ── Sizing ────────────────────────────────────────────────────────────

        private void AutoSizeWidth()
        {
            using var scratch = new Bitmap(1, 1);
            using var g      = Graphics.FromImage(scratch);
            int textW = (int)Math.Ceiling(g.MeasureString(_countValue.ToString(), AppFonts.Label).Width);
            Width = textW + 32; // 16px padding each side
        }

        // ── Paint ─────────────────────────────────────────────────────────────

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode     = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            // 1px inset so the border isn't clipped at the edge
            var rect   = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
            float radius = Height / 2f;

            if (_isSelected)
            {
                using var bg = new SolidBrush(AppColors.Accent);
                g.FillRoundedRect(bg, rect, radius);
                using var txt = new SolidBrush(Color.White);
                DrawLabel(g, txt);
            }
            else
            {
                Color borderCol  = _isHovered ? AppColors.Accent        : AppColors.BorderPrimary;
                Color textCol    = _isHovered ? AppColors.Accent        : AppColors.TextSecondary;
                using var pen    = new Pen(borderCol, 1f);
                g.DrawRoundedRect(pen, rect, radius);
                using var txt    = new SolidBrush(textCol);
                DrawLabel(g, txt);
            }
        }

        private void DrawLabel(Graphics g, Brush brush)
        {
            var sf = new StringFormat
            {
                Alignment     = StringAlignment.Center,
                LineAlignment = StringAlignment.Center,
                Trimming      = StringTrimming.None
            };
            g.DrawString(_countValue.ToString(), AppFonts.Label, brush,
                new RectangleF(0, 0, Width, Height), sf);
        }

        // ── Mouse ─────────────────────────────────────────────────────────────

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); _isHovered = true;  Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _isHovered = false; Invalidate(); }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            Selected?.Invoke(this, EventArgs.Empty);
        }

        public void ApplyTheme() => Invalidate();
    }
}
