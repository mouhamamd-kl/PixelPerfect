using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using PixelPerfect.Models;
using PixelPerfect.UI.Atoms;

namespace PixelPerfect.UI.Organisms
{
    /// <summary>
    /// Secondary floating pill that appears above BottomToolbar when the
    /// color-settings button is active. Shows color-space mode chips:
    ///   [ RGB ]  [ CMYK ]  [ HSV ]  [ YUV ]  [ LAB ]  [ YCbCr ]
    /// Active chip = solid orange pill; others = outlined chips.
    /// Lives as a child of CanvasPanel for the same transparency reason as BottomToolbar.
    /// </summary>
    public class ColorSpaceBar : UserControl, IThemeable
    {
        // ── Geometry ──────────────────────────────────────────────────────────
        private const int ChipH      = 36;
        private const int ChipPadX   = 16;   // text padding inside each chip
        private const int ChipGap    = 8;
        private const int PillPadX   = 16;
        private const int PillPadY   = 8;
        private const int PillBottom = 8;    // gap between pill bottom and control bottom

        private RectangleF _pillRect;
        private readonly List<(ColorSpaceMode Mode, RectangleF Rect)> _chips = new();

        // ── State ─────────────────────────────────────────────────────────────
        public ColorSpaceMode ActiveMode { get; private set; } = ColorSpaceMode.RGB;

        // ── Events ────────────────────────────────────────────────────────────
        public event EventHandler<ColorSpaceMode> ModeChanged;

        public ColorSpaceBar()
        {
            Height    = ChipH + PillPadY * 2 + PillBottom;
            BackColor = Color.Transparent;

            SetStyle(
                ControlStyles.AllPaintingInWmPaint         |
                ControlStyles.UserPaint                    |
                ControlStyles.DoubleBuffer                 |
                ControlStyles.SupportsTransparentBackColor |
                ControlStyles.ResizeRedraw, true);

            Cursor = Cursors.Hand;
            Resize += (s, e) => RecalcChips();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            RecalcChips();
        }

        // ── Layout ────────────────────────────────────────────────────────────

        private void RecalcChips()
        {
            _chips.Clear();

            // Do NOT use `using var` on AppFonts — they are shared static instances.
            var font = AppFonts.Label;

            var modes = (ColorSpaceMode[])Enum.GetValues(typeof(ColorSpaceMode));
            var widths = new int[modes.Length];
            int totalChipW = 0;

            using (var scratch = new Bitmap(1, 1))
            using (var g = Graphics.FromImage(scratch))
            {
                for (int i = 0; i < modes.Length; i++)
                {
                    int textW = (int)Math.Ceiling(g.MeasureString(modes[i].ToString(), font).Width);
                    widths[i]   = textW + ChipPadX * 2;
                    totalChipW += widths[i];
                }
            }
            totalChipW += ChipGap * (modes.Length - 1);

            int pillW = totalChipW + PillPadX * 2;
            int pillH = ChipH + PillPadY * 2;
            int pillX = (Width - pillW) / 2;
            int pillY = Height - pillH - PillBottom;
            _pillRect = new RectangleF(pillX, pillY, pillW, pillH);

            int x = pillX + PillPadX;
            int y = pillY + PillPadY;
            for (int i = 0; i < modes.Length; i++)
            {
                _chips.Add((modes[i], new RectangleF(x, y, widths[i], ChipH)));
                x += widths[i] + ChipGap;
            }

            UpdateWindowRegion();
            Invalidate();
        }

        private void UpdateWindowRegion()
        {
            int capsuleRadius = (int)(_pillRect.Height / 2f);
            using var path = CreateRoundedPath(_pillRect, capsuleRadius);
            Region oldRegion = Region;
            Region = new Region(path);
            oldRegion?.Dispose();
        }

        private static GraphicsPath CreateRoundedPath(RectangleF rect, float radius)
        {
            var path = new GraphicsPath();
            float r = Math.Max(0f, Math.Min(radius, Math.Min(rect.Width, rect.Height) / 2f));
            if (r <= 0f)
            {
                path.AddRectangle(rect);
                path.CloseFigure();
                return path;
            }

            float d = r * 2f;
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        // ── Paint ─────────────────────────────────────────────────────────────

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            if (Parent == null)
            {
                base.OnPaintBackground(e);
                return;
            }

            GraphicsState state = e.Graphics.Save();
            try
            {
                e.Graphics.TranslateTransform(-Left, -Top);
                var parentClip = new Rectangle(Left, Top, Width, Height);
                var pe = new PaintEventArgs(e.Graphics, parentClip);
                InvokePaintBackground(Parent, pe);
                InvokePaint(Parent, pe);
            }
            finally
            {
                e.Graphics.Restore(state);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (_chips.Count == 0) return;

            var g = e.Graphics;
            g.SmoothingMode     = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            int capsuleRadius = (int)(_pillRect.Height / 2);

            // Outer pill background + border
            int fillAlpha = AppColors.IsDarkTheme ? 190 : 220;
            using (var bg = new SolidBrush(Color.FromArgb(fillAlpha, AppColors.PanelBg)))
                g.FillRoundedRect(bg, _pillRect, capsuleRadius);
            using (var pen = new Pen(AppColors.BorderPrimary, 1f))
                g.DrawRoundedRect(pen, _pillRect, capsuleRadius);

            var font = AppFonts.Label;  // shared static — do NOT dispose
            int chipRadius = ChipH / 2;

            foreach (var (mode, rect) in _chips)
            {
                bool active = mode == ActiveMode;

                if (active)
                {
                    // Solid orange filled pill
                    using var fill = new SolidBrush(AppColors.Accent);
                    g.FillRoundedRect(fill, rect, chipRadius);
                    using var txt = new SolidBrush(Color.White);
                    g.DrawString(mode.ToString(), font, txt, rect, CenterFormat());
                }
                else
                {
                    // Outlined chip with border
                    using var border = new Pen(AppColors.BorderPrimary, 1f);
                    g.DrawRoundedRect(border, rect, chipRadius);
                    using var txt = new SolidBrush(AppColors.TextPrimary);
                    g.DrawString(mode.ToString(), font, txt, rect, CenterFormat());
                }
            }
        }

        private static StringFormat CenterFormat() => new StringFormat
        {
            Alignment     = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
            Trimming      = StringTrimming.None
        };

        // ── Mouse ─────────────────────────────────────────────────────────────

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            foreach (var (mode, rect) in _chips)
            {
                if (rect.Contains(e.Location))
                {
                    ActiveMode = mode;
                    Invalidate();
                    ModeChanged?.Invoke(this, mode);
                    return;
                }
            }
        }

        // ── Theme ─────────────────────────────────────────────────────────────

        public void ApplyTheme() => Invalidate();
    }
}
