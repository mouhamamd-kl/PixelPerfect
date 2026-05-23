using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using PixelPerfect.Models;
using PixelPerfect.Services;
using PixelPerfect.UI.Atoms;
using PixelPerfect.UI.Dialogs;
using PixelPerfect.UI.Organisms;
using PixelPerfect.UI.Templates;

namespace PixelPerfect
{
    /// <summary>
    /// The application's main form — acts as the Presenter layer.
    /// Owns the ImageModel and IImageService; wires all UI events to service calls.
    /// No layout logic lives here — all of that is in MainLayout and its organisms.
    /// </summary>
    public partial class MainEditorForm : Form
    {
        private readonly IImageService _imageService;
        private readonly MainLayout    _layout;

        private ImageModel _model;
        private Bitmap     _displayBitmap;  // result of ApplyColorSettings on original
        private System.Windows.Forms.Timer _debounce; // delays processing while sliders are dragged
        private CancellationTokenSource _cts = new CancellationTokenSource();

        public MainEditorForm()
        {
            InitializeComponent();
            KeyPreview = true;

            _imageService = new ImageService();

            _layout = new MainLayout { Dock = DockStyle.Fill };
            Controls.Add(_layout);

            _debounce = new System.Windows.Forms.Timer { Interval = 80 };
            _debounce.Tick += OnDebounce;

            WireEvents();
            ApplyTheme();
            AppColors.ThemeChanged += (s, e) => ApplyTheme();
        }

        // ── Event wiring ─────────────────────────────────────────────────────

        private void WireEvents()
        {
            var lt = _layout.LeftToolbar;
            lt.OpenClicked       += (s, e) => OpenImage();
            lt.ExportClicked     += (s, e) => ExportImage();
            lt.EyedropperToggled += (s, e) => _layout.CanvasPanel.SetEyedropperMode(lt.BtnEyedropper.IsActive);
            lt.BackClicked       += (s, e) => Undo();
            lt.ForwardClicked    += (s, e) => Redo();
            lt.ThemeToggled      += (s, e) => AppColors.ToggleTheme();

            _layout.TopBar.ResetChangesClicked += (s, e) => ResetChanges();

            _layout.ColorSettingsPanel.SettingsChanged += (s, e) =>
            {
                // Keep 3D preview in sync with right-panel slider changes.
                var settings = _layout.ColorSettingsPanel.GetSettings();
                _layout.ColorSpaceViewer.SetColorSettings(settings);

                _debounce.Stop();
                _debounce.Start();
            };

            // Keep right-panel slider semantics synced with the active 3D mode.
            _layout.ColorSpaceViewer.ModeChanged += (s, mode) =>
                _layout.ColorSettingsPanel.SetColorSpaceMode(mode);

            _layout.CanvasPanel.PixelPicked       += OnPixelPicked;
            _layout.CanvasPanel.FileDropped       += (s, path) => LoadImageFromPath(path);
            _layout.CanvasPanel.BrowseClicked     += (s, e) => OpenImage();
            _layout.ColorSpaceViewer.ColorPicked  += OnPixelPicked;

            _layout.BottomToolbar.ToolChanged += (s, tool) =>
            {
                _layout.CanvasPanel.SetEyedropperMode(false);
                lt.BtnEyedropper.IsActive = false;
            };
        }

        // ── Image operations ─────────────────────────────────────────────────

        private void OpenImage()
        {
            using var dlg = new OpenFileDialog
            {
                Filter = "Images (*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tiff)|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tiff|All Files (*.*)|*.*",
                Title  = "Open Image"
            };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            LoadImageFromPath(dlg.FileName);
        }

        private void LoadImageFromPath(string path)
        {
            try
            {
                _model?.Dispose();
                _displayBitmap?.Dispose();

                _model         = _imageService.LoadImage(path);
                _displayBitmap = new Bitmap(_model.WorkingBitmap);

                _layout.CanvasPanel.SetBitmap(_displayBitmap);
                _layout.TopBar.UpdateImageInfo(
                    _model.Width, _model.Height,
                    _model.Format, _model.FileSizeBytes,
                    _model.ColorMode);

                _layout.ColorSettingsPanel.ResetToDefaults();
                _layout.ColorSpaceViewer.SetColorSettings(_layout.ColorSettingsPanel.GetSettings());
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not open image: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ExportImage()
        {
            if (_model == null) return;
            using var dlg = new ExportDialog();
            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            // Apply current settings to working bitmap before export
            var settings = _layout.ColorSettingsPanel.GetSettings();
            using var exportBmp = _imageService.ApplyColorSettings(_model.OriginalBitmap, settings);
            _model.WorkingBitmap?.Dispose();
            _model.WorkingBitmap = new Bitmap(exportBmp);

            try
            {
                _imageService.ExportImage(_model, dlg.Result);
                MessageBox.Show("Image saved successfully.", "Export", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Export failed: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Delete && _model != null)
                ClearImage();
        }

        private void ClearImage()
        {
            _cts.Cancel();
            _debounce.Stop();

            _model?.Dispose();
            _model = null;
            _displayBitmap?.Dispose();
            _displayBitmap = null;

            _layout.CanvasPanel.SetBitmap(null);
            _layout.TopBar.UpdateImageInfo(0, 0, null, 0, null);
            _layout.ColorSettingsPanel.ResetToDefaults();
            _layout.ColorSpaceViewer.SetColorSettings(_layout.ColorSettingsPanel.GetSettings());
        }

        private void ResetChanges()
        {
            if (_model == null) return;
            using var dlg = new ResetDialog();
            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            _model.ResetToOriginal();
            _layout.ColorSettingsPanel.ResetToDefaults();
            _layout.ColorSpaceViewer.SetColorSettings(_layout.ColorSettingsPanel.GetSettings());

            _displayBitmap?.Dispose();
            _displayBitmap = new Bitmap(_model.WorkingBitmap);
            _layout.CanvasPanel.SetBitmap(_displayBitmap);
        }

        // ── Debounced color settings ──────────────────────────────────────────

        private async void OnDebounce(object s, EventArgs e)
        {
            _debounce.Stop();
            if (_model == null) return;

            // Cancel any previous in-flight processing
            _cts.Cancel();
            _cts.Dispose();
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            var settings = _layout.ColorSettingsPanel.GetSettings();

            // Snapshot the source bitmap for the background thread
            Bitmap source = new Bitmap(_model.OriginalBitmap);

            try
            {
                // Run heavy work off the UI thread
                var (result, dominant) = await Task.Run(() =>
                {
                    token.ThrowIfCancellationRequested();
                    Bitmap bmp = _imageService.ApplyColorSettings(source, settings);
                    token.ThrowIfCancellationRequested();

                    System.Collections.Generic.IReadOnlyList<Color> colors =
                        System.Array.Empty<Color>();
                    if (settings.QuantizeCount > 0)
                        colors = _imageService.GetDominantColors(source, settings.QuantizeCount);

                    return (bmp, colors);
                }, token);

                // Back on UI thread — check again before touching UI
                if (token.IsCancellationRequested)
                {
                    result.Dispose();
                    source.Dispose();
                    return;
                }

                _displayBitmap?.Dispose();
                _displayBitmap = result;
                _model.WorkingBitmap?.Dispose();
                _model.WorkingBitmap = new Bitmap(result);

                _layout.CanvasPanel.SetBitmap(_displayBitmap);

                if (dominant.Count > 0)
                    _layout.ColorSettingsPanel.SetDominantColors(dominant);
            }
            catch (OperationCanceledException) { /* superseded by newer request */ }
            catch (Exception ex)
            {
                MessageBox.Show($"Processing failed: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                source.Dispose();
            }
        }

        // ── Undo / Redo (2-state) ─────────────────────────────────────────────

        private void Undo()
        {
            if (_model == null) return;
            // Simple: revert to original
            _model.ResetToOriginal();
            _layout.ColorSettingsPanel.ResetToDefaults();
            _displayBitmap?.Dispose();
            _displayBitmap = new Bitmap(_model.WorkingBitmap);
            _layout.CanvasPanel.SetBitmap(_displayBitmap);
        }

        private void Redo()
        {
            // Re-apply current slider settings
            OnDebounce(this, EventArgs.Empty);
        }

        // ── Eyedropper / pixel picking ────────────────────────────────────────

        private void OnPixelPicked(object s, Color color)
        {
            _layout.ColorSettingsPanel.SetPickedColor(color);

            var popup = new ColorPickerPopup();
            popup.SetColor(color);

            // Position near cursor, ensure it stays on screen
            Point pos = Cursor.Position;
            pos.X += 12;
            pos.Y += 12;
            Rectangle screen = Screen.FromPoint(pos).WorkingArea;
            if (pos.X + popup.Width  > screen.Right)  pos.X = screen.Right  - popup.Width;
            if (pos.Y + popup.Height > screen.Bottom) pos.Y = screen.Bottom - popup.Height;
            popup.Location = pos;

            popup.Show(this);
        }

        // ── Theme ────────────────────────────────────────────────────────────

        private void ApplyTheme()
        {
            BackColor = AppColors.Background;
            _layout.ApplyTheme();
        }

        // ── Cleanup ───────────────────────────────────────────────────────────

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            base.OnFormClosed(e);
            _cts?.Cancel();
            _cts?.Dispose();
            _debounce?.Dispose();
            _model?.Dispose();
            _displayBitmap?.Dispose();
            AppFonts.DisposeAll();
        }
    }
}
