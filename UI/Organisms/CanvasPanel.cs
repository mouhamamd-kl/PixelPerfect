using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using OpenTK.GLControl;
using OpenTK.Graphics.OpenGL4;
using PixelPerfect.UI.Atoms;

namespace PixelPerfect.UI.Organisms
{
    /// <summary>
    /// GPU-accelerated image canvas.
    ///
    /// Rendering split:
    ///   GLControl  → checkerboard + image texture (fragment shader, zero CPU per frame)
    ///   OnPaint    → rulers + corner fill drawn on top via GDI+ (cheap vector/text)
    ///
    /// Pan and zoom are uniform uploads — cost is the same for any image size.
    /// </summary>
    public class CanvasPanel : UserControl, IThemeable
    {
        // ── GL sub-control ────────────────────────────────────────────────────
        private GLControl _gl;
        private bool      _glReady;

        // ── GL resources ──────────────────────────────────────────────────────
        private int _vao, _vbo;
        private int _prog = -1;
        private int _tex  = -1;       // image texture
        private int _imgW, _imgH;     // original image dimensions

        // ── Image ─────────────────────────────────────────────────────────────
        private Bitmap _bitmap;

        // ── View state ────────────────────────────────────────────────────────
        private float  _zoom      = 1.0f;
        private PointF _panOffset = PointF.Empty;

        // ── Mouse ─────────────────────────────────────────────────────────────
        private Point _lastMouse;
        private bool  _isPanning;
        private bool  _isEyedropper;
        public  bool  SuppressMouseWheel { get; set; }

        // ── Events ────────────────────────────────────────────────────────────
        public event EventHandler<Color>  PixelPicked;
        public event EventHandler<string> FileDropped;

        // ── GLSL ──────────────────────────────────────────────────────────────

        private const string VertSrc = @"#version 330 core
layout(location=0) in vec2 aPos;
out vec2 vUV;
void main() {
    vUV = aPos * 0.5 + 0.5;
    gl_Position = vec4(aPos, 0.0, 1.0);
}";

        // Draws checkerboard everywhere, then blends the image quad on top.
        // uImgRect = (x, y, w, h) in screen pixels; uRes = viewport size.
        // The checkerboard is anchored to the image origin so it scrolls with the image.
        private const string FragSrc = "#version 330 core\n" +
"in  vec2 vUV;\n" +
"out vec4 fragColor;\n" +
"uniform sampler2D uTex;\n" +
"uniform vec4  uImgRect;\n" +
"uniform vec2  uRes;\n" +
"uniform vec3  uBg;\n" +
"uniform int   uHasImage;\n" +
"uniform float uZoom;\n" +
"void main() {\n" +
"    vec2 screen = vUV * uRes;\n" +
"    screen.y = uRes.y - screen.y;\n" +
"    fragColor = vec4(uBg, 1.0);\n" +
"    if (uHasImage == 0) return;\n" +
"    float ix = uImgRect.x, iy = uImgRect.y, iw = uImgRect.z, ih = uImgRect.w;\n" +
"    if (screen.x >= ix && screen.x < ix+iw && screen.y >= iy && screen.y < iy+ih) {\n" +
"        float cx = screen.x - ix;\n" +
"        float cy = screen.y - iy;\n" +
"        float sz = max(1.0, round(8.0 * uZoom));\n" +
"        float odd = mod(floor(cx/sz) + floor(cy/sz), 2.0);\n" +
"        fragColor = odd > 0.5 ? vec4(0.627, 0.627, 0.627, 1.0) : vec4(0.784, 0.784, 0.784, 1.0);\n" +
"    }\n" +
"    if (screen.x >= ix && screen.x < ix+iw && screen.y >= iy && screen.y < iy+ih) {\n" +
"        vec2 uv = vec2((screen.x - ix) / iw, (screen.y - iy) / ih);\n" +
"        vec4 texel = texture(uTex, uv);\n" +
"        fragColor = vec4(mix(fragColor.rgb, texel.rgb, texel.a), 1.0);\n" +
"    }\n" +
"}";

        // ── Constructor ───────────────────────────────────────────────────────

        public CanvasPanel()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.UserPaint            |
                ControlStyles.DoubleBuffer         |
                ControlStyles.ResizeRedraw, true);

            BackColor = AppColors.Canvas;
            AllowDrop = true;

            BuildGLControl();
        }

        // ── GL setup ─────────────────────────────────────────────────────────

        private void BuildGLControl()
        {
            _gl = new GLControl(new GLControlSettings
            {
                APIVersion = new Version(3, 3),
                Profile    = OpenTK.Windowing.Common.ContextProfile.Core,
            });
            _gl.Dock      = DockStyle.None;
            _gl.BackColor = AppColors.Canvas;

            _gl.Load   += OnGLLoad;
            _gl.Paint  += OnGLPaint;
            _gl.Resize += (s, e) => _gl?.Invalidate();

            // Mouse routed through private handlers so base is never called
            // (prevents wheel events bubbling up to parent controls).
            _gl.MouseDown  += (s, e) => HandleMouseDown(e);
            _gl.MouseMove  += (s, e) => HandleMouseMove(e);
            _gl.MouseUp    += (s, e) => HandleMouseUp(e);
            _gl.MouseWheel += (s, e) => HandleMouseWheel(e);

            Controls.Add(_gl);

            Resize += (s, e) => LayoutGL();
            LayoutGL();
        }

        private void LayoutGL()
        {
            // GLControl covers only the canvas area — ruler strip stays in GDI+ OnPaint.
            int rs = AppSpacing.RulerSize;
            _gl.Bounds = new Rectangle(rs, rs, Math.Max(1, Width - rs), Math.Max(1, Height - rs));
        }

        // ── GL init ───────────────────────────────────────────────────────────

        private void OnGLLoad(object sender, EventArgs e)
        {
            _gl.MakeCurrent();

            // Fullscreen quad
            float[] verts = { -1f,-1f,  1f,-1f,  -1f,1f,  1f,1f };
            _vao = GL.GenVertexArray();
            _vbo = GL.GenBuffer();
            GL.BindVertexArray(_vao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
            GL.BufferData(BufferTarget.ArrayBuffer, verts.Length * 4, verts, BufferUsageHint.StaticDraw);
            GL.EnableVertexAttribArray(0);
            GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 8, 0);

            // Compile shader
            _prog = CompileProgram(VertSrc, FragSrc);

            _glReady = true;

            // If SetBitmap was called before GL was ready, upload now
            if (_bitmap != null) UploadTexture(_bitmap);

            _gl.Invalidate();
        }

        private static int CompileProgram(string vert, string frag)
        {
            int vs = GL.CreateShader(ShaderType.VertexShader);
            GL.ShaderSource(vs, vert);
            GL.CompileShader(vs);
            GL.GetShader(vs, ShaderParameter.CompileStatus, out int ok);
            if (ok == 0) throw new Exception("Vert: " + GL.GetShaderInfoLog(vs));

            int fs = GL.CreateShader(ShaderType.FragmentShader);
            GL.ShaderSource(fs, frag);
            GL.CompileShader(fs);
            GL.GetShader(fs, ShaderParameter.CompileStatus, out ok);
            if (ok == 0) throw new Exception("Frag: " + GL.GetShaderInfoLog(fs));

            int prog = GL.CreateProgram();
            GL.AttachShader(prog, vs);
            GL.AttachShader(prog, fs);
            GL.LinkProgram(prog);
            GL.DeleteShader(vs);
            GL.DeleteShader(fs);
            return prog;
        }

        // ── Texture upload ────────────────────────────────────────────────────

        private void UploadTexture(Bitmap bmp)
        {
            if (!_glReady) return;
            _gl.MakeCurrent();

            if (_tex >= 0) GL.DeleteTexture(_tex);

            _tex  = GL.GenTexture();
            _imgW = bmp.Width;
            _imgH = bmp.Height;

            GL.BindTexture(TextureTarget.Texture2D, _tex);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

            // Lock bits and upload — BGRA matches Windows DIB layout
            var rect = new System.Drawing.Rectangle(0, 0, bmp.Width, bmp.Height);
            var data = bmp.LockBits(rect, System.Drawing.Imaging.ImageLockMode.ReadOnly,
                                    System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba,
                          bmp.Width, bmp.Height, 0,
                          OpenTK.Graphics.OpenGL4.PixelFormat.Bgra, PixelType.UnsignedByte,
                          data.Scan0);
            bmp.UnlockBits(data);
        }

        // ── GL paint ─────────────────────────────────────────────────────────

        private void OnGLPaint(object sender, PaintEventArgs e)
        {
            if (!_glReady || _prog < 0) return;
            _gl.MakeCurrent();

            int vpW = _gl.Width;
            int vpH = _gl.Height;
            GL.Viewport(0, 0, vpW, vpH);

            var bg = AppColors.Canvas;
            GL.ClearColor(bg.R / 255f, bg.G / 255f, bg.B / 255f, 1f);
            GL.Clear(ClearBufferMask.ColorBufferBit);

            GL.UseProgram(_prog);
            GL.Uniform2(GL.GetUniformLocation(_prog, "uRes"), (float)vpW, (float)vpH);
            GL.Uniform3(GL.GetUniformLocation(_prog, "uBg"),  bg.R / 255f, bg.G / 255f, bg.B / 255f);
            GL.Uniform1(GL.GetUniformLocation(_prog, "uZoom"), _zoom);

            bool hasImage = _tex >= 0 && _bitmap != null;
            GL.Uniform1(GL.GetUniformLocation(_prog, "uHasImage"), hasImage ? 1 : 0);

            if (hasImage)
            {
                var r = GetImageRect();
                // uImgRect in GL viewport coords: _gl origin is (RulerSize, RulerSize) in panel space.
                int rs = AppSpacing.RulerSize;
                GL.Uniform4(GL.GetUniformLocation(_prog, "uImgRect"),
                    r.X - rs, r.Y - rs, r.Width, r.Height);

                GL.ActiveTexture(TextureUnit.Texture0);
                GL.BindTexture(TextureTarget.Texture2D, _tex);
                GL.Uniform1(GL.GetUniformLocation(_prog, "uTex"), 0);
            }

            GL.BindVertexArray(_vao);
            GL.DrawArrays(PrimitiveType.TriangleStrip, 0, 4);

            _gl.SwapBuffers();
        }

        // ── GDI+ overlay: rulers only ─────────────────────────────────────────

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            // GLControl renders the background; only fill the ruler strip area
            // so child controls (BottomToolbar transparency) get a valid surface.
            e.Graphics.Clear(AppColors.Canvas);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            DrawRulers(e.Graphics);

            // Corner fill (ruler intersection square)
            using var cornerBrush = new SolidBrush(AppColors.RulerBg);
            e.Graphics.FillRectangle(cornerBrush, 0, 0, AppSpacing.RulerSize, AppSpacing.RulerSize);
        }

        private void DrawRulers(Graphics g)
        {
            using var rulerBrush = new SolidBrush(AppColors.RulerBg);
            using var tickPen    = new Pen(AppColors.RulerTick, 1f);
            using var textBrush  = new SolidBrush(AppColors.RulerTick);

            RectangleF imgRect = GetImageRect();

            // ── Horizontal ruler ─────────────────────────────────────────────
            g.FillRectangle(rulerBrush, AppSpacing.RulerSize, 0, Width - AppSpacing.RulerSize, AppSpacing.RulerSize);

            if (_bitmap != null)
            {
                float originScreenX = imgRect.X;
                float leftImg  = (AppSpacing.RulerSize - originScreenX) / _zoom;
                float rightImg = (Width - originScreenX) / _zoom;

                int step  = GetRulerStep();
                int start = (int)Math.Floor(leftImg  / step) * step;
                int end   = (int)Math.Ceiling(rightImg / step) * step;

                for (int px = start; px <= end; px += step / 5)
                {
                    float sx = originScreenX + px * _zoom;
                    if (sx < AppSpacing.RulerSize || sx > Width) continue;
                    bool major = px % step == 0;
                    int tickH = major ? 8 : 4;
                    g.DrawLine(tickPen, sx, AppSpacing.RulerSize - tickH, sx, AppSpacing.RulerSize);
                    if (major)
                        g.DrawString(px.ToString(), AppFonts.Small, textBrush, sx + 2, 1);
                }
            }

            // ── Vertical ruler ───────────────────────────────────────────────
            g.FillRectangle(rulerBrush, 0, AppSpacing.RulerSize, AppSpacing.RulerSize, Height - AppSpacing.RulerSize);

            if (_bitmap != null)
            {
                float originScreenY = imgRect.Y;
                float topImg    = (AppSpacing.RulerSize - originScreenY) / _zoom;
                float bottomImg = (Height - originScreenY) / _zoom;

                int step  = GetRulerStep();
                int start = (int)Math.Floor(topImg    / step) * step;
                int end   = (int)Math.Ceiling(bottomImg / step) * step;

                for (int px = start; px <= end; px += step / 5)
                {
                    float sy = originScreenY + px * _zoom;
                    if (sy < AppSpacing.RulerSize || sy > Height) continue;
                    bool major = px % step == 0;
                    int tickW = major ? 8 : 4;
                    g.DrawLine(tickPen, AppSpacing.RulerSize - tickW, sy, AppSpacing.RulerSize, sy);
                    if (major)
                        g.DrawString(px.ToString(), AppFonts.Small, textBrush, 1, sy + 2);
                }
            }

            // Ruler border line
            g.DrawLine(tickPen, AppSpacing.RulerSize, 0, AppSpacing.RulerSize, Height);
            g.DrawLine(tickPen, 0, AppSpacing.RulerSize, Width, AppSpacing.RulerSize);
        }

        private int GetRulerStep()
        {
            if (_zoom < 0.2f) return 500;
            if (_zoom < 0.5f) return 250;
            if (_zoom < 1.0f) return 100;
            if (_zoom < 2.0f) return 50;
            return 25;
        }

        // ── Coordinate helpers ────────────────────────────────────────────────

        private RectangleF GetImageRect()
        {
            if (_bitmap == null) return RectangleF.Empty;
            float cw = Width  - AppSpacing.RulerSize;
            float ch = Height - AppSpacing.RulerSize;
            float iw = _bitmap.Width  * _zoom;
            float ih = _bitmap.Height * _zoom;
            float x  = AppSpacing.RulerSize + cw / 2f - iw / 2f + _panOffset.X;
            float y  = AppSpacing.RulerSize + ch / 2f - ih / 2f + _panOffset.Y;
            return new RectangleF(x, y, iw, ih);
        }

        private PointF ScreenToImage(Point screen)
        {
            var r = GetImageRect();
            return new PointF((screen.X - r.X) / _zoom, (screen.Y - r.Y) / _zoom);
        }

        private PointF ImageToScreen(PointF image)
        {
            var r = GetImageRect();
            return new PointF(r.X + image.X * _zoom, r.Y + image.Y * _zoom);
        }

        // ── Public API ────────────────────────────────────────────────────────

        public void SetBitmap(Bitmap bmp)
        {
            _bitmap = bmp;
            if (_glReady && bmp != null) UploadTexture(bmp);
            else if (bmp == null && _glReady && _tex >= 0)
            {
                _gl.MakeCurrent();
                GL.DeleteTexture(_tex);
                _tex = -1;
            }
            ResetView();
        }

        public void ResetView()
        {
            _zoom      = 1.0f;
            _panOffset = PointF.Empty;
            _gl?.Invalidate();
            Invalidate();
        }

        public void SetEyedropperMode(bool on)
        {
            _isEyedropper = on;
            Cursor = on ? Cursors.Cross : Cursors.Default;
        }

        // ── Mouse ─────────────────────────────────────────────────────────────

        // Absorb wheel on the panel itself (GLControl forwards via private handler)
        protected override void OnMouseWheel(MouseEventArgs e) { /* absorbed */ }

        private void HandleMouseDown(MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;

            if (_isEyedropper && _bitmap != null)
            {
                PointF imgPos = ScreenToImage(e.Location);
                int ix = (int)imgPos.X, iy = (int)imgPos.Y;
                if (ix >= 0 && ix < _bitmap.Width && iy >= 0 && iy < _bitmap.Height)
                    PixelPicked?.Invoke(this, _bitmap.GetPixel(ix, iy));
                return;
            }

            _isPanning = true;
            _lastMouse = e.Location;
            _gl.Cursor = Cursors.SizeAll;
        }

        private void HandleMouseMove(MouseEventArgs e)
        {
            if (!_isPanning) return;
            _panOffset.X += e.X - _lastMouse.X;
            _panOffset.Y += e.Y - _lastMouse.Y;
            _lastMouse = e.Location;
            _gl.Invalidate();
            Invalidate();   // redraw rulers
        }

        private void HandleMouseUp(MouseEventArgs e)
        {
            _isPanning = false;
            _gl.Cursor = _isEyedropper ? Cursors.Cross : Cursors.Default;
        }

        private void HandleMouseWheel(MouseEventArgs e)
        {
            if (SuppressMouseWheel || _bitmap == null) return;

            PointF imgPos = ScreenToImage(e.Location);
            float delta = e.Delta > 0 ? 1.15f : 1f / 1.15f;
            _zoom = Math.Max(0.05f, Math.Min(20f, _zoom * delta));

            PointF newScreen = ImageToScreen(imgPos);
            _panOffset.X += e.Location.X - newScreen.X;
            _panOffset.Y += e.Location.Y - newScreen.Y;

            _gl.Invalidate();
            Invalidate();   // redraw rulers
        }

        // ── Drag & drop ───────────────────────────────────────────────────────

        protected override void OnDragEnter(DragEventArgs e)
        {
            base.OnDragEnter(e);
            e.Effect = e.Data != null && e.Data.GetDataPresent(DataFormats.FileDrop)
                ? DragDropEffects.Copy : DragDropEffects.None;
        }

        protected override void OnDragDrop(DragEventArgs e)
        {
            base.OnDragDrop(e);
            if (e.Data == null) return;
            var files = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (files != null && files.Length > 0)
                FileDropped?.Invoke(this, files[0]);
        }

        // ── Theme ─────────────────────────────────────────────────────────────

        public void ApplyTheme()
        {
            BackColor = AppColors.Canvas;
            _gl?.Invalidate();
            Invalidate();
        }

        // ── Dispose ───────────────────────────────────────────────────────────

        protected override void Dispose(bool disposing)
        {
            if (disposing && _glReady)
            {
                _gl.MakeCurrent();
                GL.DeleteVertexArray(_vao);
                GL.DeleteBuffer(_vbo);
                if (_prog >= 0)  GL.DeleteProgram(_prog);
                if (_tex  >= 0)  GL.DeleteTexture(_tex);
            }
            base.Dispose(disposing);
        }
    }
}
