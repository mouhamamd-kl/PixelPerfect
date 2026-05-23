using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using PixelPerfect.UI.Atoms;

namespace PixelPerfect.UI.Organisms
{
    /// <summary>
    /// Vertical icon toolbar on the left edge.
    /// Top group: Back, Forward, Open, Eyedropper, Export.
    /// Bottom group: Theme toggle, Settings.
    /// </summary>
    public class LeftToolbar : UserControl, IThemeable
    {
        public IconButton BtnBack;
        public IconButton BtnForward;
        public IconButton BtnOpen;
        public IconButton BtnEyedropper;
        public IconButton BtnExport;
        public IconButton BtnTheme;
        public IconButton BtnSettings;

        public event EventHandler BackClicked;
        public event EventHandler ForwardClicked;
        public event EventHandler OpenClicked;
        public event EventHandler EyedropperToggled;
        public event EventHandler ExportClicked;
        public event EventHandler ThemeToggled;

        public LeftToolbar()
        {
            Width     = AppSpacing.LeftToolbarWidth;
            BackColor = AppColors.PanelBg;

            BtnBack        = MakeBtn("Reply.svg");
            BtnForward     = MakeBtn("Forward.svg");
            BtnOpen        = MakeBtn("folder.svg");
            BtnEyedropper  = MakeBtn("Pipette.svg", isToggle: true);
            BtnExport      = MakeBtn("Export.svg");
            BtnTheme       = MakeBtn("theme.svg",   isToggle: true);
            BtnSettings    = MakeBtn("exit.svg");

            BtnBack.Click        += (s, e) => BackClicked?.Invoke(this, e);
            BtnForward.Click     += (s, e) => ForwardClicked?.Invoke(this, e);
            BtnOpen.Click        += (s, e) => OpenClicked?.Invoke(this, e);
            BtnEyedropper.Click  += (s, e) => EyedropperToggled?.Invoke(this, e);
            BtnExport.Click      += (s, e) => ExportClicked?.Invoke(this, e);
            BtnTheme.Click       += (s, e) => ThemeToggled?.Invoke(this, e);

            Controls.AddRange(new Control[]
            {
                BtnBack, BtnForward, BtnOpen, BtnEyedropper, BtnExport, BtnTheme, BtnSettings
            });

            Resize += (s, e) => LayoutControls();
            LayoutControls();
        }

        private static IconButton MakeBtn(string svg, bool isToggle = false)
        {
            return new IconButton { SvgFileName = svg, IsToggle = isToggle };
        }

        private void LayoutControls()
        {
            int x = (Width - AppSpacing.LeftToolbarWidth) / 2;
            int btnH = AppSpacing.LeftToolbarWidth;

            // Top group
            BtnBack.Bounds       = new Rectangle(x, 0,          AppSpacing.LeftToolbarWidth, btnH);
            BtnForward.Bounds    = new Rectangle(x, btnH,       AppSpacing.LeftToolbarWidth, btnH);
            BtnOpen.Bounds       = new Rectangle(x, btnH * 2,   AppSpacing.LeftToolbarWidth, btnH);
            BtnEyedropper.Bounds = new Rectangle(x, btnH * 3,   AppSpacing.LeftToolbarWidth, btnH);
            BtnExport.Bounds     = new Rectangle(x, btnH * 4,   AppSpacing.LeftToolbarWidth, btnH);

            // Bottom group pinned to bottom
            BtnSettings.Bounds   = new Rectangle(x, Height - btnH,     AppSpacing.LeftToolbarWidth, btnH);
            BtnTheme.Bounds      = new Rectangle(x, Height - btnH * 2, AppSpacing.LeftToolbarWidth, btnH);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            // Right border separator
            using var pen = new Pen(AppColors.BorderPrimary, 1f);
            e.Graphics.DrawLine(pen, Width - 1, 0, Width - 1, Height);

            // Separator line after top group
            int sepY = AppSpacing.LeftToolbarWidth * 5 + 8;
            if (sepY < Height - AppSpacing.LeftToolbarWidth * 2 - 8)
            {
                e.Graphics.DrawLine(pen,
                    AppSpacing.GapXl, sepY,
                    Width - AppSpacing.GapXl, sepY);
            }
        }

        public void ApplyTheme()
        {
            BackColor = AppColors.PanelBg;
            foreach (Control c in Controls)
                if (c is IThemeable t) t.ApplyTheme();
            Invalidate();
        }
    }
}
