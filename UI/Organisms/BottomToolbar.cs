using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using PixelPerfect.UI.Atoms;

namespace PixelPerfect.UI.Organisms
{
    public enum ActiveTool { Select, ColorSettings, Photo, ColorSpace }

    /// <summary>
    /// Centered floating pill toolbar that lives as a child of CanvasPanel.
    /// Because it is a direct child of the canvas, Color.Transparent inherits
    /// the canvas background paint — no black rectangle, no WS_EX_TRANSPARENT
    /// hacks that break mouse input.
    /// </summary>
    public class BottomToolbar : UserControl, IThemeable
    {
        // ── Public buttons ────────────────────────────────────────────────────
        public IconButton BtnSelect;        // Group 1: cursor/select
        public IconButton BtnColorSettings; // Group 2: toggle color settings panel
        public IconButton BtnPhoto;         // Group 3: show image canvas
        public IconButton BtnColorSpace;    // Group 3: show 3D color space viewer

        // ── State ─────────────────────────────────────────────────────────────
        public ActiveTool ActiveTool { get; private set; } = ActiveTool.Photo;

        // ── Events ────────────────────────────────────────────────────────────
        public event EventHandler<ActiveTool> ToolChanged;

        // ── Pill geometry ──────────────────────────────────────────────────────
        private const int BtnSize    = 44;          // button hit area
        private const int IconPct    = 60;          // icon is 60% of BtnSize → 26px
        private const int PillPadX   = 16;
        private const int PillPadY   = 6;
        private const int DividerW   = 18;
        private const int PillBottom = 10;

        private static int IconSz => BtnSize * IconPct / 100;  // derived, never clips

        private RectangleF _pillRect;

        public BottomToolbar()
        {
            // Toolbar height = pill height + bottom gap
            Height    = BtnSize + PillPadY * 2 + PillBottom;

            SetStyle(
                ControlStyles.AllPaintingInWmPaint          |
                ControlStyles.UserPaint                     |
                ControlStyles.DoubleBuffer                  |
                ControlStyles.SupportsTransparentBackColor  |
                ControlStyles.ResizeRedraw, true);
            BackColor = Color.Transparent;

            BtnSelect        = MakeBtn("Cursor.svg");
            BtnColorSettings = MakeBtn("rgb.svg");
            BtnPhoto         = MakeBtn("Gallery.svg");
            BtnColorSpace    = MakeBtn("cube.svg");

            BtnSelect.Click        += (s, e) => SetActiveTool(ActiveTool.Select);
            BtnColorSettings.Click += (s, e) => SetActiveTool(ActiveTool.ColorSettings);
            BtnPhoto.Click         += (s, e) => SetActiveTool(ActiveTool.Photo);
            BtnColorSpace.Click    += (s, e) => SetActiveTool(ActiveTool.ColorSpace);

            Controls.AddRange(new Control[] { BtnSelect, BtnColorSettings, BtnPhoto, BtnColorSpace });

            BtnPhoto.IsActive = true;

            Resize += (s, e) => LayoutControls();
            LayoutControls();
        }

        private static IconButton MakeBtn(string svg) => new IconButton
        {
            SvgFileName = svg,
            Width       = BtnSize,
            Height      = BtnSize,
            IconSize    = IconSz,
        };

        // ── State management ──────────────────────────────────────────────────

        private void SetActiveTool(ActiveTool tool)
        {
            ActiveTool                = tool;
            BtnSelect.IsActive        = tool == ActiveTool.Select;
            BtnColorSettings.IsActive = tool == ActiveTool.ColorSettings;
            BtnPhoto.IsActive         = tool == ActiveTool.Photo;
            BtnColorSpace.IsActive    = tool == ActiveTool.ColorSpace;
            ToolChanged?.Invoke(this, tool);
        }

        // ── Layout ────────────────────────────────────────────────────────────

        private void LayoutControls()
        {
            int contentW = BtnSize * 4 + DividerW * 2;
            int pillW    = contentW + PillPadX * 2;
            int pillH    = BtnSize  + PillPadY * 2;
            int pillX    = (Width  - pillW) / 2;
            int pillY    = Height  - pillH - PillBottom;
            _pillRect    = new RectangleF(pillX, pillY, pillW, pillH);

            int btnY = pillY + PillPadY;
            int x    = pillX + PillPadX;

            BtnSelect.Bounds        = new Rectangle(x, btnY, BtnSize, BtnSize);
            x += BtnSize + DividerW;

            BtnColorSettings.Bounds = new Rectangle(x, btnY, BtnSize, BtnSize);
            x += BtnSize + DividerW;

            BtnPhoto.Bounds         = new Rectangle(x, btnY, BtnSize, BtnSize);
            x += BtnSize + AppSpacing.GapSm;

            BtnColorSpace.Bounds    = new Rectangle(x, btnY, BtnSize, BtnSize);

            UpdateWindowRegion();
        }

        private void UpdateWindowRegion()
        {
            int capsuleRadius = (int)(_pillRect.Height / 2f);
            using var path = CreateRoundedPath(_pillRect, capsuleRadius);
            Region oldRegion = Region;
            Region = new Region(path);
            oldRegion?.Dispose();
        }

        private static GraphicsPath CreateRoundedPath(RectangleF rect, float radius)
        {
            var path = new GraphicsPath();
            float r = Math.Max(0f, Math.Min(radius, Math.Min(rect.Width, rect.Height) / 2f));
            if (r <= 0f)
            {
                path.AddRectangle(rect);
                path.CloseFigure();
                return path;
            }

            float d = r * 2f;
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        // ── Paint ─────────────────────────────────────────────────────────────

        // Paint the actual parent surface behind us (including image / GL area),
        // then draw only the floating pill on top.
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            if (Parent == null)
            {
                base.OnPaintBackground(e);
                return;
            }

            // Render the real parent surface behind us (not just a flat BackColor).
            GraphicsState state = e.Graphics.Save();
            try
            {
                e.Graphics.TranslateTransform(-Left, -Top);
                var parentClip = new Rectangle(Left, Top, Width, Height);
                var pe = new PaintEventArgs(e.Graphics, parentClip);
                InvokePaintBackground(Parent, pe);
                InvokePaint(Parent, pe);
            }
            finally
            {
                e.Graphics.Restore(state);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Fully-rounded capsule: radius = half the pill height
            int capsuleRadius = (int)(_pillRect.Height / 2);

            // Pill background
            // Child transparent buttons repaint their parent area; using an
            // opaque base pill prevents visible alpha stacking blocks.
            int fillAlpha = 255;
            using (var bg = new SolidBrush(Color.FromArgb(fillAlpha, AppColors.PanelBg)))
                g.FillRoundedRect(bg, _pillRect, capsuleRadius);

            // Pill border
            using (var pen = new Pen(AppColors.BorderPrimary, 1f))
                g.DrawRoundedRect(pen, _pillRect, capsuleRadius);

            // Divider after BtnSelect
            DrawDivider(g, BtnSelect.Right + DividerW / 2, (int)_pillRect.Top + 6, (int)_pillRect.Bottom - 6);

            // Divider after BtnColorSettings
            DrawDivider(g, BtnColorSettings.Right + DividerW / 2, (int)_pillRect.Top + 6, (int)_pillRect.Bottom - 6);
        }

        private void DrawDivider(Graphics g, int x, int top, int bottom)
        {
            using var pen = new Pen(AppColors.BorderPrimary, 1f);
            g.DrawLine(pen, x, top, x, bottom);
        }

        // ── Theme ─────────────────────────────────────────────────────────────

        public void ApplyTheme()
        {
            foreach (Control c in Controls)
                if (c is IThemeable t) t.ApplyTheme();
            Invalidate();
        }
    }
}
