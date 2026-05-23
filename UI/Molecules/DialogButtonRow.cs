using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using PixelPerfect.UI.Atoms;

namespace PixelPerfect.UI.Molecules
{
    /// <summary>
    /// A row with a secondary (ghost) button and a primary (filled orange) button.
    /// Used at the bottom of dialogs.
    /// </summary>
    public class DialogButtonRow : UserControl, IThemeable
    {
        private AppButton _primaryBtn;
        private AppButton _secondaryBtn;

        public string PrimaryText
        {
            get => _primaryBtn.Text;
            set => _primaryBtn.Text = value;
        }

        public string SecondaryText
        {
            get => _secondaryBtn.Text;
            set => _secondaryBtn.Text = value;
        }

        public event EventHandler PrimaryClicked;
        public event EventHandler SecondaryClicked;

        public DialogButtonRow()
        {
            Height    = 44;
            BackColor = Color.Transparent;

            _secondaryBtn = new AppButton
            {
                Text      = "Cancel",
                IsPrimary = false
            };
            _secondaryBtn.Click += (s, e) => SecondaryClicked?.Invoke(this, e);

            _primaryBtn = new AppButton
            {
                Text      = "OK",
                IsPrimary = true
            };
            _primaryBtn.Click += (s, e) => PrimaryClicked?.Invoke(this, e);

            Controls.Add(_secondaryBtn);
            Controls.Add(_primaryBtn);

            Resize += (s, e) => LayoutControls();
            LayoutControls();
        }

        private void LayoutControls()
        {
            int btnW = 100, btnH = 36;
            int y = (Height - btnH) / 2;
            _secondaryBtn.Bounds = new Rectangle(Width - btnW * 2 - AppSpacing.GapXl, y, btnW, btnH);
            _primaryBtn.Bounds   = new Rectangle(Width - btnW - AppSpacing.GapSm, y, btnW, btnH);
        }

        public void ApplyTheme()
        {
            _primaryBtn.ApplyTheme();
            _secondaryBtn.ApplyTheme();
        }
    }

    // ── Reusable styled button ────────────────────────────────────────────────

    public class AppButton : Control, IThemeable
    {
        private bool _isHovered;

        public bool IsPrimary { get; set; } = true;

        public AppButton()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.UserPaint |
                ControlStyles.DoubleBuffer |
                ControlStyles.ResizeRedraw, true);

            Cursor = Cursors.Hand;
            Size   = new Size(100, 36);
            Font   = AppFonts.Body;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var rect   = new RectangleF(1, 1, Width - 2, Height - 2);
            float radius = AppSpacing.RadiusXl;

            if (IsPrimary)
            {
                Color bg = _isHovered ? AppColors.AccentDark : AppColors.Accent;
                using var brush = new SolidBrush(bg);
                g.FillRoundedRect(brush, rect, radius);
                using var tb = new SolidBrush(Color.White);
                DrawCenteredText(g, tb);
            }
            else
            {
                using var pen = new Pen(AppColors.BorderPrimary, 1f);
                g.DrawRoundedRect(pen, rect, radius);
                using var tb = new SolidBrush(AppColors.TextPrimary);
                DrawCenteredText(g, tb);
            }
        }

        private void DrawCenteredText(Graphics g, Brush brush)
        {
            var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString(Text, Font, brush, new RectangleF(0, 0, Width, Height), sf);
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); _isHovered = true;  Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _isHovered = false; Invalidate(); }

        public void ApplyTheme() => Invalidate();
    }
}
