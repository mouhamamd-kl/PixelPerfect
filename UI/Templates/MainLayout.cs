using System.Drawing;
using System.Windows.Forms;
using PixelPerfect.Models;
using PixelPerfect.UI.Atoms;
using PixelPerfect.UI.Organisms;

namespace PixelPerfect.UI.Templates
{
    /// <summary>
    /// Composes all organisms into the main editor layout.
    ///
    ///  ┌──────────────────────────────────────────────────────┐
    ///  │                     TopBar                           │
    ///  ├────┬──────────────────────────────────┬──────────────┤
    ///  │    │                                  │              │
    ///  │ L  │     CanvasPanel (Fill)            │  ColorSettings│
    ///  │ e  │   [ColorSpaceBar floats above]   │  Panel       │
    ///  │ f  │   [BottomToolbar floats below]   │              │
    ///  │ t  │                                  │              │
    ///  └────┴──────────────────────────────────┴──────────────┘
    ///
    /// BottomToolbar and ColorSpaceBar are attached to the active center surface
    /// (CanvasPanel or ColorSpaceViewer) so transparency blends with what is
    /// actually visible behind them.
    ///
    /// Tool modes:
    ///   Select       → canvas visible, no secondary bar
    ///   ColorSettings → canvas visible, ColorSpaceBar visible above toolbar,
    ///                   ColorSettingsPanel visible on right
    ///   Photo        → canvas visible, no secondary bar
    ///   ColorSpace   → ColorSpaceViewer visible, no secondary bar
    /// </summary>
    public class MainLayout : UserControl, IThemeable
    {
        public TopBar                TopBar             { get; }
        public LeftToolbar           LeftToolbar        { get; }
        public CanvasPanel           CanvasPanel        { get; }
        public ColorSpaceViewerPanel ColorSpaceViewer   { get; }
        public ColorSettingsPanel    ColorSettingsPanel { get; }
        public BottomToolbar         BottomToolbar      { get; }
        public ColorSpaceBar         ColorSpaceBar      { get; }

        public MainLayout()
        {
            TopBar             = new TopBar();
            LeftToolbar        = new LeftToolbar();
            CanvasPanel        = new CanvasPanel();
            ColorSpaceViewer   = new ColorSpaceViewerPanel { Visible = false };
            ColorSettingsPanel = new ColorSettingsPanel    { Visible = true };
            BottomToolbar      = new BottomToolbar();
            ColorSpaceBar      = new ColorSpaceBar         { Visible = false };

            TopBar.Dock             = DockStyle.Top;
            LeftToolbar.Dock        = DockStyle.Left;
            ColorSettingsPanel.Dock = DockStyle.Right;

            CanvasPanel.Dock      = DockStyle.Fill;
            ColorSpaceViewer.Dock = DockStyle.None;

            BottomToolbar.Dock = DockStyle.None;
            ColorSpaceBar.Dock = DockStyle.None;

            // Add top-level structure in back-to-front order.
            Controls.Add(CanvasPanel);          // back: image canvas
            Controls.Add(ColorSpaceViewer);     // overlays canvas in 3D mode
            Controls.Add(ColorSettingsPanel);   // right panel
            Controls.Add(LeftToolbar);
            Controls.Add(TopBar);

            // Floating overlays are children of the active center surface
            // (CanvasPanel / ColorSpaceViewer) so transparency blends with content.
            CanvasPanel.Controls.Add(ColorSpaceBar);
            CanvasPanel.Controls.Add(BottomToolbar);

            Padding = new Padding(0);

            Resize             += (s, e) => PositionOverlays();
            CanvasPanel.Resize += (s, e) => PositionOverlays();

            BottomToolbar.ToolChanged += (s, tool) => OnToolChanged(tool);

            // Wire ColorSpaceBar → ColorSettingsPanel so the sliders follow the active mode
            ColorSpaceBar.ModeChanged += (s, mode) =>
            {
                ColorSettingsPanel.SetColorSpaceMode(mode);
                ColorSpaceViewer.SetColorSpaceMode(mode);
            };

            // Start on Photo view
            OnToolChanged(ActiveTool.Photo);
            PositionOverlays();
        }

        // ── Positioning ───────────────────────────────────────────────────────

        private void PositionOverlays()
        {
            var  cp  = CanvasPanel.Bounds;   // position in MainLayout coords
            int  tbH = BottomToolbar.Height;
            int  csH = ColorSpaceBar.Height;
            int  gap = 8;

            // In 3D mode the toolbar should overlay the GL scene directly.
            // Keep no reserved strip so transparency blends with rendered content.
            ColorSpaceViewer.BottomReserve = 0;
            ColorSpaceViewer.Bounds = cp;
            if (ColorSpaceViewer.Visible) ColorSpaceViewer.BringToFront();

            Control host = GetActiveOverlayHost();

            // BottomToolbar floats at the bottom of the active center surface.
            BottomToolbar.Bounds = new Rectangle(0, host.Height - tbH, host.Width, tbH);
            BottomToolbar.BringToFront();

            // ColorSpaceBar sits above BottomToolbar
            ColorSpaceBar.Bounds = new Rectangle(0, host.Height - tbH - gap - csH, host.Width, csH);
            if (ColorSpaceBar.Visible) ColorSpaceBar.BringToFront();
        }

        // ── Tool switching ────────────────────────────────────────────────────

        private void OnToolChanged(ActiveTool tool)
        {
            bool showColorSettings = tool == ActiveTool.ColorSettings;
            bool showColorSpace    = tool == ActiveTool.ColorSpace;

            // ColorSpaceViewer overlays CanvasPanel from within — CanvasPanel always visible
            ColorSpaceViewer.Visible = showColorSpace;
            CanvasPanel.SuppressMouseWheel = showColorSpace;
            if (showColorSpace)
            {
                ColorSpaceViewer.SetColorSettings(ColorSettingsPanel.GetSettings());
                ColorSpaceViewer.Activate();
            }

            // Secondary bar only when color settings is the active tool
            ColorSpaceBar.Visible = showColorSettings;

            AttachOverlaysToActiveSurface();
            PositionOverlays();
        }

        private Control GetActiveOverlayHost()
            => ColorSpaceViewer.Visible ? (Control)ColorSpaceViewer : CanvasPanel;

        private void AttachOverlaysToActiveSurface()
        {
            Control host = GetActiveOverlayHost();
            if (BottomToolbar.Parent != host) host.Controls.Add(BottomToolbar);
            if (ColorSpaceBar.Parent != host) host.Controls.Add(ColorSpaceBar);
        }

        public void ApplyTheme()
        {
            TopBar.ApplyTheme();
            LeftToolbar.ApplyTheme();
            CanvasPanel.ApplyTheme();
            ColorSpaceViewer.ApplyTheme();
            ColorSettingsPanel.ApplyTheme();
            BottomToolbar.ApplyTheme();
            ColorSpaceBar.ApplyTheme();
            BackColor = AppColors.Background;
            PositionOverlays();
        }
    }
}
