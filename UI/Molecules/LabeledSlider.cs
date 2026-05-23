using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using PixelPerfect.Helpers;
using PixelPerfect.UI.Atoms;

namespace PixelPerfect.UI.Molecules
{
    /// <summary>
    /// Two-row channel slider matching the design:
    ///
    ///   Row 1:  [eye icon]  [channel name]  ·····  [value box]
    ///   Row 2:  [────────────── slider track ──────────────────]
    ///
    /// The eye icon toggles channel visibility (active = colored, inactive = dimmed).
    /// The value box is a rounded-rect chip showing the integer value.
    /// </summary>
    public class LabeledSlider : UserControl, IThemeable
    {
        // ── Sub-controls ──────────────────────────────────────────────────────
        private readonly IconButton _eyeBtn;
        private readonly Label      _nameLabel;
        private readonly Label      _valueLabel;
        private readonly AppSlider  _slider;

        // ── Layout constants ──────────────────────────────────────────────────
        private const int EyeSize    = 24;
        private const int Row1H      = 28;
        private const int Row2H      = 24;
        private const int ValueW     = 40;
        private const int ValueH     = 24;
        private const int RowGap     = 4;

        // ── Properties ────────────────────────────────────────────────────────

        public string ChannelName
        {
            get => _nameLabel.Text;
            set => _nameLabel.Text = value;
        }

        public Color TrackColor
        {
            get => _slider.TrackColor;
            set
            {
                _slider.TrackColor  = value;
                _eyeBtn.IsActive    = true;
                _eyeBtn.SvgFileName = "Eye.svg";
                Invalidate();
            }
        }

        public double Minimum { get => _slider.Minimum; set => _slider.Minimum = value; }
        public double Maximum { get => _slider.Maximum; set => _slider.Maximum = value; }

        public double Value
        {
            get => _slider.Value;
            set { _slider.Value = value; UpdateValueLabel(); }
        }

        public event EventHandler ValueChanged;

        // ── Constructor ───────────────────────────────────────────────────────

        public LabeledSlider()
        {
            Height    = Row1H + RowGap + Row2H;
            BackColor = Color.Transparent;

            SetStyle(
                ControlStyles.AllPaintingInWmPaint         |
                ControlStyles.UserPaint                    |
                ControlStyles.DoubleBuffer                 |
                ControlStyles.SupportsTransparentBackColor, true);

            // Eye toggle button — no orange bg, icon color follows track color
            _eyeBtn = new IconButton
            {
                SvgFileName  = "Eye.svg",
                Width        = EyeSize,
                Height       = EyeSize,
                IsActive     = true,
                UseActiveBg  = false,
                BackColor    = Color.Transparent,
            };
            _eyeBtn.Click += (s, e) =>
            {
                _eyeBtn.IsActive    = !_eyeBtn.IsActive;
                _eyeBtn.SvgFileName = _eyeBtn.IsActive ? "Eye.svg" : "Eye Closed.svg";
                _slider.Enabled     = _eyeBtn.IsActive;
                _slider.Invalidate();
            };

            // Channel name label
            _nameLabel = new Label
            {
                AutoSize  = false,
                Height    = Row1H,
                TextAlign = ContentAlignment.MiddleLeft,
                Font      = AppFonts.Body,
                ForeColor = AppColors.TextPrimary,
                BackColor = Color.Transparent,
            };

            // Value chip label — painted manually in OnPaint, but positioned here
            _valueLabel = new Label
            {
                AutoSize  = false,
                Width     = ValueW,
                Height    = ValueH,
                TextAlign = ContentAlignment.MiddleCenter,
                Font      = AppFonts.Label,
                ForeColor = AppColors.TextSecondary,
                BackColor = Color.Transparent,
                Visible   = false,   // we paint the box ourselves in OnPaint
            };

            // Slider
            _slider = new AppSlider { Minimum = -255, Maximum = 255, Value = 0 };
            _slider.ValueChanged += (s, e) =>
            {
                UpdateValueLabel();
                ValueChanged?.Invoke(this, EventArgs.Empty);
            };

            Controls.Add(_eyeBtn);
            Controls.Add(_nameLabel);
            Controls.Add(_slider);

            UpdateValueLabel();
            Resize += (s, e) => LayoutControls();
            LayoutControls();
        }

        // ── Layout ────────────────────────────────────────────────────────────

        private void LayoutControls()
        {
            int w = Width;

            // Row 1: eye | name ····· value-box
            int eyeY  = (Row1H - EyeSize) / 2;
            _eyeBtn.Bounds    = new Rectangle(0, eyeY, EyeSize, EyeSize);

            int nameX = EyeSize + AppSpacing.GapXl;
            int nameW = w - nameX - ValueW - AppSpacing.GapXl;
            _nameLabel.Bounds = new Rectangle(nameX, 0, nameW, Row1H);

            // Row 2: slider full width
            int sliderY = Row1H + RowGap;
            _slider.Bounds = new Rectangle(0, sliderY, w, Row2H);

            Invalidate(); // repaint value box
        }

        // ── Paint ─────────────────────────────────────────────────────────────

        protected override void OnPaintBackground(PaintEventArgs e) => base.OnPaintBackground(e);

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode     = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            // Value box — rounded rect on the right of row 1
            int boxX = Width - ValueW;
            int boxY = (Row1H - ValueH) / 2;
            var boxRect = new RectangleF(boxX + 0.5f, boxY + 0.5f, ValueW - 1f, ValueH - 1f);

            using (var bg  = new SolidBrush(AppColors.SurfaceAlt))
                g.FillRoundedRect(bg, boxRect, AppSpacing.RadiusXl);

            string valText = ((int)Math.Round(_slider.Value)).ToString();
            using (var txt = new SolidBrush(AppColors.TextPrimary))
            {
                var sf = new StringFormat
                {
                    Alignment     = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center,
                };
                g.DrawString(valText, AppFonts.Label, txt,
                    new RectangleF(boxX, boxY, ValueW, ValueH), sf);
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private void UpdateValueLabel() => Invalidate();

        public void Reset() => _slider.Value = 0;

        public void ApplyTheme()
        {
            _nameLabel.ForeColor = AppColors.TextPrimary;
            _eyeBtn.ApplyTheme();
            _slider.ApplyTheme();
            Invalidate();
        }
    }
}
