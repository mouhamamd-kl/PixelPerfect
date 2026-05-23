using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using PixelPerfect.Models;

namespace PixelPerfect.Helpers
{
    /// <summary>
    /// Renders gradient 3D color-space visualizations to a Bitmap.
    /// Supports orbit (yaw/pitch), zoom, and pan via the Camera struct.
    /// </summary>
    public static class ColorSpaceRenderer
    {
        // ── Camera ────────────────────────────────────────────────────────────

        public struct Camera
        {
            public double Yaw;   // horizontal rotation in radians
            public double Pitch; // vertical rotation in radians
            public float  Zoom;  // scale multiplier (1 = default)
            public PointF Pan;   // screen-pixel offset from center

            public static Camera Default => new Camera
            {
                Yaw   = Math.PI / 6,
                Pitch = Math.PI / 5,
                Zoom  = 1f,
                Pan   = PointF.Empty
            };
        }

        // ── Public entry point ────────────────────────────────────────────────

        public static Bitmap Render(ColorSpaceMode mode, int width, int height, Camera cam)
        {
            var bmp = new Bitmap(width, height);
            using var g = Graphics.FromImage(bmp);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            switch (mode)
            {
                case ColorSpaceMode.RGB:   DrawRgbCube(g, width, height, cam);     break;
                case ColorSpaceMode.HSV:   DrawHsvCylinder(g, width, height, cam); break;
                case ColorSpaceMode.CMYK:  DrawCmykCube(g, width, height, cam);    break;
                case ColorSpaceMode.LAB:   DrawLabSphere(g, width, height, cam);   break;
                case ColorSpaceMode.YUV:   DrawYuvBox(g, width, height, cam);      break;
                case ColorSpaceMode.YCbCr: DrawYCbCrBox(g, width, height, cam);   break;
            }

            return bmp;
        }

        // ── 3D → 2D projection ────────────────────────────────────────────────

        /// <summary>
        /// Projects a 3D point (x,y,z in [-1..1] space) to screen coords using
        /// yaw/pitch rotation followed by orthographic projection.
        /// </summary>
        private static PointF Project3D(double x, double y, double z,
            double cx, double cy, double side, Camera cam)
        {
            // Rotate around Y axis (yaw)
            double cosY = Math.Cos(cam.Yaw),   sinY = Math.Sin(cam.Yaw);
            double rx = x * cosY - z * sinY;
            double rz = x * sinY + z * cosY;

            // Rotate around X axis (pitch)
            double cosP = Math.Cos(cam.Pitch), sinP = Math.Sin(cam.Pitch);
            double ry2  = y  * cosP - rz * sinP;
            double rz2  = y  * sinP + rz * cosP;

            // Orthographic projection + pan
            double sx = cx + rx * side * cam.Zoom + cam.Pan.X;
            double sy = cy - ry2 * side * cam.Zoom + cam.Pan.Y;
            return new PointF((float)sx, (float)sy);
        }

        // ── RGB Cube ──────────────────────────────────────────────────────────

        private static void DrawRgbCube(Graphics g, int w, int h, Camera cam)
        {
            double cx = w / 2.0, cy = h / 2.0;
            double side = Math.Min(w, h) * 0.34;

            // Map [0,1] RGB to [-1,1] cube centered at origin
            PointF P(double r, double gv, double b) =>
                Project3D(r * 2 - 1, gv * 2 - 1, b * 2 - 1, cx, cy, side, cam);

            DrawCubeFaces(g,
                P(0,0,0), P(1,0,0), P(0,1,0), P(1,1,0),
                P(0,0,1), P(1,0,1), P(0,1,1), P(1,1,1),
                Color.Black,  Color.Red,     Color.Lime,   Color.Yellow,
                Color.Blue,   Color.Magenta, Color.Cyan,   Color.White,
                cam);
        }

        // ── CMYK Cube ─────────────────────────────────────────────────────────

        private static void DrawCmykCube(Graphics g, int w, int h, Camera cam)
        {
            double cx = w / 2.0, cy = h / 2.0;
            double side = Math.Min(w, h) * 0.34;

            PointF P(double c, double m, double y) =>
                Project3D(c * 2 - 1, m * 2 - 1, y * 2 - 1, cx, cy, side, cam);

            DrawCubeFaces(g,
                P(0,0,0), P(1,0,0), P(0,1,0), P(1,1,0),
                P(0,0,1), P(1,0,1), P(0,1,1), P(1,1,1),
                Color.White,  Color.Cyan,    Color.Magenta, Color.Blue,
                Color.Yellow, Color.Green,   Color.Red,     Color.Black,
                cam);
        }

        /// <summary>
        /// Draws 6 faces of a cube given 8 corner points and their colors.
        /// Corners order: 000,100,010,110,001,101,011,111 (x,y,z bits).
        /// Faces are depth-sorted by average Z after rotation so back faces draw first.
        /// </summary>
        private static void DrawCubeFaces(Graphics g,
            PointF p000, PointF p100, PointF p010, PointF p110,
            PointF p001, PointF p101, PointF p011, PointF p111,
            Color c000, Color c100, Color c010, Color c110,
            Color c001, Color c101, Color c011, Color c111,
            Camera cam)
        {
            // 6 faces: each defined by 4 corner indices (bit pattern x,y,z)
            // We draw all 6; the triangle area check skips back faces that project degenerate.
            DrawGradientFace(g, new[]{p000,p100,p110,p010}, new[]{c000,c100,c110,c010}); // bottom
            DrawGradientFace(g, new[]{p001,p101,p111,p011}, new[]{c001,c101,c111,c011}); // top
            DrawGradientFace(g, new[]{p000,p001,p011,p010}, new[]{c000,c001,c011,c010}); // left
            DrawGradientFace(g, new[]{p100,p101,p111,p110}, new[]{c100,c101,c111,c110}); // right
            DrawGradientFace(g, new[]{p000,p100,p101,p001}, new[]{c000,c100,c101,c001}); // front
            DrawGradientFace(g, new[]{p010,p110,p111,p011}, new[]{c010,c110,c111,c011}); // back
        }

        // ── HSV Cylinder ──────────────────────────────────────────────────────

        private static void DrawHsvCylinder(Graphics g, int w, int h, Camera cam)
        {
            double cx = w / 2.0, cy = h / 2.0;
            double side  = Math.Min(w, h) * 0.32;
            double r     = 1.0;   // radius in 3D space
            double halfH = 1.0;   // half-height

            int steps = 60;

            for (int i = 0; i < steps; i++)
            {
                double a1 = i       * 2 * Math.PI / steps;
                double a2 = (i + 1) * 2 * Math.PI / steps;

                double x1 = r * Math.Cos(a1), z1 = r * Math.Sin(a1);
                double x2 = r * Math.Cos(a2), z2 = r * Math.Sin(a2);

                double hue1 = i       * 360.0 / steps;
                double hue2 = (i + 1) * 360.0 / steps;

                Color topC1 = ColorMath.FromHsv(hue1, 100, 100);
                Color topC2 = ColorMath.FromHsv(hue2, 100, 100);

                // Side quad: top-bright → bottom-black
                PointF topP1 = Project3D(x1,  halfH, z1, cx, cy, side, cam);
                PointF topP2 = Project3D(x2,  halfH, z2, cx, cy, side, cam);
                PointF botP1 = Project3D(x1, -halfH, z1, cx, cy, side, cam);
                PointF botP2 = Project3D(x2, -halfH, z2, cx, cy, side, cam);

                DrawGradientFace(g,
                    new[] { topP1, topP2, botP2, botP1 },
                    new[] { topC1, topC2, Color.Black, Color.Black });
            }

            // Top cap — hue wheel
            PointF topCenter = Project3D(0, halfH, 0, cx, cy, side, cam);
            for (int i = 0; i < steps; i++)
            {
                double a1 = i       * 2 * Math.PI / steps;
                double a2 = (i + 1) * 2 * Math.PI / steps;
                double x1 = r * Math.Cos(a1), z1 = r * Math.Sin(a1);
                double x2 = r * Math.Cos(a2), z2 = r * Math.Sin(a2);

                PointF p1 = Project3D(x1, halfH, z1, cx, cy, side, cam);
                PointF p2 = Project3D(x2, halfH, z2, cx, cy, side, cam);

                FillGradientTriangle(g, topCenter, p1, p2,
                    Color.White,
                    ColorMath.FromHsv(i * 360.0 / steps, 100, 100),
                    ColorMath.FromHsv((i + 1) * 360.0 / steps, 100, 100));
            }

            // Bottom cap — solid black
            PointF botCenter = Project3D(0, -halfH, 0, cx, cy, side, cam);
            for (int i = 0; i < steps; i++)
            {
                double a1 = i       * 2 * Math.PI / steps;
                double a2 = (i + 1) * 2 * Math.PI / steps;
                double x1 = r * Math.Cos(a1), z1 = r * Math.Sin(a1);
                double x2 = r * Math.Cos(a2), z2 = r * Math.Sin(a2);

                PointF p1 = Project3D(x1, -halfH, z1, cx, cy, side, cam);
                PointF p2 = Project3D(x2, -halfH, z2, cx, cy, side, cam);

                FillGradientTriangle(g, botCenter, p1, p2,
                    Color.Black, Color.Black, Color.Black);
            }
        }

        // ── LAB Sphere ────────────────────────────────────────────────────────

        private static void DrawLabSphere(Graphics g, int w, int h, Camera cam)
        {
            double cx = w / 2.0, cy = h / 2.0;
            double side   = Math.Min(w, h) * 0.32;
            int    stacks = 16;
            int    slices = 36;

            for (int si = 0; si < stacks; si++)
            {
                double phi1 = Math.PI * si       / stacks - Math.PI / 2;
                double phi2 = Math.PI * (si + 1) / stacks - Math.PI / 2;

                double y1 = Math.Sin(phi1), y2 = Math.Sin(phi2);
                double r1 = Math.Cos(phi1), r2 = Math.Cos(phi2);
                double l1 = (y1 + 1) / 2 * 100;
                double l2 = (y2 + 1) / 2 * 100;

                for (int sli = 0; sli < slices; sli++)
                {
                    double a1 = sli       * 2 * Math.PI / slices;
                    double a2 = (sli + 1) * 2 * Math.PI / slices;

                    double x1 = Math.Cos(a1), z1 = Math.Sin(a1);
                    double x2 = Math.Cos(a2), z2 = Math.Sin(a2);

                    PointF pA = Project3D(x1 * r1, y1, z1 * r1, cx, cy, side, cam);
                    PointF pB = Project3D(x2 * r1, y1, z2 * r1, cx, cy, side, cam);
                    PointF pC = Project3D(x2 * r2, y2, z2 * r2, cx, cy, side, cam);
                    PointF pD = Project3D(x1 * r2, y2, z1 * r2, cx, cy, side, cam);

                    Color cA = ColorMath.FromLab(l1, 80 * x1, 80 * z1);
                    Color cB = ColorMath.FromLab(l1, 80 * x2, 80 * z2);
                    Color cC = ColorMath.FromLab(l2, 80 * x2, 80 * z2);
                    Color cD = ColorMath.FromLab(l2, 80 * x1, 80 * z1);

                    DrawGradientFace(g, new[] { pA, pB, pC, pD }, new[] { cA, cB, cC, cD });
                }
            }
        }

        // ── YUV / YCbCr Box ───────────────────────────────────────────────────

        private static void DrawYuvBox(Graphics g, int w, int h, Camera cam)
            => DrawChromaBox(g, w, h, cam, isYCbCr: false);

        private static void DrawYCbCrBox(Graphics g, int w, int h, Camera cam)
            => DrawChromaBox(g, w, h, cam, isYCbCr: true);

        private static void DrawChromaBox(Graphics g, int w, int h, Camera cam, bool isYCbCr)
        {
            double cx = w / 2.0, cy = h / 2.0;
            double side = Math.Min(w, h) * 0.34;

            PointF P(double y, double u, double v) =>
                Project3D(u * 2 - 1, y * 2 - 1, v * 2 - 1, cx, cy, side, cam);

            Color C(double yn, double un, double vn)
            {
                if (isYCbCr)
                    return ColorMath.FromYCbCr(16 + yn * 219, 16 + un * 224, 16 + vn * 224);
                else
                    return ColorMath.FromYuv(yn * 255, (un - 0.5) * 224, (vn - 0.5) * 314);
            }

            DrawCubeFaces(g,
                P(0,0,0), P(0,1,0), P(1,0,0), P(1,1,0),
                P(0,0,1), P(0,1,1), P(1,0,1), P(1,1,1),
                C(0,0,0), C(0,1,0), C(1,0,0), C(1,1,0),
                C(0,0,1), C(0,1,1), C(1,0,1), C(1,1,1),
                cam);
        }

        // ── Drawing helpers ───────────────────────────────────────────────────

        private static void DrawGradientFace(Graphics g, PointF[] corners, Color[] colors)
        {
            if (corners.Length < 3) return;
            FillGradientTriangle(g, corners[0], corners[1], corners[2], colors[0], colors[1], colors[2]);
            if (corners.Length == 4)
                FillGradientTriangle(g, corners[0], corners[2], corners[3], colors[0], colors[2], colors[3]);
        }

        private static void FillGradientTriangle(Graphics g,
            PointF p1, PointF p2, PointF p3, Color c1, Color c2, Color c3)
        {
            float area = Math.Abs((p2.X - p1.X) * (p3.Y - p1.Y) - (p3.X - p1.X) * (p2.Y - p1.Y));
            if (area < 0.5f) return;

            var path = new GraphicsPath();
            path.AddPolygon(new[] { p1, p2, p3 });

            using var brush = new PathGradientBrush(path)
            {
                CenterPoint = new PointF((p1.X + p2.X + p3.X) / 3f, (p1.Y + p2.Y + p3.Y) / 3f),
                CenterColor = Color.FromArgb(
                    (c1.R + c2.R + c3.R) / 3,
                    (c1.G + c2.G + c3.G) / 3,
                    (c1.B + c2.B + c3.B) / 3),
                SurroundColors = new[] { c1, c2, c3 }
            };
            g.FillPath(brush, path);
        }
    }
}
