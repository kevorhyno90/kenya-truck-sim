using System.IO;
using UnityEditor;
using UnityEngine;

namespace KenyaStreetSim.Editor
{
    /// <summary>
    /// Generates crisp, realistic 512x512 automotive instrument cluster textures (Speedometer 0-140,
    /// Tachometer RPM with green diesel economy zone & redline, GPS Navigation/Media Touchscreen,
    /// Kenya Police VHF Radio & Siren Panel, and louvered AC Vent grilles) saved as persistent PNG assets.
    /// </summary>
    public static class KenyaDashboardTextures
    {
        public const string TexFolder = "Assets/KenyaStreetSim/Textures/Dashboard";

        public static Texture2D SpeedoDialTex { get; private set; }
        public static Texture2D RpmDialTex { get; private set; }
        public static Texture2D InfotainmentTex { get; private set; }
        public static Texture2D PoliceRadioTex { get; private set; }
        public static Texture2D AcVentTex { get; private set; }

        public static void GenerateAll()
        {
            if (!Directory.Exists(TexFolder)) Directory.CreateDirectory(TexFolder);

            SpeedoDialTex   = EnsureTex("Dash_SpeedoDial_512.png", 512, 512, PaintSpeedoDial);
            RpmDialTex      = EnsureTex("Dash_RpmDial_512.png", 512, 512, PaintRpmDial);
            InfotainmentTex = EnsureTex("Dash_Infotainment_512.png", 512, 320, PaintInfotainment);
            PoliceRadioTex  = EnsureTex("Dash_PoliceRadio_512.png", 512, 320, PaintPoliceRadio);
            AcVentTex       = EnsureTex("Dash_AcVent_256.png", 256, 160, PaintAcVent);
        }

        private static Texture2D EnsureTex(string fileName, int w, int h, System.Action<Texture2D> painter)
        {
            string path = $"{TexFolder}/{fileName}";
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            painter(tex);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp != null)
            {
                imp.sRGBTexture = true;
                imp.mipmapEnabled = true;
                imp.anisoLevel = 8;
                imp.wrapMode = TextureWrapMode.Clamp;
                imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static void PaintSpeedoDial(Texture2D tex)
        {
            int w = tex.width, h = tex.height;
            float cx = w * 0.5f, cy = h * 0.5f, rMax = w * 0.48f;
            Fill(tex, new Color(0.055f, 0.068f, 0.090f, 1f));

            // Subtle radial carbon-brushed dial gradient + metallic inner accent ring
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float dx = x - cx, dy = y - cy;
                    float r = Mathf.Sqrt(dx * dx + dy * dy) / rMax;
                    if (r > 1f) { tex.SetPixel(x, y, new Color(0.03f, 0.035f, 0.045f, 1f)); continue; }
                    float sheen = Mathf.Lerp(0.11f, 0.045f, r * r);
                    Color c = new Color(sheen * 0.82f, sheen * 0.94f, sheen * 1.15f, 1f);
                    if (r > 0.92f && r < 0.98f) c = new Color(0.55f, 0.60f, 0.68f, 1f);
                    else if (r > 0.87f && r <= 0.92f) c = new Color(0.10f, 0.13f, 0.18f, 1f);
                    tex.SetPixel(x, y, c);
                }
            }

            // Outer glowing red overspeed arc (100 - 140 km/h)
            DrawArc(tex, cx, cy, rMax * 0.82f, rMax * 0.87f, -45f, -120f, new Color(0.95f, 0.16f, 0.12f, 1f));
            // Crisp cyan economy cruise arc (0 - 100 km/h)
            DrawArc(tex, cx, cy, rMax * 0.84f, rMax * 0.87f, 120f, -45f, new Color(0.18f, 0.78f, 0.98f, 0.85f));

            // 29 graduated tick marks (0 to 140 km/h in 5 km/h steps; major white ticks every 20 km/h)
            for (int i = 0; i <= 28; i++)
            {
                float frac = i / 28f;
                float deg = Mathf.Lerp(120f, -120f, frac);
                bool major = (i % 4 == 0);
                bool redZone = (i >= 20);
                Color tickCol = redZone ? new Color(1f, 0.25f, 0.18f, 1f) : (major ? Color.white : new Color(0.72f, 0.80f, 0.88f, 1f));
                DrawRadialTick(tex, cx, cy, rMax * (major ? 0.66f : 0.74f), rMax * 0.84f, deg, major ? 6 : 3, tickCol);

                // Crisp white/amber scale dot/bar at major positions
                if (major)
                {
                    DrawRadialTick(tex, cx, cy, rMax * 0.56f, rMax * 0.62f, deg, 8, redZone ? new Color(1f, 0.35f, 0.20f) : new Color(0.95f, 0.86f, 0.42f));
                }
            }

            // Glowing digital odometer & gear readout strip in lower center
            DrawRect(tex, (int)(cx - 72), (int)(cy - 135), 144, 48, new Color(0.03f, 0.08f, 0.12f, 1f));
            DrawRectBorder(tex, (int)(cx - 72), (int)(cy - 135), 144, 48, 3, new Color(0.16f, 0.76f, 0.92f, 1f));
            DrawRect(tex, (int)(cx - 52), (int)(cy - 116), 104, 12, new Color(0.18f, 0.95f, 0.58f, 1f));
        }

        private static void PaintRpmDial(Texture2D tex)
        {
            int w = tex.width, h = tex.height;
            float cx = w * 0.5f, cy = h * 0.5f, rMax = w * 0.48f;
            Fill(tex, new Color(0.055f, 0.068f, 0.090f, 1f));

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float dx = x - cx, dy = y - cy;
                    float r = Mathf.Sqrt(dx * dx + dy * dy) / rMax;
                    if (r > 1f) { tex.SetPixel(x, y, new Color(0.03f, 0.035f, 0.045f, 1f)); continue; }
                    float sheen = Mathf.Lerp(0.11f, 0.045f, r * r);
                    Color c = new Color(sheen * 0.82f, sheen * 0.94f, sheen * 1.15f, 1f);
                    if (r > 0.92f && r < 0.98f) c = new Color(0.55f, 0.60f, 0.68f, 1f);
                    tex.SetPixel(x, y, c);
                }
            }

            // Authentic Diesel Green Economy Torque Band (1000 - 1800 RPM) + Amber + Crimson Redline!
            DrawArc(tex, cx, cy, rMax * 0.78f, rMax * 0.87f, 45f, -20f, new Color(0.16f, 0.84f, 0.36f, 1f));
            DrawArc(tex, cx, cy, rMax * 0.78f, rMax * 0.87f, -20f, -68f, new Color(0.98f, 0.68f, 0.10f, 1f));
            DrawArc(tex, cx, cy, rMax * 0.78f, rMax * 0.87f, -68f, -120f, new Color(0.95f, 0.16f, 0.12f, 1f));

            for (int i = 0; i <= 24; i++)
            {
                float frac = i / 24f;
                float deg = Mathf.Lerp(120f, -120f, frac);
                bool major = (i % 4 == 0);
                bool red = (i >= 19);
                Color col = red ? new Color(1f, 0.25f, 0.18f, 1f) : Color.white;
                DrawRadialTick(tex, cx, cy, rMax * (major ? 0.64f : 0.73f), rMax * 0.85f, deg, major ? 6 : 3, col);
            }

            // Fuel & Air-Brake Pressure Twin Mini-Bars in bottom dial panel
            DrawRect(tex, (int)(cx - 76), (int)(cy - 135), 152, 48, new Color(0.03f, 0.07f, 0.10f, 1f));
            DrawRectBorder(tex, (int)(cx - 76), (int)(cy - 135), 152, 48, 3, new Color(0.95f, 0.68f, 0.12f, 1f));
            DrawRect(tex, (int)(cx - 62), (int)(cy - 104), 108, 10, new Color(0.18f, 0.92f, 0.42f, 1f));
            DrawRect(tex, (int)(cx - 62), (int)(cy - 122), 92,  10, new Color(0.12f, 0.84f, 1.0f, 1f));
        }

        private static void PaintInfotainment(Texture2D tex)
        {
            int w = tex.width, h = tex.height;
            Fill(tex, new Color(0.05f, 0.08f, 0.13f, 1f));
            // Top sleek status bar
            DrawRect(tex, 0, h - 42, w, 42, new Color(0.08f, 0.14f, 0.22f, 1f));
            DrawRect(tex, 20, h - 28, 96, 14, new Color(0.14f, 0.86f, 1.0f, 1f));
            DrawRect(tex, w - 100, h - 28, 76, 14, new Color(0.96f, 0.74f, 0.14f, 1f));

            // Left half: Live Mini-GPS Road Canvas with bright cyan Kenyan route line & glowing vehicle triangle
            DrawRect(tex, 18, 22, 260, h - 82, new Color(0.04f, 0.10f, 0.16f, 1f));
            DrawRectBorder(tex, 18, 22, 260, h - 82, 3, new Color(0.14f, 0.62f, 0.85f, 1f));
            for (int i = 0; i < 180; i++)
            {
                int px = 135 + (int)(Mathf.Sin(i * 0.03f) * 42f);
                int py = 36 + i;
                DrawRect(tex, px - 5, py, 11, 4, new Color(0.10f, 0.88f, 1.0f, 1f));
            }
            DrawRect(tex, 130, 72, 16, 18, new Color(1.0f, 0.76f, 0.14f, 1f));

            // Right half: Climate / Radio / Vehicle Status Cards
            DrawRect(tex, 296, h - 140, 198, 80, new Color(0.10f, 0.16f, 0.25f, 1f));
            DrawRect(tex, 312, h - 90, 120, 14, new Color(0.20f, 0.94f, 0.52f, 1f));
            DrawRect(tex, 312, h - 118, 164, 12, new Color(0.75f, 0.82f, 0.90f, 1f));

            DrawRect(tex, 296, 22, 198, 136, new Color(0.10f, 0.16f, 0.25f, 1f));
            DrawRect(tex, 314, 118, 52, 24, new Color(0.12f, 0.72f, 0.96f, 1f));
            DrawRect(tex, 376, 118, 52, 24, new Color(0.95f, 0.64f, 0.12f, 1f));
            DrawRect(tex, 438, 118, 42, 24, new Color(0.92f, 0.22f, 0.18f, 1f));
            DrawRect(tex, 314, 42, 164, 54, new Color(0.06f, 0.10f, 0.16f, 1f));
        }

        private static void PaintPoliceRadio(Texture2D tex)
        {
            int w = tex.width, h = tex.height;
            Fill(tex, new Color(0.08f, 0.09f, 0.11f, 1f));
            DrawRectBorder(tex, 4, 4, w - 8, h - 8, 5, new Color(0.24f, 0.28f, 0.34f, 1f));

            // Glowing green Kenya Police VHF Dispatch LCD display
            DrawRect(tex, 26, h - 140, w - 52, 108, new Color(0.05f, 0.22f, 0.10f, 1f));
            DrawRectBorder(tex, 26, h - 140, w - 52, 108, 4, new Color(0.22f, 0.88f, 0.42f, 1f));
            DrawRect(tex, 46, h - 78, 220, 22, new Color(0.32f, 1.0f, 0.52f, 1f));
            DrawRect(tex, 46, h - 114, 340, 16, new Color(0.20f, 0.82f, 0.38f, 1f));

            // Red & Blue Lightbar / Siren Illuminated Rocker Switches!
            DrawRect(tex, 32, 34, 100, 102, new Color(0.92f, 0.14f, 0.14f, 1f));
            DrawRect(tex, 148, 34, 100, 102, new Color(0.12f, 0.45f, 1.0f, 1f));
            DrawRect(tex, 264, 34, 100, 102, new Color(0.96f, 0.72f, 0.10f, 1f));
            DrawRect(tex, 380, 34, 100, 102, new Color(0.18f, 0.86f, 0.42f, 1f));
        }

        private static void PaintAcVent(Texture2D tex)
        {
            int w = tex.width, h = tex.height;
            Fill(tex, new Color(0.04f, 0.045f, 0.055f, 1f));
            DrawRectBorder(tex, 0, 0, w, h, 8, new Color(0.26f, 0.30f, 0.36f, 1f));
            for (int y = 20; y < h - 20; y += 22)
            {
                DrawRect(tex, 12, y, w - 24, 9, new Color(0.18f, 0.21f, 0.26f, 1f));
            }
            DrawRect(tex, w / 2 - 10, h / 2 - 14, 20, 28, new Color(0.68f, 0.74f, 0.80f, 1f));
        }

        private static void Fill(Texture2D tex, Color col)
        {
            var arr = new Color[tex.width * tex.height];
            for (int i = 0; i < arr.Length; i++) arr[i] = col;
            tex.SetPixels(arr);
        }

        private static void DrawRect(Texture2D tex, int x0, int y0, int rw, int rh, Color col)
        {
            int xMax = Mathf.Min(tex.width, x0 + rw);
            int yMax = Mathf.Min(tex.height, y0 + rh);
            for (int y = Mathf.Max(0, y0); y < yMax; y++)
                for (int x = Mathf.Max(0, x0); x < xMax; x++)
                    tex.SetPixel(x, y, col);
        }

        private static void DrawRectBorder(Texture2D tex, int x0, int y0, int rw, int rh, int t, Color col)
        {
            DrawRect(tex, x0, y0, rw, t, col);
            DrawRect(tex, x0, y0 + rh - t, rw, t, col);
            DrawRect(tex, x0, y0, t, rh, col);
            DrawRect(tex, x0 + rw - t, y0, t, rh, col);
        }

        private static void DrawArc(Texture2D tex, float cx, float cy, float rInner, float rOuter, float startDeg, float endDeg, Color col)
        {
            int steps = 220;
            for (int s = 0; s <= steps; s++)
            {
                float deg = Mathf.Lerp(startDeg, endDeg, s / (float)steps);
                float rad = deg * Mathf.Deg2Rad;
                float sx = -Mathf.Sin(rad);
                float sy = Mathf.Cos(rad);
                for (float r = rInner; r <= rOuter; r += 0.9f)
                {
                    int px = Mathf.RoundToInt(cx + sx * r);
                    int py = Mathf.RoundToInt(cy + sy * r);
                    if (px >= 0 && px < tex.width && py >= 0 && py < tex.height)
                        tex.SetPixel(px, py, col);
                }
            }
        }

        private static void DrawRadialTick(Texture2D tex, float cx, float cy, float r0, float r1, float deg, int thickness, Color col)
        {
            float rad = deg * Mathf.Deg2Rad;
            float dx = -Mathf.Sin(rad), dy = Mathf.Cos(rad);
            float pxPerp = -dy, pyPerp = dx;
            int halfT = thickness / 2;
            for (float r = r0; r <= r1; r += 0.8f)
            {
                for (int k = -halfT; k <= halfT; k++)
                {
                    int x = Mathf.RoundToInt(cx + dx * r + pxPerp * k);
                    int y = Mathf.RoundToInt(cy + dy * r + pyPerp * k);
                    if (x >= 0 && x < tex.width && y >= 0 && y < tex.height)
                        tex.SetPixel(x, y, col);
                }
            }
        }
    }
}
