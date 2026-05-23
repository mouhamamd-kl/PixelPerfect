using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using PixelPerfect.Helpers;

namespace PixelPerfect.UI.Atoms
{
    /// <summary>
    /// A square button that renders an SVG icon.
    /// Supports normal / hover / active (orange) states.
    /// Set IsToggle = true for sticky on/off behavior.
    /// </summary>
    public class IconButton : Control, IThemeable
    {
        private string _svgFileName;
        private bool   _isActive;
        private bool   _isHovered;
        private bool   _isToggle;

        public string SvgFileName
        {
            get => _svgFileName;
            set { _svgFileName = value; Invalidate(); }
        }

        public bool IsActive
        {
            get => _isActive;
            set { _isActive = value; Invalidate(); }
        }

        public bool IsToggle
        {
            get => _isToggle;
            set => _isToggle = value;
        }

        public int   IconSize       { get; set; } = AppSpacing.IconSize;
        public bool  UseActiveBg    { get; set; } = true;
        public Color ActiveIconColor { get; set; } = Color.Empty;

        public IconButton()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint         |
                ControlStyles.UserPaint                    |
                ControlStyles.DoubleBuffer                 |
                ControlStyles.ResizeRedraw                 |
                ControlStyles.SupportsTransparentBackColor, true);

            Size      = new Size(AppSpacing.LeftToolbarWidth, AppSpacing.LeftToolbarWidth);
            BackColor = Color.Transparent;
            Cursor    = Cursors.Hand;

            AppColors.ThemeChanged += (s, e) => { SvgIconHelper.ClearCache(); Invalidate(); };
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Active: solid orange rounded-square bg (only when UseActiveBg); Hover: subtle tinted bg
            if (_isHovered || (_isActive && UseActiveBg))
            {
                Color bgColor = (_isActive && UseActiveBg)
                    ? AppColors.Accent
                    : Color.FromArgb(20, AppColors.TextPrimary);

                int pad = 4;
                var rect = new RectangleF(pad, pad, Width - pad * 2, Height - pad * 2);
                using var bgBrush = new SolidBrush(bgColor);
                g.FillRoundedRect(bgBrush, rect, AppSpacing.Radius4Xl);
            }

            // Icon color:
            //   UseActiveBg=true  → white on active, hover color on hover, normal otherwise
            //   UseActiveBg=false → ActiveIconColor (track color) on active, hover on hover, normal otherwise
            if (!string.IsNullOrEmpty(_svgFileName))
            {
                Color iconColor = _isActive && UseActiveBg  ? Color.White
                    : _isActive && !UseActiveBg             ? (ActiveIconColor != Color.Empty ? ActiveIconColor : AppColors.IconNormal)
                    : _isHovered                            ? AppColors.IconHover
                    : AppColors.IconNormal;

                Bitmap icon = SvgIconHelper.Load(_svgFileName, IconSize, iconColor);
                if (icon != null)
                {
                    int x = (Width  - IconSize) / 2;
                    int y = (Height - IconSize) / 2;
                    g.DrawImage(icon, x, y, IconSize, IconSize);
                }
            }
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            _isHovered = true;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _isHovered = false;
            Invalidate();
        }

        protected override void OnClick(EventArgs e)
        {
            if (_isToggle) IsActive = !_isActive;
            base.OnClick(e);
        }

        public void ApplyTheme()
        {
            SvgIconHelper.ClearCache();
            Invalidate();
        }
    }
}
