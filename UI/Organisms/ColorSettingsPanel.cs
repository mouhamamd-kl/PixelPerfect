using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using PixelPerfect.Models;
using PixelPerfect.UI.Atoms;
using PixelPerfect.UI.Molecules;

namespace PixelPerfect.UI.Organisms
{
    /// <summary>
    /// Right-side panel: color-space selector, per-channel sliders,
    /// color count buttons, and dominant-color swatches.
    /// </summary>
    public class ColorSettingsPanel : UserControl, IThemeable
    {
        // ── Header ────────────────────────────────────────────────────────────
        private Label _headerLabel;
        private Label _clearLabel;

        // ── Channel sliders ───────────────────────────────────────────────────
        private LabeledSlider _ch1Slider;
        private LabeledSlider _ch2Slider;
        private LabeledSlider _ch3Slider;
        private LabeledSlider _ch4Slider; // CMYK K channel only

        // ── Color count ───────────────────────────────────────────────────────
        private Label            _countHeader;
        private Label            _countValue;
        private FlowLayoutPanel  _countButtonsRow;
        private ColorCountButton[] _countButtons;
        private int              _selectedCount = 0;

        // ── Dominant color swatches ────────────────────────────────────────────
        private FlowLayoutPanel _swatchesPanel;
        private List<ColorSwatch> _swatches = new List<ColorSwatch>();

        private ColorSpaceMode _activeMode = ColorSpaceMode.RGB;

        public event EventHandler SettingsChanged;

        public ColorSettingsPanel()
        {
            Width     = AppSpacing.RightPanelWidth;
            BackColor = AppColors.PanelBg;
            Padding   = new Padding(AppSpacing.PadLg);
            AutoScroll = true;

            BuildControls();
            SetColorSpace(ColorSpaceMode.RGB);
            Resize += (s, e) => LayoutControls();
        }

        // ── Public API ───────────────────────────────────────────────────────

        public ColorSettings GetSettings()
        {
            var s = new ColorSettings
            {
                ActiveColorSpace = _activeMode,
                Channel1         = _ch1Slider.Value,
                Channel2         = _ch2Slider.Value,
                Channel3         = _ch3Slider.Value,
                Channel4         = _ch4Slider.Value,
                QuantizeCount    = _selectedCount
            };
            return s;
        }

        public void SetColorSpaceMode(ColorSpaceMode mode)
        {
            SetColorSpace(mode);
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }

        public void ResetToDefaults()
        {
            ResetSliders();
            foreach (var btn in _countButtons) btn.IsSelected = false;
            _selectedCount = 0;
            _countValue.Text = "";
            _swatchesPanel.Controls.Clear();
            _swatches.Clear();
        }

        public void SelectCount(int val)
        {
            _selectedCount = val;
            foreach (var b in _countButtons) b.IsSelected = b.CountValue == val;
            _countValue.Text = val > 0 ? "× clear" : "";
        }

        public void SetPickedColor(Color c)
        {
            if (_swatches.Count == 0) AddSwatch();
            _swatches[0].SwatchColor = c;
        }

        public void SetDominantColors(IReadOnlyList<Color> colors)
        {
            // Rebuild swatches
            _swatchesPanel.Controls.Clear();
            _swatches.Clear();
            foreach (var c in colors)
            {
                var sw = new ColorSwatch { SwatchColor = c, Width = 48, Height = 28, Margin = new Padding(0, 0, 6, 6) };
                _swatches.Add(sw);
                _swatchesPanel.Controls.Add(sw);
            }
        }

        // ── Build controls ───────────────────────────────────────────────────

        private void BuildControls()
        {
            // Header
            _headerLabel = new Label
            {
                Text      = "Colors Settings",
                Font      = AppFonts.Header,
                ForeColor = AppColors.TextPrimary,
                BackColor = Color.Transparent,
                AutoSize  = true
            };
            _clearLabel = new Label
            {
                Text      = "× clear",
                Font      = AppFonts.Label,
                ForeColor = AppColors.Accent,
                BackColor = Color.Transparent,
                AutoSize  = true,
                Cursor    = Cursors.Hand,
                Visible   = false,
            };
            _clearLabel.Click += (s, e) =>
            {
                ResetSliders();
                _clearLabel.Visible = false;
                SettingsChanged?.Invoke(this, EventArgs.Empty);
            };

            // Channel sliders
            _ch1Slider = MakeSlider(); _ch1Slider.TrackColor = AppColors.ChannelRed;
            _ch2Slider = MakeSlider(); _ch2Slider.TrackColor = AppColors.ChannelGreen;
            _ch3Slider = MakeSlider(); _ch3Slider.TrackColor = AppColors.ChannelBlue;
            _ch4Slider = MakeSlider(); _ch4Slider.TrackColor = AppColors.TextSecondary; // CMYK K

            // Color count section
            _countHeader = new Label { Text = "Color Count", Font = AppFonts.Label, ForeColor = AppColors.TextPrimary, BackColor = Color.Transparent, AutoSize = true };
            _countValue  = new Label { Text = "",    Font = AppFonts.Label, ForeColor = AppColors.Accent, BackColor = Color.Transparent, AutoSize = true, Cursor = Cursors.Hand };
            _countValue.Click += (s, e) => OnCountButtonClicked(_selectedCount);

            int[] countValues = { 2, 4, 8, 16, 64, 256 };
            _countButtons = new ColorCountButton[countValues.Length];
            _countButtonsRow = new FlowLayoutPanel
            {
                BackColor     = Color.Transparent,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents  = true,
                AutoSize      = true,
                AutoSizeMode  = AutoSizeMode.GrowAndShrink,
                Padding       = new Padding(0),
                Margin        = new Padding(0),
            };
            for (int i = 0; i < countValues.Length; i++)
            {
                var btn = new ColorCountButton
                {
                    CountValue = countValues[i],
                    Margin     = new Padding(0, 4, 8, 4), // 8px right, 4px top/bottom between rows
                };
                int val = countValues[i];
                btn.Selected += (s, e) => OnCountButtonClicked(val);
                _countButtons[i] = btn;
                _countButtonsRow.Controls.Add(btn);
            }

            // Swatches
            _swatchesPanel = new FlowLayoutPanel
            {
                BackColor     = Color.Transparent,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents  = true,
                AutoSize      = true,
                AutoSizeMode  = AutoSizeMode.GrowAndShrink,
                Padding       = new Padding(0),
                Margin        = new Padding(0),
            };
            AddSwatch(); // default picked-color swatch

            Controls.AddRange(new Control[]
            {
                _headerLabel, _clearLabel,
                _ch1Slider, _ch2Slider, _ch3Slider, _ch4Slider,
                _countHeader, _countValue, _countButtonsRow,
                _swatchesPanel
            });
        }

        private LabeledSlider MakeSlider()
        {
            var s = new LabeledSlider { Width = Width - Padding.Left - Padding.Right };
            s.ValueChanged += (_, __) =>
            {
                _clearLabel.Visible = _ch1Slider.Value != 0 || _ch2Slider.Value != 0
                                   || _ch3Slider.Value != 0 || _ch4Slider.Value != 0;
                SettingsChanged?.Invoke(this, EventArgs.Empty);
            };
            return s;
        }

        private void AddSwatch()
        {
            var sw = new ColorSwatch { SwatchColor = Color.Transparent, Width = 48, Height = 28, Margin = new Padding(0, 0, 6, 6) };
            _swatches.Add(sw);
            _swatchesPanel.Controls.Add(sw);
        }

        // ── Color space switching ────────────────────────────────────────────

        private void SetColorSpace(ColorSpaceMode mode)
        {
            _activeMode = mode;

            // Update slider labels + ranges
            switch (mode)
            {
                case ColorSpaceMode.RGB:
                    ConfigSlider(_ch1Slider, "Red",   -255, 255, AppColors.ChannelRed);
                    ConfigSlider(_ch2Slider, "Green", -255, 255, AppColors.ChannelGreen);
                    ConfigSlider(_ch3Slider, "Blue",  -255, 255, AppColors.ChannelBlue);
                    _ch4Slider.Visible = false;
                    break;
                case ColorSpaceMode.HSV:
                    ConfigSlider(_ch1Slider, "Hue",        -180, 180, AppColors.ChannelYellow);
                    ConfigSlider(_ch2Slider, "Saturation", -100, 100, AppColors.ChannelGreen);
                    ConfigSlider(_ch3Slider, "Value",      -100, 100, AppColors.TextSecondary);
                    _ch4Slider.Visible = false;
                    break;
                case ColorSpaceMode.CMYK:
                    ConfigSlider(_ch1Slider, "Cyan",    -100, 100, AppColors.ChannelCyan);
                    ConfigSlider(_ch2Slider, "Magenta", -100, 100, Color.Magenta);
                    ConfigSlider(_ch3Slider, "Yellow",  -100, 100, AppColors.ChannelYellow);
                    ConfigSlider(_ch4Slider, "Key (K)", -100, 100, AppColors.TextSecondary);
                    _ch4Slider.Visible = true;
                    break;
                case ColorSpaceMode.LAB:
                    ConfigSlider(_ch1Slider, "L",  -100, 100, AppColors.TextSecondary);
                    ConfigSlider(_ch2Slider, "a",  -128, 128, AppColors.ChannelRed);
                    ConfigSlider(_ch3Slider, "b",  -128, 128, AppColors.ChannelBlue);
                    _ch4Slider.Visible = false;
                    break;
                case ColorSpaceMode.YUV:
                    ConfigSlider(_ch1Slider, "Y",  -255, 255, AppColors.TextSecondary);
                    ConfigSlider(_ch2Slider, "U",  -112, 112, AppColors.ChannelBlue);
                    ConfigSlider(_ch3Slider, "V",  -157, 157, AppColors.ChannelRed);
                    _ch4Slider.Visible = false;
                    break;
                case ColorSpaceMode.YCbCr:
                    ConfigSlider(_ch1Slider, "Y",  -255, 255, AppColors.TextSecondary);
                    ConfigSlider(_ch2Slider, "Cb", -128, 128, AppColors.ChannelBlue);
                    ConfigSlider(_ch3Slider, "Cr", -128, 128, AppColors.ChannelRed);
                    _ch4Slider.Visible = false;
                    break;
            }

            ResetSliders();
            LayoutControls();
            Invalidate();
        }

        private void ResetSliders()
        {
            _ch1Slider.Reset();
            _ch2Slider.Reset();
            _ch3Slider.Reset();
            _ch4Slider.Reset();
            if (_clearLabel != null) _clearLabel.Visible = false;
        }

        private void ConfigSlider(LabeledSlider s, string name, double min, double max, Color trackColor)
        {
            s.ChannelName = name;
            s.Minimum     = min;
            s.Maximum     = max;
            s.TrackColor  = trackColor;
            s.Visible     = true;
            s.Reset();
        }

        private void OnCountButtonClicked(int val)
        {
            if (_selectedCount == val || val == 0)
            {
                _selectedCount = 0;
                foreach (var b in _countButtons) b.IsSelected = false;
                _countValue.Text = "";
                _swatchesPanel.Controls.Clear();
                _swatches.Clear();
            }
            else SelectCount(val);
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }

        // ── Layout ───────────────────────────────────────────────────────────

        private void LayoutControls()
        {
            int x = Padding.Left;
            int w = Width - Padding.Left - Padding.Right;
            int y = Padding.Top;

            Place(_headerLabel, x, ref y, w, AppSpacing.Gap11X);
            // "× clear" floats right on the same row as the header
            _clearLabel.Location = new Point(x + w - _clearLabel.Width, y - AppSpacing.Gap11X + (_headerLabel.Height - _clearLabel.Height) / 2);

            y += AppSpacing.Gap6Xl;

            Place(_ch1Slider, x, ref y, w, 56);
            Place(_ch2Slider, x, ref y, w, 56);
            Place(_ch3Slider, x, ref y, w, 56);
            if (_ch4Slider.Visible)
                Place(_ch4Slider, x, ref y, w, 56);

            y += AppSpacing.Gap6Xl;

            // Count header row
            _countHeader.Location = new Point(x, y);
            _countValue.Location  = new Point(x + w - _countValue.Width, y);
            y += _countHeader.Height + AppSpacing.GapXl;

            _countButtonsRow.MaximumSize = new Size(w, 0); // constrain width, height is auto
            _countButtonsRow.Location    = new Point(x, y);
            _countButtonsRow.Width       = w;
            y += _countButtonsRow.Height + AppSpacing.GapXl;

            y += AppSpacing.GapXl;
            _swatchesPanel.MaximumSize = new Size(w, 0);
            _swatchesPanel.Location    = new Point(x, y);
            _swatchesPanel.Width       = w;
            y += _swatchesPanel.Height + AppSpacing.GapXl;
        }

        private static void Place(Control c, int x, ref int y, int w, int h)
        {
            c.Bounds = new Rectangle(x, y, w, h);
            y += h + AppSpacing.GapXl;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            // Left border separator
            using var pen = new Pen(AppColors.BorderPrimary, 1f);
            e.Graphics.DrawLine(pen, 0, 0, 0, Height);
        }

        public void ApplyTheme()
        {
            BackColor = AppColors.PanelBg;
            _headerLabel.ForeColor = AppColors.TextPrimary;
            _countHeader.ForeColor = AppColors.TextPrimary;
            _countValue.ForeColor  = AppColors.TextSecondary;
            foreach (Control c in Controls)
                if (c is IThemeable t) t.ApplyTheme();
            Invalidate();
        }
    }
}
