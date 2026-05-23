using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using OpenTK.Graphics.OpenGL4;
using OpenTK.GLControl;
using PixelPerfect.Helpers;
using PixelPerfect.Models;
using PixelPerfect.UI.Atoms;

namespace PixelPerfect.UI.Organisms
{
    /// <summary>
    /// GPU-accelerated 3D color-space viewer using OpenGL fragment shaders.
    /// Each color space is ray-cast analytically in the shader — orbit/zoom/pan
    /// are uniform uploads, so interaction is lag-free.
    ///
    ///   Left drag    → orbit (yaw / pitch)
    ///   Right drag   → pan
    ///   Scroll wheel → zoom
    ///   Left click   → pick color at cursor
    /// </summary>
    public class ColorSpaceViewerPanel : UserControl, IThemeable
    {
        // ── Sub-controls ──────────────────────────────────────────────────────
        private ModeBar    _modeBar;
        private GLControl  _gl;

        // ── GL resources ──────────────────────────────────────────────────────
        private int _vao, _vbo;           // fullscreen quad
        private int _prog = -1;           // current shader program
        private int[] _programs;          // one program per ColorSpaceMode
        private bool _glReady;

        // ── State ─────────────────────────────────────────────────────────────
        private ColorSpaceMode _activeMode = ColorSpaceMode.RGB;
        private ColorSettings _colorSettings = new ColorSettings();
        private double _yaw   = 0.6;
        private double _pitch = 0.4;
        private float  _zoom  = 1f;
        private float  _panX, _panY;

        // ── Mouse ─────────────────────────────────────────────────────────────
        private Point _lastMouse;
        private bool  _isOrbiting, _isPanning, _mouseHasMoved;

        // ── Events ────────────────────────────────────────────────────────────
        public event EventHandler<Color> ColorPicked;
        public event EventHandler<ColorSpaceMode> ModeChanged;

        // ── GLSL shaders ──────────────────────────────────────────────────────

        private const string VertSrc = @"#version 330 core
layout(location=0) in vec2 aPos;
out vec2 uv;
void main() { uv = aPos * 0.5 + 0.5; gl_Position = vec4(aPos,0,1); }";

        // Common uniforms and rotation helpers shared by all shaders
        private const string FragHeader = @"#version 330 core
in  vec2 uv;
out vec4 fragColor;
uniform float uYaw;
uniform float uPitch;
uniform float uZoom;
uniform vec2  uPan;
uniform vec2  uRes;
uniform vec3  uBg;
uniform int   uColorSpace;
uniform vec4  uAdjust;

vec3 rotateYP(vec3 d) {
    float cy = cos(uYaw),  sy = sin(uYaw);
    float cp = cos(uPitch), sp = sin(uPitch);
    float x2 = d.x*cy - d.z*sy;
    float z2 = d.x*sy + d.z*cy;
    float y2 = d.y*cp - z2*sp;
    float z3 = d.y*sp + z2*cp;
    return vec3(x2, y2, z3);
}

vec3 getRayDir() {
    float aspect = uRes.x / uRes.y;
    vec2  ndc    = (uv - 0.5) * 2.0;
    ndc.x       *= aspect;
    ndc          /= uZoom;
    ndc          -= uPan / uRes * 2.0;
    return normalize(rotateYP(vec3(ndc, 2.0)));
}
vec3 getRayOri() {
    return rotateYP(vec3(0,0,-3.0));
}

vec3 rgbToHsv01(vec3 c) {
    vec4 K = vec4(0.0, -1.0/3.0, 2.0/3.0, -1.0);
    vec4 p = mix(vec4(c.bg, K.wz), vec4(c.gb, K.xy), step(c.b, c.g));
    vec4 q = mix(vec4(p.xyw, c.r), vec4(c.r, p.yzx), step(p.x, c.r));
    float d = q.x - min(q.w, q.y);
    float e = 1.0e-10;
    return vec3(abs(q.z + (q.w - q.y) / (6.0 * d + e)), d / (q.x + e), q.x);
}

vec3 hsv01ToRgb(vec3 c) {
    vec3 p = abs(fract(c.xxx + vec3(0.0, 2.0/3.0, 1.0/3.0)) * 6.0 - 3.0);
    return c.z * mix(vec3(1.0), clamp(p - 1.0, 0.0, 1.0), c.y);
}

vec4 rgbToCmyk01(vec3 rgb) {
    float k = 1.0 - max(rgb.r, max(rgb.g, rgb.b));
    if (k >= 0.9999) return vec4(0.0, 0.0, 0.0, 1.0);
    float d = 1.0 - k;
    return vec4((1.0 - rgb.r - k) / d,
                (1.0 - rgb.g - k) / d,
                (1.0 - rgb.b - k) / d,
                k);
}

vec3 cmyk01ToRgb(vec4 cmyk) {
    float c = cmyk.x, m = cmyk.y, y = cmyk.z, k = cmyk.w;
    return clamp(vec3((1.0 - c) * (1.0 - k),
                      (1.0 - m) * (1.0 - k),
                      (1.0 - y) * (1.0 - k)), 0.0, 1.0);
}

float gammaExpand(float v) {
    return v > 0.04045 ? pow((v + 0.055) / 1.055, 2.4) : v / 12.92;
}

float gammaCompress(float v) {
    return v > 0.0031308 ? 1.055 * pow(v, 1.0 / 2.4) - 0.055 : 12.92 * v;
}

float labF(float t) {
    return t > 0.008856 ? pow(t, 1.0 / 3.0) : (7.787 * t + 16.0 / 116.0);
}

float labFInv(float t) {
    return t > 0.2069 ? (t * t * t) : ((t - 16.0 / 116.0) / 7.787);
}

vec3 rgbToLab(vec3 rgb) {
    float rd = gammaExpand(rgb.r);
    float gd = gammaExpand(rgb.g);
    float bd = gammaExpand(rgb.b);

    float x = rd * 41.24 + gd * 35.76 + bd * 18.05;
    float y = rd * 21.26 + gd * 71.52 + bd * 7.22;
    float z = rd * 1.93  + gd * 11.92 + bd * 95.05;

    x = labF(x / 95.047);
    y = labF(y / 100.0);
    z = labF(z / 108.883);

    float L = 116.0 * y - 16.0;
    float A = 500.0 * (x - y);
    float B = 200.0 * (y - z);
    return vec3(L, A, B);
}

vec3 labToRgb(vec3 lab) {
    float fy = (lab.x + 16.0) / 116.0;
    float fx = lab.y / 500.0 + fy;
    float fz = fy - lab.z / 200.0;

    float x = labFInv(fx) * 95.047;
    float y = labFInv(fy) * 100.0;
    float z = labFInv(fz) * 108.883;

    x /= 100.0; y /= 100.0; z /= 100.0;

    float r =  x * 3.2406 - y * 1.5372 - z * 0.4986;
    float g = -x * 0.9689 + y * 1.8758 + z * 0.0415;
    float b =  x * 0.0557 - y * 0.2040 + z * 1.0570;

    r = gammaCompress(r);
    g = gammaCompress(g);
    b = gammaCompress(b);

    return clamp(vec3(r, g, b), 0.0, 1.0);
}

vec3 rgbToYuv(vec3 rgb) {
    float r = rgb.r * 255.0, g = rgb.g * 255.0, b = rgb.b * 255.0;
    float y =  0.29900 * r + 0.58700 * g + 0.11400 * b;
    float u = -0.14713 * r - 0.28886 * g + 0.43600 * b;
    float v =  0.61500 * r - 0.51499 * g - 0.10001 * b;
    return vec3(y, u, v);
}

vec3 yuvToRgb(vec3 yuv) {
    float y = yuv.x, u = yuv.y, v = yuv.z;
    float r = y + 1.13983 * v;
    float g = y - 0.39465 * u - 0.58060 * v;
    float b = y + 2.03211 * u;
    return clamp(vec3(r, g, b) / 255.0, 0.0, 1.0);
}

vec3 rgbToYCbCr(vec3 rgb) {
    float r = rgb.r * 255.0, g = rgb.g * 255.0, b = rgb.b * 255.0;
    float y  = 16.0  + 0.257 * r + 0.504 * g + 0.098 * b;
    float cb = 128.0 - 0.148 * r - 0.291 * g + 0.439 * b;
    float cr = 128.0 + 0.439 * r - 0.368 * g - 0.071 * b;
    return vec3(y, cb, cr);
}

vec3 ycbcrToRgb(vec3 ycc) {
    float c = ycc.x - 16.0, d = ycc.y - 128.0, e = ycc.z - 128.0;
    float r = 1.164 * c + 1.596 * e;
    float g = 1.164 * c - 0.392 * d - 0.813 * e;
    float b = 1.164 * c + 2.017 * d;
    return clamp(vec3(r, g, b) / 255.0, 0.0, 1.0);
}

vec3 applyColorAdjust(vec3 rgb) {
    vec4 a = uAdjust;

    if (uColorSpace == 0) { // RGB
        return clamp(rgb + vec3(a.x, a.y, a.z) / 255.0, 0.0, 1.0);
    }
    if (uColorSpace == 1) { // HSV
        vec3 hsv = rgbToHsv01(rgb);
        hsv.x = fract(hsv.x + a.x / 360.0);
        hsv.y = clamp(hsv.y + a.y / 100.0, 0.0, 1.0);
        hsv.z = clamp(hsv.z + a.z / 100.0, 0.0, 1.0);
        return clamp(hsv01ToRgb(hsv), 0.0, 1.0);
    }
    if (uColorSpace == 2) { // CMYK
        vec4 cmyk = rgbToCmyk01(rgb);
        cmyk = clamp(cmyk + vec4(a.x, a.y, a.z, a.w) / 100.0, 0.0, 1.0);
        return clamp(cmyk01ToRgb(cmyk), 0.0, 1.0);
    }
    if (uColorSpace == 3) { // LAB
        vec3 lab = rgbToLab(rgb);
        lab += vec3(a.x, a.y, a.z);
        return clamp(labToRgb(lab), 0.0, 1.0);
    }
    if (uColorSpace == 4) { // YUV
        vec3 yuv = rgbToYuv(rgb);
        yuv += vec3(a.x, a.y, a.z);
        return clamp(yuvToRgb(yuv), 0.0, 1.0);
    }
    if (uColorSpace == 5) { // YCbCr
        vec3 ycc = rgbToYCbCr(rgb);
        ycc += vec3(a.x, a.y, a.z);
        return clamp(ycbcrToRgb(ycc), 0.0, 1.0);
    }

    return clamp(rgb, 0.0, 1.0);
}
";

        // ── RGB cube shader ───────────────────────────────────────────────────
        private const string FragRgb = FragHeader + @"
// Ray-AABB intersection for unit cube [-1,1]^3
bool intersectCube(vec3 ro, vec3 rd, out float tNear, out vec3 norm, out vec3 hit) {
    vec3 tMin = (-1.0 - ro) / rd;
    vec3 tMax = ( 1.0 - ro) / rd;
    vec3 t1   = min(tMin, tMax);
    vec3 t2   = max(tMin, tMax);
    tNear     = max(max(t1.x, t1.y), t1.z);
    float tF  = min(min(t2.x, t2.y), t2.z);
    if (tNear > tF || tF < 0.0) return false;
    if (tNear < 0.0) tNear = tF;
    hit  = ro + tNear * rd;
    vec3 absHit = abs(hit);
    if      (absHit.x >= absHit.y && absHit.x >= absHit.z) norm = vec3(sign(hit.x),0,0);
    else if (absHit.y >= absHit.x && absHit.y >= absHit.z) norm = vec3(0,sign(hit.y),0);
    else                                                    norm = vec3(0,0,sign(hit.z));
    return true;
}
void main() {
    vec3 ro = getRayOri(), rd = getRayDir();
    float t; vec3 norm, hit;
    if (!intersectCube(ro, rd, t, norm, hit)) { fragColor = vec4(uBg, 1.0); return; }
    vec3 col = (hit + 1.0) * 0.5;  // map [-1,1] -> [0,1] = RGB
    float shade = dot(norm, normalize(vec3(1,2,1.5))) * 0.15 + 0.85;
    fragColor = vec4(applyColorAdjust(col * shade), 1.0);
}";

        // ── HSV cylinder shader ───────────────────────────────────────────────
        private const string FragHsv = FragHeader + @"
vec3 hsv2rgb(float h, float s, float v) {
    vec3 c = vec3(h*6.0, s, v);
    vec3 rgb = clamp(abs(mod(c.x+vec3(0,4,2),6.0)-3.0)-1.0, 0.0, 1.0);
    return c.z * mix(vec3(1.0), rgb, c.y);
}
void main() {
    vec3 ro = getRayOri(), rd = getRayDir();
    // Cylinder x^2+z^2=1, y in [-1,1]
    float a = rd.x*rd.x + rd.z*rd.z;
    if (a < 1e-6) { fragColor = vec4(uBg, 1.0); return; }
    float b = ro.x*rd.x + ro.z*rd.z;
    float c = ro.x*ro.x + ro.z*ro.z - 1.0;
    float disc = b*b - a*c;
    if (disc < 0.0) { fragColor = vec4(uBg, 1.0); return; }
    float sq = sqrt(disc);
    float tBest = 1e9; vec3 col = vec3(0); bool hit = false;
    // Side
    for (int s = -1; s <= 1; s += 2) {
        float t = (-b + float(s)*sq) / a;
        if (t < 0.001 || t >= tBest) continue;
        float y = ro.y + t*rd.y;
        if (y < -1.0 || y > 1.0) continue;
        float hx = ro.x+t*rd.x, hz = ro.z+t*rd.z;
        float hue = atan(hz, hx) / 6.2832 + 0.5;
        float val = (y + 1.0) * 0.5;
        tBest = t; hit = true;
        col = hsv2rgb(hue, 1.0, val);
    }
    // Top cap y=1
    if (abs(rd.y) > 1e-6) {
        float t = (1.0 - ro.y) / rd.y;
        if (t > 0.001 && t < tBest) {
            float hx=ro.x+t*rd.x, hz=ro.z+t*rd.z;
            if (hx*hx+hz*hz <= 1.0) {
                float hue = atan(hz,hx)/6.2832+0.5;
                float sat  = sqrt(hx*hx+hz*hz);
                tBest=t; hit=true; col=hsv2rgb(hue,sat,1.0);
            }
        }
        // Bottom cap y=-1
        t = (-1.0 - ro.y) / rd.y;
        if (t > 0.001 && t < tBest) {
            float hx=ro.x+t*rd.x, hz=ro.z+t*rd.z;
            if (hx*hx+hz*hz <= 1.0) { tBest=t; hit=true; col=vec3(0); }
        }
    }
    if (!hit) { fragColor=vec4(uBg,1.0); return; }
    fragColor = vec4(applyColorAdjust(col), 1.0);
}";

        // ── LAB sphere shader ─────────────────────────────────────────────────
        private const string FragLab = FragHeader + @"
vec3 lab2rgb(float L, float a, float b) {
    float fy = (L+16.0)/116.0, fx = a/500.0+fy, fz = fy-b/200.0;
    float x = (fx>0.2069 ? fx*fx*fx : (fx-16.0/116.0)/7.787) * 95.047;
    float y = (fy>0.2069 ? fy*fy*fy : (fy-16.0/116.0)/7.787) * 100.0;
    float z = (fz>0.2069 ? fz*fz*fz : (fz-16.0/116.0)/7.787) * 108.883;
    x/=100.0; y/=100.0; z/=100.0;
    float r =  x*3.2406 - y*1.5372 - z*0.4986;
    float g = -x*0.9689 + y*1.8758 + z*0.0415;
    float bv=  x*0.0557 - y*0.2040 + z*1.0570;
    r = r>0.0031308 ? 1.055*pow(r,1.0/2.4)-0.055 : 12.92*r;
    g = g>0.0031308 ? 1.055*pow(g,1.0/2.4)-0.055 : 12.92*g;
    bv= bv>0.0031308? 1.055*pow(bv,1.0/2.4)-0.055: 12.92*bv;
    return clamp(vec3(r,g,bv),0.0,1.0);
}
void main() {
    vec3 ro = getRayOri(), rd = getRayDir();
    float b2 = dot(ro,rd), c = dot(ro,ro)-1.0;
    float disc = b2*b2 - c;
    if (disc < 0.0) { fragColor=vec4(uBg,1.0); return; }
    float t = -b2 - sqrt(disc);
    if (t < 0.001) t = -b2 + sqrt(disc);
    if (t < 0.001) { fragColor=vec4(uBg,1.0); return; }
    vec3 h = ro + t*rd;
    float L   = (h.y + 1.0) * 50.0;
    float r2  = length(h.xz);
    float aV  = r2 * 80.0 * h.x / max(r2,0.001);
    float bV  = r2 * 80.0 * h.z / max(r2,0.001);
    fragColor = vec4(applyColorAdjust(lab2rgb(L, aV, bV)), 1.0);
}";

        // ── CMYK cube shader ──────────────────────────────────────────────────
        private const string FragCmyk = FragHeader + @"
bool intersectCube(vec3 ro, vec3 rd, out float tNear, out vec3 norm, out vec3 hit) {
    vec3 tMin = (-1.0-ro)/rd, tMax = (1.0-ro)/rd;
    vec3 t1=min(tMin,tMax), t2=max(tMin,tMax);
    tNear = max(max(t1.x,t1.y),t1.z);
    float tF = min(min(t2.x,t2.y),t2.z);
    if (tNear>tF||tF<0.0) return false;
    if (tNear<0.0) tNear=tF;
    hit = ro+tNear*rd;
    vec3 absH=abs(hit);
    if(absH.x>=absH.y&&absH.x>=absH.z) norm=vec3(sign(hit.x),0,0);
    else if(absH.y>=absH.x&&absH.y>=absH.z) norm=vec3(0,sign(hit.y),0);
    else norm=vec3(0,0,sign(hit.z));
    return true;
}
void main() {
    vec3 ro=getRayOri(), rd=getRayDir();
    float t; vec3 norm, hit;
    if (!intersectCube(ro,rd,t,norm,hit)) { fragColor=vec4(uBg,1.0); return; }
    vec3 cmy = (hit+1.0)*0.5;  // C,M,Y in [0,1], K=0
    float c=cmy.x, m=cmy.y, y=cmy.z;
    vec3 rgb = vec3((1.0-c),(1.0-m),(1.0-y));
    float shade = dot(norm,normalize(vec3(1,2,1.5)))*0.15+0.85;
    fragColor = vec4(applyColorAdjust(rgb*shade), 1.0);
}";

        // ── YUV box shader ────────────────────────────────────────────────────
        private const string FragYuv = FragHeader + @"
bool intersectCube(vec3 ro, vec3 rd, out float tNear, out vec3 norm, out vec3 hit) {
    vec3 tMin=(-1.0-ro)/rd, tMax=(1.0-ro)/rd;
    vec3 t1=min(tMin,tMax), t2=max(tMin,tMax);
    tNear=max(max(t1.x,t1.y),t1.z);
    float tF=min(min(t2.x,t2.y),t2.z);
    if(tNear>tF||tF<0.0) return false;
    if(tNear<0.0) tNear=tF;
    hit=ro+tNear*rd;
    vec3 absH=abs(hit);
    if(absH.x>=absH.y&&absH.x>=absH.z) norm=vec3(sign(hit.x),0,0);
    else if(absH.y>=absH.x&&absH.y>=absH.z) norm=vec3(0,sign(hit.y),0);
    else norm=vec3(0,0,sign(hit.z));
    return true;
}
void main() {
    vec3 ro=getRayOri(), rd=getRayDir();
    float t; vec3 norm, hit;
    if (!intersectCube(ro,rd,t,norm,hit)) { fragColor=vec4(uBg,1.0); return; }
    float Y = (hit.y+1.0)*0.5*255.0;
    float U = (hit.x+1.0)*0.5*224.0 - 112.0;
    float V = (hit.z+1.0)*0.5*314.0 - 157.0;
    float r = clamp(Y + 1.13983*V, 0.0, 255.0)/255.0;
    float g = clamp(Y - 0.39465*U - 0.58060*V, 0.0, 255.0)/255.0;
    float b = clamp(Y + 2.03211*U, 0.0, 255.0)/255.0;
    float shade=dot(norm,normalize(vec3(1,2,1.5)))*0.15+0.85;
    fragColor = vec4(applyColorAdjust(vec3(r,g,b)*shade), 1.0);
}";

        // ── YCbCr box shader ──────────────────────────────────────────────────
        private const string FragYCbCr = FragHeader + @"
bool intersectCube(vec3 ro, vec3 rd, out float tNear, out vec3 norm, out vec3 hit) {
    vec3 tMin=(-1.0-ro)/rd, tMax=(1.0-ro)/rd;
    vec3 t1=min(tMin,tMax), t2=max(tMin,tMax);
    tNear=max(max(t1.x,t1.y),t1.z);
    float tF=min(min(t2.x,t2.y),t2.z);
    if(tNear>tF||tF<0.0) return false;
    if(tNear<0.0) tNear=tF;
    hit=ro+tNear*rd;
    vec3 absH=abs(hit);
    if(absH.x>=absH.y&&absH.x>=absH.z) norm=vec3(sign(hit.x),0,0);
    else if(absH.y>=absH.x&&absH.y>=absH.z) norm=vec3(0,sign(hit.y),0);
    else norm=vec3(0,0,sign(hit.z));
    return true;
}
void main() {
    vec3 ro=getRayOri(), rd=getRayDir();
    float t; vec3 norm, hit;
    if (!intersectCube(ro,rd,t,norm,hit)) { fragColor=vec4(uBg,1.0); return; }
    float Y  = 16.0  + (hit.y+1.0)*0.5*219.0;
    float Cb = 16.0  + (hit.x+1.0)*0.5*224.0;
    float Cr = 16.0  + (hit.z+1.0)*0.5*224.0;
    float c2=Y-16.0, d=Cb-128.0, e=Cr-128.0;
    float r=clamp(1.164*c2+1.596*e,       0.0,255.0)/255.0;
    float g=clamp(1.164*c2-0.392*d-0.813*e,0.0,255.0)/255.0;
    float b=clamp(1.164*c2+2.017*d,        0.0,255.0)/255.0;
    float shade=dot(norm,normalize(vec3(1,2,1.5)))*0.15+0.85;
    fragColor = vec4(applyColorAdjust(vec3(r,g,b)*shade), 1.0);
}";

        // ── Constructor ───────────────────────────────────────────────────────

        public ColorSpaceViewerPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.DoubleBuffer |
                     ControlStyles.ResizeRedraw, true);
            BackColor = AppColors.Canvas;

            BuildModeBar();
            BuildGLControl();
        }

        // ── Mode bar ──────────────────────────────────────────────────────────

        private void BuildModeBar()
        {
            _modeBar = new ModeBar(_activeMode);
            _modeBar.ModeSelected += (s, mode) =>
            {
                _activeMode = mode;
                _colorSettings.ActiveColorSpace = mode;
                _yaw = 0.6; _pitch = 0.4; _zoom = 1f; _panX = _panY = 0;
                UpdateShader();
                _gl?.Invalidate();
                ModeChanged?.Invoke(this, mode);
            };
            Controls.Add(_modeBar);
        }

        // ── GL control setup ──────────────────────────────────────────────────

        private void BuildGLControl()
        {
            _gl = new GLControl(new GLControlSettings
            {
                APIVersion = new Version(3, 3),
                Profile    = OpenTK.Windowing.Common.ContextProfile.Core,
            });
            _gl.Dock = DockStyle.None;
            _gl.BackColor = AppColors.Canvas;

            _gl.Load      += OnGLLoad;
            _gl.Paint     += OnGLPaint;
            _gl.Resize    += (s, e) => { _gl.Invalidate(); };

            // Forward mouse events from GLControl to our private handlers.
            // We use private methods (not the overrides) so base.OnMouseXxx is NOT
            // called — that would re-raise the events on ColorSpaceViewerPanel and
            // allow them to bubble up to CanvasPanel, causing unwanted pan/zoom there.
            _gl.MouseDown  += (s, e) => HandleMouseDown(e);
            _gl.MouseMove  += (s, e) => HandleMouseMove(e);
            _gl.MouseUp    += (s, e) => HandleMouseUp(e);
            _gl.MouseWheel += (s, e) => HandleMouseWheel(e);

            Controls.Add(_gl);
            Resize += (s, e) => LayoutGL();
            LayoutGL();
        }

        // Reserve pixels at the bottom so the floating BottomToolbar (which is also
        // a child of CanvasPanel) is not covered by the native GLControl HWND.
        public int BottomReserve { get; set; } = 0;

        private void LayoutGL()
        {
            // Keep GL full-height and float the mode selector over it.
            int glH = Math.Max(1, Height - BottomReserve);
            _gl.Bounds      = new Rectangle(0, 0, Width, glH);
            _modeBar.Bounds = new Rectangle(0, 10, Width, 56);
            _modeBar.BringToFront();
        }

        // ── GL init ───────────────────────────────────────────────────────────

        private void OnGLLoad(object sender, EventArgs e)
        {
            _gl.MakeCurrent();

            // Fullscreen quad (-1..1)
            float[] verts = { -1,-1,  1,-1,  -1,1,  1,1 };
            _vao = GL.GenVertexArray();
            _vbo = GL.GenBuffer();
            GL.BindVertexArray(_vao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
            GL.BufferData(BufferTarget.ArrayBuffer, verts.Length * 4, verts, BufferUsageHint.StaticDraw);
            GL.EnableVertexAttribArray(0);
            GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 8, 0);

            // Compile one program per mode
            string[] names = { "RGB", "HSV", "CMYK", "LAB", "YUV", "YCbCr" };
            string[] frags = { FragRgb, FragHsv, FragCmyk, FragLab, FragYuv, FragYCbCr };
            _programs = new int[frags.Length];
            for (int i = 0; i < frags.Length; i++)
            {
                try { _programs[i] = CompileProgram(VertSrc, frags[i]); }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Shader [{names[i]}] failed: {ex.Message}");
                    _programs[i] = -1;
                }
            }

            _glReady = true;
            UpdateShader();
        }

        private void UpdateShader()
        {
            if (!_glReady) return;
            _prog = _programs[(int)_activeMode];
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

        // ── GL paint ──────────────────────────────────────────────────────────

        private void OnGLPaint(object sender, PaintEventArgs e)
        {
            if (!_glReady || _prog < 0) return;
            _gl.MakeCurrent();

            GL.Viewport(0, 0, _gl.Width, _gl.Height);
            var bg = AppColors.Canvas;
            GL.ClearColor(bg.R / 255f, bg.G / 255f, bg.B / 255f, 1f);
            GL.Clear(ClearBufferMask.ColorBufferBit);

            GL.UseProgram(_prog);
            GL.Uniform1(GL.GetUniformLocation(_prog, "uYaw"),   (float)_yaw);
            GL.Uniform1(GL.GetUniformLocation(_prog, "uPitch"), (float)_pitch);
            GL.Uniform1(GL.GetUniformLocation(_prog, "uZoom"),  _zoom);
            GL.Uniform2(GL.GetUniformLocation(_prog, "uPan"),   _panX, _panY);
            GL.Uniform2(GL.GetUniformLocation(_prog, "uRes"),   (float)_gl.Width, (float)_gl.Height);
            var bgC = AppColors.Canvas;
            GL.Uniform3(GL.GetUniformLocation(_prog, "uBg"), bgC.R/255f, bgC.G/255f, bgC.B/255f);
            GL.Uniform1(GL.GetUniformLocation(_prog, "uColorSpace"), (int)_colorSettings.ActiveColorSpace);
            GL.Uniform4(GL.GetUniformLocation(_prog, "uAdjust"),
                (float)_colorSettings.Channel1,
                (float)_colorSettings.Channel2,
                (float)_colorSettings.Channel3,
                (float)_colorSettings.Channel4);

            GL.BindVertexArray(_vao);
            GL.DrawArrays(PrimitiveType.TriangleStrip, 0, 4);

            _gl.SwapBuffers();
        }

        // ── Mouse ─────────────────────────────────────────────────────────────

        // Absorb all mouse events on the panel itself so they never reach CanvasPanel.
        protected override void OnMouseDown(MouseEventArgs e)  { /* absorbed */ }
        protected override void OnMouseMove(MouseEventArgs e)  { /* absorbed */ }
        protected override void OnMouseUp(MouseEventArgs e)    { /* absorbed */ }
        protected override void OnMouseWheel(MouseEventArgs e) { /* absorbed */ }
        protected override void OnClick(EventArgs e)           { /* absorbed */ }

        private void HandleMouseDown(MouseEventArgs e)
        {
            _lastMouse = e.Location; _mouseHasMoved = false;
            if (e.Button == MouseButtons.Left)  { _isOrbiting = true; _gl.Cursor = Cursors.SizeAll; }
            if (e.Button == MouseButtons.Right) { _isPanning  = true; _gl.Cursor = Cursors.SizeAll; }
        }

        private void HandleMouseMove(MouseEventArgs e)
        {
            int dx = e.X - _lastMouse.X, dy = e.Y - _lastMouse.Y;
            if (dx == 0 && dy == 0) return;
            _mouseHasMoved = true; _lastMouse = e.Location;

            if (_isOrbiting)
            {
                _yaw   += dx * 0.008;
                _pitch += dy * 0.008;
                _pitch  = Math.Max(-Math.PI/2 + 0.05, Math.Min(Math.PI/2 - 0.05, _pitch));
                _gl.Invalidate();
            }
            else if (_isPanning)
            {
                _panX += dx; _panY += dy;
                _gl.Invalidate();
            }
        }

        private void HandleMouseUp(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && !_mouseHasMoved) PickColorAt(e.Location);
            _isOrbiting = false; _isPanning = false;
            _gl.Cursor = Cursors.Default;
        }

        private void HandleMouseWheel(MouseEventArgs e)
        {
            float delta = e.Delta > 0 ? 1.12f : 1f / 1.12f;
            _zoom = Math.Max(0.2f, Math.Min(5f, _zoom * delta));
            _gl.Invalidate();
        }

        // ── Color picking ─────────────────────────────────────────────────────

        private void PickColorAt(Point pt)
        {
            if (!_glReady) return;
            _gl.MakeCurrent();

            // Flip Y: OpenGL origin is bottom-left
            int glY = _gl.Height - pt.Y;
            glY = Math.Max(0, Math.Min(_gl.Height - 1, glY));
            int glX = Math.Max(0, Math.Min(_gl.Width - 1, pt.X));

            byte[] pixel = new byte[4];
            GL.ReadPixels(glX, glY, 1, 1, PixelFormat.Rgba, PixelType.UnsignedByte, pixel);

            if (pixel[3] < 20) return;
            ColorPicked?.Invoke(this, Color.FromArgb(255, pixel[0], pixel[1], pixel[2]));
        }

        // ── Public API ────────────────────────────────────────────────────────

        public void SetColorSpaceMode(ColorSpaceMode mode)
        {
            _activeMode = mode;
            _colorSettings.ActiveColorSpace = mode;
            _modeBar?.SetMode(mode);
            UpdateShader();
            _gl?.Invalidate();
        }

        public void SetColorSettings(ColorSettings settings)
        {
            _colorSettings = settings?.Clone() ?? new ColorSettings();
            _activeMode = _colorSettings.ActiveColorSpace;
            _modeBar?.SetMode(_activeMode);
            UpdateShader();
            _gl?.Invalidate();
        }

        // ── Background ───────────────────────────────────────────────────────────

        protected override void OnPaintBackground(PaintEventArgs e) { /* suppress — OnPaint does everything */ }

        protected override void OnPaint(PaintEventArgs e)
        {
            // Keep panel paint minimal: GLControl renders the scene in its own HWND.
            // Any uncovered area (BottomReserve) still uses canvas color.
            if (BottomReserve > 0)
            {
                using var brush = new SolidBrush(AppColors.Canvas);
                e.Graphics.FillRectangle(brush, 0, Math.Max(0, Height - BottomReserve), Width, BottomReserve);
            }
        }

        public void Activate()
        {
            _yaw = 0.6; _pitch = 0.4; _zoom = 1f; _panX = _panY = 0;
            UpdateShader();
            _gl?.Invalidate();
        }

        public void ApplyTheme()
        {
            BackColor = AppColors.Canvas;
            _modeBar?.Invalidate();
            _gl?.Invalidate();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _glReady)
            {
                _gl.MakeCurrent();
                GL.DeleteVertexArray(_vao);
                GL.DeleteBuffer(_vbo);
                if (_programs != null)
                    foreach (var p in _programs) GL.DeleteProgram(p);
            }
            base.Dispose(disposing);
        }
    }

    // ── Mode bar: custom-painted chips, no child controls, no event bubbling ──

    internal sealed class ModeBar : Control
    {
        private const int BarH      = 56;
        private const int ChipH     = 32;
        private const int ChipPadX  = 14;
        private const int ChipGap   = 8;
        private const int PillPadX  = 12;
        private const int PillPadY  = 6;

        private RectangleF _pillRect;

        private readonly List<(ColorSpaceMode Mode, RectangleF Rect)> _chips = new();
        private ColorSpaceMode _active;

        public event EventHandler<ColorSpaceMode> ModeSelected;

        public ModeBar(ColorSpaceMode initial)
        {
            _active   = initial;
            Height    = BarH;

            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.UserPaint            |
                ControlStyles.DoubleBuffer         |
                ControlStyles.SupportsTransparentBackColor |
                ControlStyles.ResizeRedraw, true);

            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
            Resize += (s, e) => RecalcChips();
        }

        public void SetMode(ColorSpaceMode mode)
        {
            if (_active == mode) return;
            _active = mode;
            Invalidate();
        }

        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); RecalcChips(); }

        private void RecalcChips()
        {
            _chips.Clear();
            var modes = (ColorSpaceMode[])Enum.GetValues(typeof(ColorSpaceMode));
            var widths = new int[modes.Length];
            int totalChipW = 0;

            using var scratch = new Bitmap(1, 1);
            using var g = Graphics.FromImage(scratch);

            for (int i = 0; i < modes.Length; i++)
            {
                int textW = (int)Math.Ceiling(g.MeasureString(modes[i].ToString(), AppFonts.Label).Width);
                widths[i] = textW + ChipPadX * 2;
                totalChipW += widths[i];
            }
            totalChipW += ChipGap * (modes.Length - 1);

            int pillW = totalChipW + PillPadX * 2;
            int pillH = ChipH + PillPadY * 2;
            int pillX = (Width - pillW) / 2;
            int pillY = (Height - pillH) / 2;
            _pillRect = new RectangleF(pillX, pillY, pillW, pillH);

            int x = pillX + PillPadX;
            int y = pillY + PillPadY;
            for (int i = 0; i < modes.Length; i++)
            {
                _chips.Add((modes[i], new RectangleF(x, y, widths[i], ChipH)));
                x += widths[i] + ChipGap;
            }

            UpdateWindowRegion();
            Invalidate();
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

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            if (Parent == null)
            {
                base.OnPaintBackground(e);
                return;
            }

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
            g.SmoothingMode     = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            int capsuleRadius = (int)(_pillRect.Height / 2f);
            int chipRadius = ChipH / 2;
            int fillAlpha = AppColors.IsDarkTheme ? 190 : 220;

            using (var bg = new SolidBrush(Color.FromArgb(fillAlpha, AppColors.PanelBg)))
                g.FillRoundedRect(bg, _pillRect, capsuleRadius);
            using (var border = new Pen(AppColors.BorderPrimary, 1f))
                g.DrawRoundedRect(border, _pillRect, capsuleRadius);

            var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };

            foreach (var (mode, rect) in _chips)
            {
                bool active = mode == _active;
                if (active)
                {
                    using var fill = new SolidBrush(AppColors.Accent);
                    g.FillRoundedRect(fill, rect, chipRadius);
                    using var txt = new SolidBrush(Color.White);
                    g.DrawString(mode.ToString(), AppFonts.Label, txt, rect, sf);
                }
                else
                {
                    using var border = new Pen(AppColors.BorderPrimary, 1f);
                    g.DrawRoundedRect(border, rect, chipRadius);
                    using var txt = new SolidBrush(AppColors.TextPrimary);
                    g.DrawString(mode.ToString(), AppFonts.Label, txt, rect, sf);
                }
            }
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            // Do NOT call base — prevents event bubbling to parent controls
            foreach (var (mode, rect) in _chips)
            {
                if (rect.Contains(e.Location))
                {
                    _active = mode;
                    Invalidate();
                    ModeSelected?.Invoke(this, mode);
                    return;
                }
            }
        }
    }
}
