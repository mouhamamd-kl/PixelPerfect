using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace PixelPerfect.UI.Atoms
{
    /// <summary>
    /// Custom slider with a colored rounded track and a circular thumb.
    /// Supports double-precision values for non-RGB color spaces.
    /// </summary>
    public class AppSlider : Control, IThemeable
    {
        private double _minimum = 0;
        private double _maximum = 255;
        private double _value   = 0;
        private bool   _dragging;
        private Color  _trackColor = Color.FromArgb(0xFA, 0x7B, 0x3D);

        private const int TrackHeight = 3;
        private const int ThumbW      = 24;
        private const int ThumbH      = 12;
        private const int SliderPad   = ThumbW / 2 + 2;

        public double Minimum
        {
            get => _minimum;
            set { _minimum = value; Invalidate(); }
        }

        public double Maximum
        {
            get => _maximum;
            set { _maximum = value; Invalidate(); }
        }

        public double Value
        {
            get => _value;
            set
            {
                double clamped = Math.Max(_minimum, Math.Min(_maximum, value));
                if (Math.Abs(clamped - _value) < 0.001) return;
                _value = clamped;
                ValueChanged?.Invoke(this, EventArgs.Empty);
                Invalidate();
            }
        }

        public Color TrackColor
        {
            get => _trackColor;
            set { _trackColor = value; Invalidate(); }
        }

        public event EventHandler ValueChanged;

        public AppSlider()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint         |
                ControlStyles.UserPaint                    |
                ControlStyles.DoubleBuffer                 |
                ControlStyles.ResizeRedraw                 |
                ControlStyles.SupportsTransparentBackColor, true);

            Height    = ThumbH + 6; // room for shadow + track
            BackColor = Color.Transparent;
            Cursor    = Cursors.Hand;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int trackY = Height / 2;
            float thumbX = GetThumbX();

            // Background track (full width)
            using (var bgBrush = new SolidBrush(AppColors.BorderPrimary))
            {
                var trackRect = new RectangleF(SliderPad, trackY - TrackHeight / 2f, Width - SliderPad * 2, TrackHeight);
                g.FillRoundedRect(bgBrush, trackRect, TrackHeight / 2f);
            }

            // Filled track (up to thumb)
            if (thumbX > SliderPad)
            {
                using var fillBrush = new SolidBrush(_trackColor);
                var fillRect = new RectangleF(SliderPad, trackY - TrackHeight / 2f, thumbX - SliderPad, TrackHeight);
                g.FillRoundedRect(fillBrush, fillRect, TrackHeight / 2f);
            }

            // Thumb: 24×12 rounded-rect pill, white fill, shadow underneath
            float tx = thumbX - ThumbW / 2f;
            float ty = trackY - ThumbH / 2f;
            var thumbRect   = new RectangleF(tx, ty, ThumbW, ThumbH);
            var shadowRect  = new RectangleF(tx, ty + 1f, ThumbW, ThumbH); // Y+1 shadow offset
            float thumbCorner = ThumbH / 2f; // fully rounded ends

            // Shadow (10% black, Y=1, blur≈2, spread=-1)
            using (var shadowBrush = new SolidBrush(Color.FromArgb(26, 0, 0, 0)))
                g.FillRoundedRect(shadowBrush,
                    new RectangleF(shadowRect.X + 1, shadowRect.Y, shadowRect.Width - 2, shadowRect.Height - 1),
                    thumbCorner - 1);

            // White pill
            using (var thumbBrush = new SolidBrush(Color.White))
                g.FillRoundedRect(thumbBrush, thumbRect, thumbCorner);

            // Subtle border
            using (var thumbPen = new Pen(Color.FromArgb(30, 0, 0, 0), 1f))
                g.DrawRoundedRect(thumbPen, new RectangleF(tx + 0.5f, ty + 0.5f, ThumbW - 1, ThumbH - 1), thumbCorner);
        }

        private float GetThumbX()
        {
            if (_maximum <= _minimum) return SliderPad;
            double ratio = (_value - _minimum) / (_maximum - _minimum);
            return (float)(SliderPad + ratio * (Width - SliderPad * 2));
        }

        private double XToValue(int x)
        {
            double ratio = (double)(x - SliderPad) / (Width - SliderPad * 2);
            ratio = Math.Max(0, Math.Min(1, ratio));
            return _minimum + ratio * (_maximum - _minimum);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left)
            {
                _dragging = true;
                Value = XToValue(e.X);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_dragging) Value = XToValue(e.X);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            _dragging = false;
        }

        public void ApplyTheme() => Invalidate();
    }

    // ── Graphics extension for rounded rectangles ─────────────────────────────

    internal static class GraphicsExtensions
    {
        public static void FillRoundedRect(this Graphics g, Brush brush, RectangleF rect, float radius)
        {
            if (rect.Width <= 0 || rect.Height <= 0) return;
            float d = radius * 2;
            var path = new GraphicsPath();
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            g.FillPath(brush, path);
        }

        public static void DrawRoundedRect(this Graphics g, Pen pen, RectangleF rect, float radius)
        {
            if (rect.Width <= 0 || rect.Height <= 0) return;
            float d = radius * 2;
            var path = new GraphicsPath();
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            g.DrawPath(pen, path);
        }
    }
}
