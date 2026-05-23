using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using PixelPerfect.UI.Atoms;
using PixelPerfect.UI.Molecules;

namespace PixelPerfect.UI.Dialogs
{
    /// <summary>
    /// Borderless confirmation dialog for "Reset Changes".
    /// Returns DialogResult.OK when the user confirms reset.
    /// </summary>
    public class ResetDialog : Form
    {
        public ResetDialog()
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition   = FormStartPosition.CenterParent;
            Size            = new Size(360, 180);
            BackColor       = AppColors.PanelBg;
            ShowInTaskbar   = false;

            KeyPreview = true;
            KeyDown   += (s, e) => { if (e.KeyCode == Keys.Escape) { DialogResult = DialogResult.Cancel; Close(); } };

            BuildControls();
        }

        private void BuildControls()
        {
            int pad = AppSpacing.PadLg;

            var title = new Label
            {
                Text      = "Reset Changes",
                Font      = AppFonts.Header,
                ForeColor = AppColors.TextPrimary,
                AutoSize  = true,
                Location  = new Point(pad, pad)
            };

            var body = new Label
            {
                Text      = "Are you sure you want to reset all changes?\nThis action cannot be undone.",
                Font      = AppFonts.Label,
                ForeColor = AppColors.TextSecondary,
                AutoSize  = false,
                Width     = Width - pad * 2,
                Height    = 40,
                Location  = new Point(pad, pad + title.Height + AppSpacing.GapXl)
            };

            var btnRow = new DialogButtonRow
            {
                PrimaryText   = "RESET",
                SecondaryText = "CANCEL",
                Bounds        = new Rectangle(0, Height - 52, Width, 52)
            };
            btnRow.PrimaryClicked   += (s, e) => { DialogResult = DialogResult.OK;     Close(); };
            btnRow.SecondaryClicked += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

            Controls.AddRange(new Control[] { title, body, btnRow });
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using var pen = new Pen(AppColors.BorderPrimary, 1f);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.DrawRoundedRect(pen, new RectangleF(0, 0, Width - 1, Height - 1), AppSpacing.Radius4Xl);
        }
    }
}
