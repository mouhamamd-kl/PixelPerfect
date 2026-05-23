using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using PixelPerfect.UI.Atoms;
using PixelPerfect.UI.Molecules;

namespace PixelPerfect.UI.Organisms
{
    /// <summary>
    /// Top bar showing image dimensions, format, size, color mode,
    /// and a "Reset Changes" button on the right.
    /// </summary>
    public class TopBar : UserControl, IThemeable
    {
        private Label      _lblDimensions;
        private Label      _lblW;
        private Label      _widthValue;
        private Label      _lblH;
        private Label      _heightValue;
        private Label      _lblFormat;
        private Label      _formatValue;
        private Label      _lblSize;
        private Label      _sizeValue;
        private Label      _lblColorMode;
        private Label      _colorModeValue;
        private AppButton  _resetBtn;

        public event EventHandler ResetChangesClicked;

        public TopBar()
        {
            Height    = AppSpacing.TopBarHeight;
            BackColor = AppColors.PanelBg;
            Padding   = new Padding(AppSpacing.PadLg, 0, AppSpacing.PadLg, 0);

            BuildControls();
            Resize += (s, e) => LayoutControls();
            LayoutControls();
        }

        private void BuildControls()
        {
            _lblDimensions = MakeLabel("Dimensions", AppFonts.Label, AppColors.TextSecondary);
            _lblW          = MakeLabel("W",           AppFonts.Label, AppColors.TextSecondary);
            _widthValue    = MakeLabel("—",           AppFonts.Value, AppColors.TextPrimary);
            _lblH          = MakeLabel("H",           AppFonts.Label, AppColors.TextSecondary);
            _heightValue   = MakeLabel("—",           AppFonts.Value, AppColors.TextPrimary);
            _lblFormat     = MakeLabel("Format",      AppFonts.Label, AppColors.TextSecondary);
            _formatValue   = MakeLabel("—",           AppFonts.Value, AppColors.TextPrimary);
            _lblSize       = MakeLabel("Size",        AppFonts.Label, AppColors.TextSecondary);
            _sizeValue     = MakeLabel("—",           AppFonts.Value, AppColors.TextPrimary);
            _lblColorMode  = MakeLabel("Color mode",  AppFonts.Label, AppColors.TextSecondary);
            _colorModeValue = MakeLabel("—",          AppFonts.Value, AppColors.TextPrimary);

            _resetBtn = new AppButton
            {
                Text      = "Reset Changes",
                IsPrimary = false,
                Width     = 120,
                Height    = 30,
                Font      = AppFonts.Label
            };
            _resetBtn.Click += (s, e) => ResetChangesClicked?.Invoke(this, EventArgs.Empty);

            Controls.AddRange(new Control[]
            {
                _lblDimensions, _lblW, _widthValue, _lblH, _heightValue,
                _lblFormat, _formatValue, _lblSize, _sizeValue,
                _lblColorMode, _colorModeValue, _resetBtn
            });
        }

        private Label MakeLabel(string text, Font font, Color foreColor)
        {
            return new Label
            {
                Text      = text,
                Font      = font,
                ForeColor = foreColor,
                BackColor = Color.Transparent,
                AutoSize  = true
            };
        }

        private void LayoutControls()
        {
            int cy = Height / 2;
            int x  = AppSpacing.PadLg;
            int gap = AppSpacing.Gap6Xl;

            void PlaceGroup(Label lbl, Label val)
            {
                lbl.Location = new Point(x, cy - lbl.Height / 2 - 1);
                x += lbl.Width + AppSpacing.GapSm;
                val.Location = new Point(x, cy - val.Height / 2 - 1);
                x += val.Width + gap;
            }

            _lblDimensions.Location = new Point(x, cy - _lblDimensions.Height / 2 - 1);
            x += _lblDimensions.Width + AppSpacing.GapSm;
            PlaceGroup(_lblW, _widthValue);
            PlaceGroup(_lblH, _heightValue);
            PlaceGroup(_lblFormat, _formatValue);
            PlaceGroup(_lblSize, _sizeValue);
            PlaceGroup(_lblColorMode, _colorModeValue);

            // Reset button pinned to right
            _resetBtn.Location = new Point(Width - _resetBtn.Width - AppSpacing.PadLg,
                                           (Height - _resetBtn.Height) / 2);
        }

        public void UpdateImageInfo(int w, int h, string format, long bytes, string colorMode)
        {
            _widthValue.Text     = w.ToString();
            _heightValue.Text    = h.ToString();
            _formatValue.Text    = format;
            _sizeValue.Text      = FormatBytes(bytes);
            _colorModeValue.Text = colorMode;
            LayoutControls();
        }

        private static string FormatBytes(long b)
        {
            if (b < 1024)         return $"{b} B";
            if (b < 1024 * 1024)  return $"{b / 1024.0:F1} KB";
            return $"{b / (1024.0 * 1024):F1} MB";
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            // Bottom border
            using var pen = new Pen(AppColors.BorderPrimary, 1f);
            e.Graphics.DrawLine(pen, 0, Height - 1, Width, Height - 1);
        }

        public void ApplyTheme()
        {
            BackColor = AppColors.PanelBg;
            foreach (Control c in Controls)
            {
                if (c is Label lbl)
                {
                    bool isValue = lbl.Font == AppFonts.Value;
                    lbl.ForeColor = isValue ? AppColors.TextPrimary : AppColors.TextSecondary;
                    lbl.BackColor = Color.Transparent;
                }
                if (c is AppButton btn) btn.ApplyTheme();
            }
            Invalidate();
        }
    }
}
