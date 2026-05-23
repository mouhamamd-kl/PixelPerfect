using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using PixelPerfect.Helpers;
using PixelPerfect.UI.Atoms;
using PixelPerfect.UI.Molecules;

namespace PixelPerfect.UI.Dialogs
{
    /// <summary>
    /// Borderless popup showing detailed color information for a picked pixel.
    /// Displays: color swatch, hex, RGB, CMYK, LAB, HSV values.
    /// "Copy Values" copies the hex to clipboard.
    /// Auto-dismisses on Escape or when focus is lost.
    /// </summary>
    public class ColorPickerPopup : Form
    {
        private Color _pickedColor;

        private ColorSwatch _swatch;
        private Label _hexLabel;
        private Label _rgbLabel;
        private Label _cmykLabel;
        private Label _labLabel;
        private Label _hsvLabel;
        private AppButton _copyBtn;

        public ColorPickerPopup()
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition   = FormStartPosition.Manual;
            Size            = new Size(230, 240);
            BackColor       = AppColors.PanelBg;
            ShowInTaskbar   = false;
            TopMost         = true;

            KeyPreview = true;
            KeyDown   += (s, e) => { if (e.KeyCode == Keys.Escape) Close(); };
            Deactivate += (s, e) => Close();

            BuildControls();
        }

        public void SetColor(Color c)
        {
            _pickedColor = c;
            _swatch.SwatchColor = c;

            _hexLabel.Text  = $"Hex {ColorMath.ToHex(c)}";

            _rgbLabel.Text  = $"RGB   {c.R}  {c.G}  {c.B}";

            var (cm, mv, yv, kv) = ColorMath.ToCmyk(c);
            _cmykLabel.Text = $"CMYK  {cm:F0}  {mv:F0}  {yv:F0}  {kv:F0}";

            var (l, a, b) = ColorMath.ToLab(c);
            _labLabel.Text  = $"LAB   {l:F1}  {a:F1}  {b:F1}";

            var (h, s, v) = ColorMath.ToHsv(c);
            _hsvLabel.Text  = $"HSV   {h:F0}°  {s:F0}%  {v:F0}%";
        }

        private void BuildControls()
        {
            int pad = AppSpacing.PadLg;
            int y   = pad;

            _swatch = new ColorSwatch
            {
                SwatchColor  = Color.Gray,
                ShowBorder   = true,
                CornerRadius = AppSpacing.RadiusMd,
                Bounds       = new Rectangle(pad, y, 24, 24)
            };

            _hexLabel  = MakeMonoLabel(pad + 32, y, Width - pad * 2 - 32);
            y += 28;

            _rgbLabel  = MakeMonoLabel(pad, y, Width - pad * 2); y += 20;
            _cmykLabel = MakeMonoLabel(pad, y, Width - pad * 2); y += 20;
            _labLabel  = MakeMonoLabel(pad, y, Width - pad * 2); y += 20;
            _hsvLabel  = MakeMonoLabel(pad, y, Width - pad * 2); y += 24;

            _copyBtn = new AppButton
            {
                Text      = "Copy Values",
                IsPrimary = false,
                Bounds    = new Rectangle(pad, y, Width - pad * 2, 30),
                Font      = AppFonts.Label
            };
            _copyBtn.Click += (s, e) => Clipboard.SetText(ColorMath.ToHex(_pickedColor));

            Controls.AddRange(new Control[] { _swatch, _hexLabel, _rgbLabel, _cmykLabel, _labLabel, _hsvLabel, _copyBtn });
        }

        private Label MakeMonoLabel(int x, int y, int w)
        {
            return new Label
            {
                Font      = AppFonts.Mono,
                ForeColor = AppColors.TextPrimary,
                BackColor = Color.Transparent,
                AutoSize  = false,
                Location  = new Point(x, y),
                Width     = w,
                Height    = 18
            };
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using var pen = new Pen(AppColors.BorderPrimary, 1f);
            g.DrawRoundedRect(pen, new RectangleF(0, 0, Width - 1, Height - 1), AppSpacing.Radius4Xl);

            // Drop shadow effect via border
            using var shadowPen = new Pen(Color.FromArgb(20, 0, 0, 0), 2f);
            g.DrawRoundedRect(shadowPen, new RectangleF(1, 1, Width - 2, Height - 2), AppSpacing.Radius4Xl);
        }
    }
}
