using System.IO;
using UnityEditor;
using UnityEngine;

namespace KenyaStreetSim.Editor
{
    /// <summary>
    /// Generates high-resolution tileable PBR Albedo + Tangent-Space Normal Map PNG textures
    /// for realistic Kenyan tarmac, red murram soil, interlocking cabro pavers, weathered plaster,
    /// corrugated mabati iron roofing, fissured acacia bark, alpha-clipped leaf clusters, and bus tire tread.
    /// </summary>
    public static class KenyaPbrTextureGenerator
    {
        public const string TexFolder = "Assets/KenyaStreetSim/Textures";

        public static void GenerateAllTextures()
        {
            if (!Directory.Exists(TexFolder))
            {
                Directory.CreateDirectory(TexFolder);
            }

            GenerateTarmac(512);
            GenerateMurramSoil(512);
            GenerateCabroPavers(512);
            GenerateWeatheredPlaster(512);
            GenerateCorrugatedMabati(512);
            GenerateTreeBark(256);
            GenerateLeafClusterAlpha(512);
            GenerateTireTread(256);
            GenerateGlassFacade(256);

            AssetDatabase.Refresh();
            ConfigureImporters();
        }

        private static void GenerateTarmac(int size)
        {
            var albedo = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var height = new float[size * size];

            for (int y = 0; y < size; y++)
            {
                float v = (float)y / size;
                for (int x = 0; x < size; x++)
                {
                    float u = (float)x / size;
                    // Multi-octave asphalt aggregate + subtle tire-wear bands along U
                    float grain = TileFbm(u * 32f, v * 32f, 32f, 4, 17);
                    float fine = Hash2(x, y, 101) * 0.14f;
                    float macro = TileFbm(u * 4f, v * 4f, 4f, 3, 29);

                    // Subtle tire rut sheen bands (darker worn track along road length)
                    float rut1 = Mathf.Exp(-Mathf.Pow((u - 0.25f) * 9f, 2f));
                    float rut2 = Mathf.Exp(-Mathf.Pow((u - 0.75f) * 9f, 2f));
                    float rut = Mathf.Clamp01(rut1 + rut2);

                    // Fine tar hairline cracks
                    float crackNoise = Mathf.Abs(TileFbm(u * 6f, v * 6f, 6f, 3, 73) - 0.5f);
                    float crack = crackNoise < 0.014f ? (1f - crackNoise / 0.014f) : 0f;

                    float h = Mathf.Clamp01(grain * 0.55f + fine + macro * 0.25f - crack * 0.45f);
                    height[y * size + x] = h;

                    float baseLum = Mathf.Lerp(0.11f, 0.21f, h);
                    baseLum *= Mathf.Lerp(1f, 0.84f, rut);
                    baseLum *= Mathf.Lerp(1f, 0.55f, crack);

                    // Slight cool-charcoal aggregate tint with occasional warm stone flecks
                    float warmFleck = fine > 0.115f ? 0.025f : 0f;
                    var c = new Color(baseLum + warmFleck, baseLum + warmFleck * 0.7f, baseLum + 0.012f, 1f);
                    albedo.SetPixel(x, y, c);
                }
            }
            albedo.Apply();
            SavePng(albedo, $"{TexFolder}/Tarmac_Albedo.png");
            SaveNormalFromHeight(height, size, 3.2f, $"{TexFolder}/Tarmac_Normal.png");
            Object.DestroyImmediate(albedo);
        }

        private static void GenerateMurramSoil(int size)
        {
            var albedo = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var height = new float[size * size];

            Color redSoilDark = new Color(0.36f, 0.19f, 0.11f);
            Color redSoilMid = new Color(0.54f, 0.30f, 0.17f);
            Color dryGrass = new Color(0.36f, 0.44f, 0.20f);
            Color pebble = new Color(0.62f, 0.52f, 0.42f);

            for (int y = 0; y < size; y++)
            {
                float v = (float)y / size;
                for (int x = 0; x < size; x++)
                {
                    float u = (float)x / size;
                    float macro = TileFbm(u * 6f, v * 6f, 6f, 4, 211);
                    float detail = TileFbm(u * 24f, v * 24f, 24f, 3, 307);
                    float grit = Hash2(x, y, 409);

                    // Patchy Kenyan savanna grass interspersed with iconic red-ochre murram earth
                    float grassMask = Mathf.SmoothStep(0.50f, 0.68f, TileFbm(u * 5f, v * 5f, 5f, 3, 503));
                    Color col = Color.Lerp(redSoilDark, redSoilMid, macro * 0.7f + detail * 0.3f);
                    if (grit > 0.94f) col = Color.Lerp(col, pebble, 0.65f);
                    col = Color.Lerp(col, dryGrass * (0.78f + 0.35f * detail), grassMask * 0.72f);

                    height[y * size + x] = Mathf.Clamp01(macro * 0.4f + detail * 0.45f + grit * 0.15f);
                    albedo.SetPixel(x, y, col);
                }
            }
            albedo.Apply();
            SavePng(albedo, $"{TexFolder}/Murram_Albedo.png");
            SaveNormalFromHeight(height, size, 4.2f, $"{TexFolder}/Murram_Normal.png");
            Object.DestroyImmediate(albedo);
        }

        private static void GenerateCabroPavers(int size)
        {
            var albedo = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var height = new float[size * size];

            int cols = 8;
            int rows = 16;
            for (int y = 0; y < size; y++)
            {
                float v = (float)y / size;
                int row = Mathf.FloorToInt(v * rows);
                float rv = (v * rows) - row;
                float rowShift = (row % 2 == 0) ? 0f : 0.5f;

                for (int x = 0; x < size; x++)
                {
                    float u = (float)x / size;
                    float shiftedU = (u * cols + rowShift) % cols;
                    int col = Mathf.FloorToInt(shiftedU);
                    float ru = shiftedU - col;

                    // Crisp chamfered interlocking block joint
                    float edgeDist = Mathf.Min(Mathf.Min(ru, 1f - ru) * 2f, Mathf.Min(rv, 1f - rv));
                    float joint = Mathf.SmoothStep(0.03f, 0.10f, edgeDist);

                    float blockVar = Hash2(col, row, 613);
                    float stoneGrain = TileFbm(u * 24f, v * 24f, 24f, 3, 701);

                    // Alternate warm concrete grey & subtle terracotta Kenyan cabro paver tones
                    Color paverA = new Color(0.68f, 0.65f, 0.60f);
                    Color paverB = new Color(0.60f, 0.57f, 0.52f);
                    Color paverWarm = new Color(0.66f, 0.56f, 0.49f);
                    Color blockCol = blockVar > 0.72f ? paverWarm : Color.Lerp(paverB, paverA, blockVar);
                    blockCol *= (0.88f + 0.20f * stoneGrain);

                    Color mortar = new Color(0.28f, 0.26f, 0.24f);
                    Color finalCol = Color.Lerp(mortar, blockCol, joint);

                    height[y * size + x] = Mathf.Clamp01(joint * 0.80f + stoneGrain * 0.20f);
                    albedo.SetPixel(x, y, finalCol);
                }
            }
            albedo.Apply();
            SavePng(albedo, $"{TexFolder}/Cabro_Albedo.png");
            SaveNormalFromHeight(height, size, 4.5f, $"{TexFolder}/Cabro_Normal.png");
            Object.DestroyImmediate(albedo);
        }

        private static void GenerateWeatheredPlaster(int size)
        {
            var albedo = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var height = new float[size * size];

            for (int y = 0; y < size; y++)
            {
                float v = (float)y / size;
                for (int x = 0; x < size; x++)
                {
                    float u = (float)x / size;
                    float macro = TileFbm(u * 5f, v * 5f, 5f, 4, 809);
                    float stucco = TileFbm(u * 28f, v * 28f, 28f, 3, 911);
                    // Subtle vertical tropical rainwater streak variation
                    float streak = TileFbm(u * 14f, v * 2f, 14f, 2, 997);

                    float lum = 0.82f + (macro - 0.5f) * 0.16f + (stucco - 0.5f) * 0.12f - (1f - streak) * 0.08f;
                    lum = Mathf.Clamp(lum, 0.62f, 1.0f);
                    height[y * size + x] = Mathf.Clamp01(stucco * 0.65f + macro * 0.35f);
                    albedo.SetPixel(x, y, new Color(lum, lum * 0.99f, lum * 0.97f, 1f));
                }
            }
            albedo.Apply();
            SavePng(albedo, $"{TexFolder}/Plaster_Albedo.png");
            SaveNormalFromHeight(height, size, 2.2f, $"{TexFolder}/Plaster_Normal.png");
            Object.DestroyImmediate(albedo);
        }

        private static void GenerateCorrugatedMabati(int size)
        {
            var albedo = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var height = new float[size * size];

            for (int y = 0; y < size; y++)
            {
                float v = (float)y / size;
                for (int x = 0; x < size; x++)
                {
                    float u = (float)x / size;
                    float wave = 0.5f + 0.5f * Mathf.Sin(u * Mathf.PI * 2f * 20f);
                    float weather = TileFbm(u * 8f, v * 8f, 8f, 3, 1201);
                    float seam = Mathf.Abs(((v * 4f) % 1f) - 0.5f) > 0.485f ? 0.72f : 1f;

                    float lum = (0.74f + 0.24f * wave) * (0.86f + 0.22f * weather) * seam;
                    // Subtle warm galvanized patina
                    var c = new Color(lum, lum * 0.98f, lum * 0.96f, 1f);
                    height[y * size + x] = Mathf.Clamp01(wave * 0.85f + weather * 0.15f);
                    albedo.SetPixel(x, y, c);
                }
            }
            albedo.Apply();
            SavePng(albedo, $"{TexFolder}/Mabati_Albedo.png");
            SaveNormalFromHeight(height, size, 5.5f, $"{TexFolder}/Mabati_Normal.png");
            Object.DestroyImmediate(albedo);
        }

        private static void GenerateTreeBark(int size)
        {
            var albedo = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var height = new float[size * size];

            Color darkBark = new Color(0.20f, 0.14f, 0.10f);
            Color lightBark = new Color(0.42f, 0.32f, 0.24f);

            for (int y = 0; y < size; y++)
            {
                float v = (float)y / size;
                for (int x = 0; x < size; x++)
                {
                    float u = (float)x / size;
                    float ridge = TileFbm(u * 18f, v * 4f, 18f, 4, 1409);
                    float cracks = Mathf.Pow(ridge, 1.5f);
                    height[y * size + x] = cracks;
                    albedo.SetPixel(x, y, Color.Lerp(darkBark, lightBark, cracks));
                }
            }
            albedo.Apply();
            SavePng(albedo, $"{TexFolder}/Bark_Albedo.png");
            SaveNormalFromHeight(height, size, 5.0f, $"{TexFolder}/Bark_Normal.png");
            Object.DestroyImmediate(albedo);
        }

        private static void GenerateLeafClusterAlpha(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var clear = new Color(0.20f, 0.34f, 0.14f, 0f);
            var pixels = new Color[size * size];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = clear;

            // Paint a rich organic Kenyan acacia/shade branch with ~68 layered pointed leaves
            var rng = new System.Random(254254);

            // Central twig lines
            DrawLine(pixels, size, 0.50f, 0.08f, 0.50f, 0.88f, 0.012f, new Color(0.28f, 0.22f, 0.15f, 1f));
            DrawLine(pixels, size, 0.50f, 0.30f, 0.24f, 0.56f, 0.009f, new Color(0.28f, 0.22f, 0.15f, 1f));
            DrawLine(pixels, size, 0.50f, 0.36f, 0.78f, 0.60f, 0.009f, new Color(0.28f, 0.22f, 0.15f, 1f));
            DrawLine(pixels, size, 0.50f, 0.58f, 0.28f, 0.82f, 0.008f, new Color(0.28f, 0.22f, 0.15f, 1f));
            DrawLine(pixels, size, 0.50f, 0.62f, 0.74f, 0.84f, 0.008f, new Color(0.28f, 0.22f, 0.15f, 1f));

            Color leafDeep = new Color(0.17f, 0.33f, 0.12f, 1f);
            Color leafMid = new Color(0.26f, 0.45f, 0.17f, 1f);
            Color leafSun = new Color(0.38f, 0.56f, 0.22f, 1f);

            for (int k = 0; k < 85; k++)
            {
                float angle = (float)(rng.NextDouble() * Mathf.PI * 2.0);
                float dist = Mathf.Sqrt((float)rng.NextDouble()) * 0.38f;
                float cx = 0.5f + Mathf.Cos(angle) * dist;
                float cy = 0.52f + Mathf.Sin(angle) * dist * 0.92f;
                float leafRot = angle + (float)(rng.NextDouble() - 0.5) * 1.1f;
                float rx = 0.065f + (float)rng.NextDouble() * 0.045f;
                float ry = rx * (0.36f + (float)rng.NextDouble() * 0.18f);

                float shade = (float)rng.NextDouble();
                Color lCol = shade < 0.35f ? leafDeep : (shade < 0.78f ? leafMid : leafSun);
                DrawLeaf(pixels, size, cx, cy, rx, ry, leafRot, lCol);
            }

            tex.SetPixels(pixels);
            tex.Apply();
            SavePng(tex, $"{TexFolder}/LeafCluster_Albedo.png");
            Object.DestroyImmediate(tex);
        }

        private static void GenerateTireTread(int size)
        {
            var albedo = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var height = new float[size * size];

            for (int y = 0; y < size; y++)
            {
                float v = (float)y / size;
                for (int x = 0; x < size; x++)
                {
                    float u = (float)x / size;
                    // Commercial bus/truck longitudinal grooves + diagonal tread lugs
                    bool groove = Mathf.Abs(u - 0.28f) < 0.035f || Mathf.Abs(u - 0.50f) < 0.035f || Mathf.Abs(u - 0.72f) < 0.035f;
                    float lugPhase = ((v * 24f) + Mathf.Abs(u - 0.5f) * 3f) % 1f;
                    bool lugCut = lugPhase < 0.18f;
                    float h = (groove || lugCut) ? 0.18f : 0.88f;
                    float rubberNoise = Hash2(x, y, 1601) * 0.08f;

                    float lum = Mathf.Lerp(0.06f, 0.15f, h) + rubberNoise * 0.25f;
                    height[y * size + x] = Mathf.Clamp01(h + rubberNoise);
                    albedo.SetPixel(x, y, new Color(lum, lum, lum * 1.03f, 1f));
                }
            }
            albedo.Apply();
            SavePng(albedo, $"{TexFolder}/TireTread_Albedo.png");
            SaveNormalFromHeight(height, size, 4.0f, $"{TexFolder}/TireTread_Normal.png");
            Object.DestroyImmediate(albedo);
        }

        private static void GenerateGlassFacade(int size)
        {
            var albedo = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var height = new float[size * size];

            for (int y = 0; y < size; y++)
            {
                float v = (float)y / size;
                for (int x = 0; x < size; x++)
                {
                    float u = (float)x / size;
                    bool mullion = u < 0.05f || u > 0.95f || v < 0.06f || v > 0.94f || Mathf.Abs(u - 0.5f) < 0.025f;
                    if (mullion)
                    {
                        height[y * size + x] = 0.92f;
                        albedo.SetPixel(x, y, new Color(0.24f, 0.27f, 0.31f, 1f));
                    }
                    else
                    {
                        // Subtle realistic interior curtain / sky gradient reflection
                        float grad = Mathf.Lerp(0.11f, 0.24f, v);
                        float glint = Mathf.Exp(-Mathf.Pow((u + v - 1.15f) * 5.5f, 2f)) * 0.16f;
                        height[y * size + x] = 0.22f;
                        albedo.SetPixel(x, y, new Color(grad * 0.72f + glint, grad * 0.92f + glint, grad * 1.18f + glint, 1f));
                    }
                }
            }
            albedo.Apply();
            SavePng(albedo, $"{TexFolder}/WindowFacade_Albedo.png");
            SaveNormalFromHeight(height, size, 3.0f, $"{TexFolder}/WindowFacade_Normal.png");
            Object.DestroyImmediate(albedo);
        }

        private static void ConfigureImporters()
        {
            string[] normalPaths =
            {
                $"{TexFolder}/Tarmac_Normal.png",
                $"{TexFolder}/Murram_Normal.png",
                $"{TexFolder}/Cabro_Normal.png",
                $"{TexFolder}/Plaster_Normal.png",
                $"{TexFolder}/Mabati_Normal.png",
                $"{TexFolder}/Bark_Normal.png",
                $"{TexFolder}/TireTread_Normal.png",
                $"{TexFolder}/WindowFacade_Normal.png"
            };

            foreach (var p in normalPaths)
            {
                var imp = AssetImporter.GetAtPath(p) as TextureImporter;
                if (imp == null) continue;
                imp.textureType = TextureImporterType.NormalMap;
                imp.wrapMode = TextureWrapMode.Repeat;
                imp.filterMode = FilterMode.Trilinear;
                imp.anisoLevel = 8;
                imp.SaveAndReimport();
            }

            string[] albedoPaths =
            {
                $"{TexFolder}/Tarmac_Albedo.png",
                $"{TexFolder}/Murram_Albedo.png",
                $"{TexFolder}/Cabro_Albedo.png",
                $"{TexFolder}/Plaster_Albedo.png",
                $"{TexFolder}/Mabati_Albedo.png",
                $"{TexFolder}/Bark_Albedo.png",
                $"{TexFolder}/TireTread_Albedo.png",
                $"{TexFolder}/WindowFacade_Albedo.png"
            };

            foreach (var p in albedoPaths)
            {
                var imp = AssetImporter.GetAtPath(p) as TextureImporter;
                if (imp == null) continue;
                imp.textureType = TextureImporterType.Default;
                imp.wrapMode = TextureWrapMode.Repeat;
                imp.filterMode = FilterMode.Trilinear;
                imp.anisoLevel = 8;
                imp.SaveAndReimport();
            }

            var leafImp = AssetImporter.GetAtPath($"{TexFolder}/LeafCluster_Albedo.png") as TextureImporter;
            if (leafImp != null)
            {
                leafImp.textureType = TextureImporterType.Default;
                leafImp.alphaIsTransparency = true;
                leafImp.wrapMode = TextureWrapMode.Clamp;
                leafImp.filterMode = FilterMode.Trilinear;
                leafImp.anisoLevel = 4;
                leafImp.SaveAndReimport();
            }
        }

        private static void SaveNormalFromHeight(float[] h, int size, float strength, string path)
        {
            var normalTex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            {
                int ym = (y - 1 + size) % size;
                int yp = (y + 1) % size;
                for (int x = 0; x < size; x++)
                {
                    int xm = (x - 1 + size) % size;
                    int xp = (x + 1) % size;

                    float dX = (h[y * size + xp] - h[y * size + xm]) * strength;
                    float dY = (h[yp * size + x] - h[ym * size + x]) * strength;
                    var n = new Vector3(-dX, -dY, 1f).normalized;
                    normalTex.SetPixel(x, y, new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f));
                }
            }
            normalTex.Apply();
            SavePng(normalTex, path);
            Object.DestroyImmediate(normalTex);
        }

        private static void SavePng(Texture2D tex, string path)
        {
            File.WriteAllBytes(path, tex.EncodeToPNG());
        }

        private static void DrawLine(Color[] pixels, int size, float x0, float y0, float x1, float y1, float halfW, Color col)
        {
            int steps = Mathf.CeilToInt(Vector2.Distance(new Vector2(x0, y0), new Vector2(x1, y1)) * size * 2f);
            int rad = Mathf.Max(1, Mathf.RoundToInt(halfW * size));
            for (int i = 0; i <= steps; i++)
            {
                float t = (float)i / Mathf.Max(1, steps);
                int cx = Mathf.RoundToInt(Mathf.Lerp(x0, x1, t) * (size - 1));
                int cy = Mathf.RoundToInt(Mathf.Lerp(y0, y1, t) * (size - 1));
                for (int dy = -rad; dy <= rad; dy++)
                {
                    for (int dx = -rad; dx <= rad; dx++)
                    {
                        int px = cx + dx;
                        int py = cy + dy;
                        if (px >= 0 && px < size && py >= 0 && py < size && dx * dx + dy * dy <= rad * rad)
                        {
                            pixels[py * size + px] = col;
                        }
                    }
                }
            }
        }

        private static void DrawLeaf(Color[] pixels, int size, float cx, float cy, float rx, float ry, float rot, Color col)
        {
            float cos = Mathf.Cos(rot);
            float sin = Mathf.Sin(rot);
            int minX = Mathf.Max(0, Mathf.FloorToInt((cx - rx * 1.2f) * size));
            int maxX = Mathf.Min(size - 1, Mathf.CeilToInt((cx + rx * 1.2f) * size));
            int minY = Mathf.Max(0, Mathf.FloorToInt((cy - rx * 1.2f) * size));
            int maxY = Mathf.Min(size - 1, Mathf.CeilToInt((cy + rx * 1.2f) * size));

            for (int y = minY; y <= maxY; y++)
            {
                float dy = ((float)y / size) - cy;
                for (int x = minX; x <= maxX; x++)
                {
                    float dx = ((float)x / size) - cx;
                    float lx = (dx * cos + dy * sin) / rx;
                    float ly = (-dx * sin + dy * cos) / ry;
                    // Pointed almond leaf silhouette
                    float taper = 1f - 0.35f * Mathf.Abs(lx);
                    if (lx * lx + (ly / Mathf.Max(0.2f, taper)) * (ly / Mathf.Max(0.2f, taper)) <= 1f)
                    {
                        float vein = Mathf.Abs(ly) < 0.14f ? 1.08f : (1f - 0.12f * Mathf.Abs(ly));
                        pixels[y * size + x] = new Color(col.r * vein, col.g * vein, col.b * vein, 1f);
                    }
                }
            }
        }

        private static float TileFbm(float x, float y, float period, int octaves, int seed)
        {
            float sum = 0f;
            float amp = 0.5f;
            float norm = 0f;
            float p = period;
            for (int i = 0; i < octaves; i++)
            {
                sum += amp * TileValueNoise(x, y, Mathf.RoundToInt(p), seed + i * 37);
                norm += amp;
                x *= 2f;
                y *= 2f;
                p *= 2f;
                amp *= 0.5f;
            }
            return sum / norm;
        }

        private static float TileValueNoise(float x, float y, int period, int seed)
        {
            int xi = Mathf.FloorToInt(x);
            int yi = Mathf.FloorToInt(y);
            float fx = x - xi;
            float fy = y - yi;
            float ux = fx * fx * (3f - 2f * fx);
            float uy = fy * fy * (3f - 2f * fy);

            int x0 = ((xi % period) + period) % period;
            int y0 = ((yi % period) + period) % period;
            int x1 = (x0 + 1) % period;
            int y1 = (y0 + 1) % period;

            float a = Hash2(x0, y0, seed);
            float b = Hash2(x1, y0, seed);
            float c = Hash2(x0, y1, seed);
            float d = Hash2(x1, y1, seed);

            return Mathf.Lerp(Mathf.Lerp(a, b, ux), Mathf.Lerp(c, d, ux), uy);
        }

        private static float Hash2(int x, int y, int seed)
        {
            uint h = (uint)(x * 374761393 + y * 668265263 + seed * 1442695041);
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= (h >> 16);
            return (h & 0x00FFFFFFu) / 16777215f;
        }
    }
}
