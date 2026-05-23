using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;
using PixelPerfect.Models;
using PixelPerfect.UI.Atoms;
using PixelPerfect.UI.Molecules;

namespace PixelPerfect.UI.Dialogs
{
    /// <summary>
    /// Borderless Export dialog.
    /// Format: PNG / JPEG / BMP toggle
    /// Save To: path + folder browse button
    /// </summary>
    public class ExportDialog : Form
    {
        public ExportSettings Result { get; private set; }

        private string _selectedFormat = "PNG";
        private AppButton _btnPng, _btnJpeg, _btnBmp;
        private TextBox   _pathBox;
        private DialogButtonRow _btnRow;

        public ExportDialog()
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition   = FormStartPosition.CenterParent;
            Size            = new Size(400, 260);
            BackColor       = AppColors.PanelBg;
            ShowInTaskbar   = false;

            KeyPreview = true;
            KeyDown   += (s, e) => { if (e.KeyCode == Keys.Escape) DialogResult = DialogResult.Cancel; };

            BuildControls();
        }

        private void BuildControls()
        {
            int pad = AppSpacing.PadLg;

            // Title
            var title = new Label
            {
                Text      = "Export image",
                Font      = AppFonts.Header,
                ForeColor = AppColors.TextPrimary,
                AutoSize  = true,
                Location  = new Point(pad, pad)
            };

            var sub = new Label
            {
                Text      = "Save your modified image to disk",
                Font      = AppFonts.Label,
                ForeColor = AppColors.TextSecondary,
                AutoSize  = true,
                Location  = new Point(pad, pad + title.Height + AppSpacing.GapSm)
            };

            // Format row
            var fmtLabel = new Label { Text = "Format", Font = AppFonts.Label, ForeColor = AppColors.TextSecondary, AutoSize = true, Location = new Point(pad, 80) };
            _btnPng  = MakeFormatBtn("PNG",  new Point(pad, 100));
            _btnJpeg = MakeFormatBtn("JPEG", new Point(pad + 70, 100));
            _btnBmp  = MakeFormatBtn("BMP",  new Point(pad + 140, 100));
            SelectFormat("PNG");

            // Save to row
            var saveLabel = new Label { Text = "Save To", Font = AppFonts.Label, ForeColor = AppColors.TextSecondary, AutoSize = true, Location = new Point(pad, 148) };

            _pathBox = new TextBox
            {
                Text      = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "export.png"),
                Font      = AppFonts.Label,
                ForeColor = AppColors.TextPrimary,
                BackColor = AppColors.SurfaceAlt,
                BorderStyle = BorderStyle.None,
                Location  = new Point(pad, 168),
                Width     = Width - pad * 2 - 36
            };

            var browseBtn = new IconButton
            {
                SvgFileName = "folder.svg",
                IconSize    = 16,
                Location    = new Point(_pathBox.Right + AppSpacing.GapSm, 162),
                Size        = new Size(28, 28)
            };
            browseBtn.Click += BrowseClicked;

            _btnRow = new DialogButtonRow
            {
                PrimaryText   = "Save",
                SecondaryText = "Cancel",
                Bounds        = new Rectangle(0, Height - 52, Width, 52)
            };
            _btnRow.PrimaryClicked   += SaveClicked;
            _btnRow.SecondaryClicked += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

            Controls.AddRange(new Control[] { title, sub, fmtLabel, _btnPng, _btnJpeg, _btnBmp, saveLabel, _pathBox, browseBtn, _btnRow });
        }

        private AppButton MakeFormatBtn(string format, Point location)
        {
            var btn = new AppButton { Text = format, IsPrimary = false, Width = 60, Height = 28, Location = location, Font = AppFonts.Label };
            btn.Click += (s, e) => SelectFormat(format);
            return btn;
        }

        private void SelectFormat(string format)
        {
            _selectedFormat  = format;
            _btnPng.IsPrimary  = format == "PNG";
            _btnJpeg.IsPrimary = format == "JPEG";
            _btnBmp.IsPrimary  = format == "BMP";
            _btnPng.Invalidate(); _btnJpeg.Invalidate(); _btnBmp.Invalidate();

            // Update suggested extension
            if (_pathBox != null)
            {
                string dir  = Path.GetDirectoryName(_pathBox.Text) ?? "";
                string stem = Path.GetFileNameWithoutExtension(_pathBox.Text);
                string ext  = format == "JPEG" ? ".jpg" : "." + format.ToLower();
                _pathBox.Text = Path.Combine(dir, stem + ext);
            }
        }

        private void BrowseClicked(object s, EventArgs e)
        {
            using var dlg = new FolderBrowserDialog { SelectedPath = Path.GetDirectoryName(_pathBox.Text) ?? "" };
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                string ext = _selectedFormat == "JPEG" ? ".jpg" : "." + _selectedFormat.ToLower();
                _pathBox.Text = Path.Combine(dlg.SelectedPath, "export" + ext);
            }
        }

        private void SaveClicked(object s, EventArgs e)
        {
            Result = new ExportSettings
            {
                Format   = _selectedFormat,
                SavePath = _pathBox.Text,
                JpegQuality = 90
            };
            DialogResult = DialogResult.OK;
            Close();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using var pen = new Pen(AppColors.BorderPrimary, 1f);
            g.DrawRoundedRect(pen, new RectangleF(0, 0, Width - 1, Height - 1), AppSpacing.Radius4Xl);
        }
    }
}
