using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace KenyaStreetSim.Editor
{
    /// <summary>
    /// Generates smooth, beveled, contoured high-detail 3D automotive meshes for the playable
    /// KCP 380D Kenya Police Land Cruiser 79 Patrol Truck:
    /// - Contoured 79-Series beveled hood & fender nose with raised power-bulge
    /// - Curved 16-segment semi-circular wheel-arch fender flares
    /// - Round tubular wrap-around steel bullbar (bent pipes, not cubes)
    /// - Curved cylindrical safari air snorkel sweeping smoothly up the left A-pillar
    /// - Arched & gently pleated draped Olive-Khaki canvas canopy roof
    /// - 28-segment lathed deep-tread 4x4 mud-terrain tire & concave dished steel rim
    /// - Aerodynamic beveled LED lightbar pods
    /// </summary>
    public static class KenyaPoliceLC79MeshFactory
    {
        public const string MeshDir = "Assets/KenyaStreetSim/Meshes/PoliceLC79";

        public static Mesh HoodNoseMesh { get; private set; }
        public static Mesh FenderArchFlareMesh { get; private set; }
        public static Mesh TubularBullbarMesh { get; private set; }
        public static Mesh CurvedSnorkelMesh { get; private set; }
        public static Mesh DrapedCanvasRoofMesh { get; private set; }
        public static Mesh OffroadTireMesh { get; private set; }
        public static Mesh DishedRimMesh { get; private set; }
        public static Mesh BeveledLightPodMesh { get; private set; }

        public static void BuildAllMeshes()
        {
            if (!Directory.Exists(MeshDir)) Directory.CreateDirectory(MeshDir);

            HoodNoseMesh = SaveMesh("LC79_SculptedHoodNose", BuildSculptedHoodNose());
            FenderArchFlareMesh = SaveMesh("LC79_CurvedFenderFlare", BuildCurvedFenderFlare());
            TubularBullbarMesh = SaveMesh("LC79_TubularBullbar", BuildTubularBullbar());
            CurvedSnorkelMesh = SaveMesh("LC79_CurvedSnorkel", BuildCurvedSnorkel());
            DrapedCanvasRoofMesh = SaveMesh("LC79_DrapedCanvasRoof", BuildDrapedCanvasRoof());
            OffroadTireMesh = SaveMesh("LC79_OffroadTire", BuildLathedTire(0.44f, 0.30f, 28));
            DishedRimMesh = SaveMesh("LC79_DishedRim", BuildDishedRim(0.26f, 0.28f, 24));
            BeveledLightPodMesh = SaveMesh("LC79_BeveledLightPod", BuildBeveledBox(0.48f, 0.17f, 0.27f, 0.045f));

            AssetDatabase.SaveAssets();
        }

        private static Mesh SaveMesh(string name, Mesh m)
        {
            m.name = name;
            string path = $"{MeshDir}/{name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                existing.Clear();
                existing.vertices = m.vertices;
                existing.uv = m.uv;
                existing.triangles = m.triangles;
                existing.normals = m.normals;
                existing.tangents = m.tangents;
                existing.RecalculateBounds();
                Object.DestroyImmediate(m);
                EditorUtility.SetDirty(existing);
                return existing;
            }
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        /// <summary>
        /// Lofts the classic Toyota Land Cruiser 79 sculpted front nose, beveled fenders, and raised hood power bulge.
        /// Spans Z = 1.08 (cowl) to Z = 2.52 (front grille fascia).
        /// </summary>
        private static Mesh BuildSculptedHoodNose()
        {
            var slices = new (float z, float hw, float botY, float shoulderY, float hoodEdgeY, float crownY, float bulgeW)[]
            {
                (1.08f, 0.91f, 0.72f, 1.32f, 1.43f, 1.49f, 0.44f),
                (1.26f, 0.91f, 0.72f, 1.32f, 1.42f, 1.48f, 0.44f),
                (1.78f, 0.90f, 0.72f, 1.30f, 1.39f, 1.45f, 0.42f),
                (2.26f, 0.88f, 0.72f, 1.26f, 1.34f, 1.39f, 0.38f),
                (2.44f, 0.85f, 0.74f, 1.22f, 1.29f, 1.32f, 0.34f),
                (2.51f, 0.80f, 0.78f, 1.16f, 1.22f, 1.24f, 0.28f),
            };

            const int ringN = 10;
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();

            for (int s = 0; s < slices.Length; s++)
            {
                var (z, hw, by, sy, ey, cy, bw) = slices[s];
                float v = (float)s / (slices.Length - 1);
                Vector3[] ring =
                {
                    new Vector3(-hw * 0.94f, by, z),
                    new Vector3(-hw,         by + 0.08f, z),
                    new Vector3(-hw,         sy, z),
                    new Vector3(-hw * 0.88f, ey, z),
                    new Vector3(-bw,         cy, z),
                    new Vector3( bw,         cy, z),
                    new Vector3( hw * 0.88f, ey, z),
                    new Vector3( hw,         sy, z),
                    new Vector3( hw,         by + 0.08f, z),
                    new Vector3( hw * 0.94f, by, z),
                };
                for (int r = 0; r < ringN; r++)
                {
                    verts.Add(ring[r]);
                    uvs.Add(new Vector2((float)r / (ringN - 1), v));
                }
            }

            for (int s = 0; s < slices.Length - 1; s++)
            {
                int a0 = s * ringN;
                int b0 = (s + 1) * ringN;
                for (int r = 0; r < ringN; r++)
                {
                    int rn = (r + 1) % ringN;
                    tris.Add(a0 + r); tris.Add(a0 + rn); tris.Add(b0 + rn);
                    tris.Add(a0 + r); tris.Add(b0 + rn); tris.Add(b0 + r);
                }
            }

            // Cap front and back cleanly
            CapRing(verts, uvs, tris, 0, ringN, flip: true);
            CapRing(verts, uvs, tris, (slices.Length - 1) * ringN, ringN, flip: false);

            return FinalizeMesh(verts, uvs, tris);
        }

        /// <summary>
        /// Semi-circular extruded 4x4 wheel-arch fender flare centered at origin in local space (radius ~0.50m).
        /// Mount at (side * 0.91, wheelY, wheelZ), scale X by side sign.
        /// </summary>
        private static Mesh BuildCurvedFenderFlare()
        {
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();

            const int segs = 16;
            float rIn = 0.49f;
            float rOut = 0.575f;
            float w = 0.14f;

            for (int i = 0; i <= segs; i++)
            {
                float t = (float)i / segs;
                float ang = Mathf.Lerp(Mathf.PI * 0.06f, Mathf.PI * 0.94f, t);
                float c = Mathf.Cos(ang);
                float s = Mathf.Sin(ang);

                // 4-vertex cross-section around arch rim (inner-in, outer-in, outer-out, inner-out)
                verts.Add(new Vector3(0f,      s * rIn,  c * rIn));
                verts.Add(new Vector3(0f,      s * rOut, c * rOut));
                verts.Add(new Vector3(w,       s * rOut * 0.97f, c * rOut * 0.97f));
                verts.Add(new Vector3(w * 0.88f, s * rIn,  c * rIn));

                for (int k = 0; k < 4; k++) uvs.Add(new Vector2(t, k * 0.33f));
            }

            for (int i = 0; i < segs; i++)
            {
                int a = i * 4;
                int b = (i + 1) * 4;
                for (int k = 0; k < 4; k++)
                {
                    int kn = (k + 1) % 4;
                    tris.Add(a + k); tris.Add(b + kn); tris.Add(a + kn);
                    tris.Add(a + k); tris.Add(b + k);  tris.Add(b + kn);
                }
            }
            return FinalizeMesh(verts, uvs, tris);
        }

        /// <summary>
        /// Classic Kenyan Police 79-Series wrap-around round-tube steel bullbar (smooth bent steel pipes!).
        /// </summary>
        private static Mesh BuildTubularBullbar()
        {
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();

            // Main curved top hoop wrapping across hood & headlights
            Vector3[] topHoop =
            {
                new Vector3(-0.95f, 0.72f, 2.48f),
                new Vector3(-0.95f, 1.18f, 2.58f),
                new Vector3(-0.84f, 1.28f, 2.66f),
                new Vector3(-0.46f, 1.30f, 2.68f),
                new Vector3( 0.46f, 1.30f, 2.68f),
                new Vector3( 0.84f, 1.28f, 2.66f),
                new Vector3( 0.95f, 1.18f, 2.58f),
                new Vector3( 0.95f, 0.72f, 2.48f),
            };
            AddPolylineTube(verts, uvs, tris, topHoop, 0.036f, 10);

            // Mid crossbar across headlight guards
            Vector3[] midBar =
            {
                new Vector3(-0.95f, 0.98f, 2.56f),
                new Vector3(-0.86f, 0.98f, 2.66f),
                new Vector3( 0.86f, 0.98f, 2.66f),
                new Vector3( 0.95f, 0.98f, 2.56f),
            };
            AddPolylineTube(verts, uvs, tris, midBar, 0.030f, 10);

            // Center twin upright winch-frame stanchions
            AddPolylineTube(verts, uvs, tris, new[] { new Vector3(-0.44f, 0.66f, 2.64f), new Vector3(-0.44f, 1.30f, 2.68f) }, 0.036f, 10);
            AddPolylineTube(verts, uvs, tris, new[] { new Vector3( 0.44f, 0.66f, 2.64f), new Vector3( 0.44f, 1.30f, 2.68f) }, 0.036f, 10);

            // Lower heavy bumper horizontal protection tube
            Vector3[] lowBar =
            {
                new Vector3(-0.98f, 0.68f, 2.44f),
                new Vector3(-0.90f, 0.68f, 2.64f),
                new Vector3( 0.90f, 0.68f, 2.64f),
                new Vector3( 0.98f, 0.68f, 2.44f),
            };
            AddPolylineTube(verts, uvs, tris, lowBar, 0.055f, 12);

            return FinalizeMesh(verts, uvs, tris);
        }

        /// <summary>
        /// Smooth swept cylindrical 4x4 safari snorkel hugging the left fender & A-pillar.
        /// </summary>
        private static Mesh BuildCurvedSnorkel()
        {
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();

            Vector3[] path =
            {
                new Vector3(-0.90f, 1.24f, 1.88f),
                new Vector3(-0.97f, 1.26f, 1.72f),
                new Vector3(-0.97f, 1.28f, 1.30f),
                new Vector3(-0.95f, 1.42f, 1.15f),
                new Vector3(-0.93f, 1.78f, 1.05f),
                new Vector3(-0.92f, 2.16f, 0.96f),
                new Vector3(-0.92f, 2.22f, 1.06f),
            };
            AddPolylineTube(verts, uvs, tris, path, 0.052f, 12);
            return FinalizeMesh(verts, uvs, tris);
        }

        /// <summary>
        /// Smoothly arched, draped & subtly pleated Olive-Khaki canvas canopy roof spanning the rear troop bed.
        /// Uses separate top and bottom vertex layers (3cm thickness) so normals stay pure smooth & artifact-free!
        /// </summary>
        private static Mesh BuildDrapedCanvasRoof()
        {
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();

            const int segX = 14;
            const int segZ = 18;
            float halfW = 0.91f;
            float zMin = -2.44f;
            float zMax = -0.14f;
            int row = segX + 1;
            int layerCount = (segZ + 1) * row;

            // Layer 0: Top canvas surface (+Y normals), Layer 1: Underside canvas lining (-Y normals, 3cm lower)
            for (int layer = 0; layer < 2; layer++)
            {
                float yOff = layer == 0 ? 0f : -0.032f;
                for (int iz = 0; iz <= segZ; iz++)
                {
                    float vz = (float)iz / segZ;
                    float z = Mathf.Lerp(zMin, zMax, vz);
                    float dHoop = Mathf.Min(Mathf.Abs(z + 2.34f), Mathf.Min(Mathf.Abs(z + 1.26f), Mathf.Abs(z + 0.22f)));
                    float sag = -0.022f * Mathf.Clamp01(dHoop / 0.55f);

                    for (int ix = 0; ix <= segX; ix++)
                    {
                        float ux = (float)ix / segX;
                        float nx = (ux - 0.5f) * 2f;
                        float x = nx * halfW;
                        float arch = (1f - nx * nx) * 0.14f;
                        float edgeDrop = Mathf.Pow(Mathf.Abs(nx), 6f) * 0.08f;
                        float y = 2.34f + arch - edgeDrop + sag * (1f - nx * nx * 0.5f) + yOff;
                        verts.Add(new Vector3(x, y, z));
                        uvs.Add(new Vector2(ux * 2f, vz * 2.5f));
                    }
                }
            }

            // Top surface (+Y outward winding in Unity left-handed coords: i00 -> i01 -> i11, i00 -> i11 -> i10)
            for (int iz = 0; iz < segZ; iz++)
            {
                for (int ix = 0; ix < segX; ix++)
                {
                    int i00 = iz * row + ix;
                    int i10 = i00 + 1;
                    int i01 = i00 + row;
                    int i11 = i01 + 1;
                    tris.Add(i00); tris.Add(i01); tris.Add(i11);
                    tris.Add(i00); tris.Add(i11); tris.Add(i10);

                    int b00 = layerCount + i00;
                    int b10 = layerCount + i10;
                    int b01 = layerCount + i01;
                    int b11 = layerCount + i11;
                    tris.Add(b00); tris.Add(b11); tris.Add(b01);
                    tris.Add(b00); tris.Add(b10); tris.Add(b11);
                }
            }
            return FinalizeMesh(verts, uvs, tris);
        }

        /// <summary>
        /// 28-segment smooth lathed off-road mud-terrain tire (axle along local X) with rounded sidewall shoulders!
        /// </summary>
        private static Mesh BuildLathedTire(float outerR, float width, int radialSegs)
        {
            float hw = width * 0.5f;
            float rRim = outerR * 0.56f;
            // Profile points (x along axle, radius from axle)
            (float x, float r)[] prof =
            {
                (-hw * 0.82f, rRim),
                (-hw,         outerR * 0.76f),
                (-hw * 0.96f, outerR * 0.95f),
                (-hw * 0.78f, outerR),
                ( 0f,         outerR * 1.015f),
                ( hw * 0.78f, outerR),
                ( hw * 0.96f, outerR * 0.95f),
                ( hw,         outerR * 0.76f),
                ( hw * 0.82f, rRim),
            };
            return BuildLatheX(prof, radialSegs);
        }

        private static Mesh BuildDishedRim(float rOuter, float width, int radialSegs)
        {
            float hw = width * 0.5f;
            (float x, float r)[] prof =
            {
                (-hw * 0.72f, rOuter),
                (-hw * 0.64f, rOuter * 0.82f),
                (-hw * 0.35f, rOuter * 0.72f),
                (-hw * 0.35f, 0.01f),
                ( hw * 0.35f, 0.01f),
                ( hw * 0.35f, rOuter * 0.72f),
                ( hw * 0.64f, rOuter * 0.82f),
                ( hw * 0.72f, rOuter),
            };
            return BuildLatheX(prof, radialSegs);
        }

        private static Mesh BuildLatheX((float x, float r)[] profile, int segs)
        {
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            int pn = profile.Length;

            for (int s = 0; s <= segs; s++)
            {
                float u = (float)s / segs;
                float ang = u * Mathf.PI * 2f;
                float c = Mathf.Cos(ang);
                float sn = Mathf.Sin(ang);
                for (int p = 0; p < pn; p++)
                {
                    verts.Add(new Vector3(profile[p].x, sn * profile[p].r, c * profile[p].r));
                    uvs.Add(new Vector2(u * 4f, (float)p / (pn - 1)));
                }
            }

            for (int s = 0; s < segs; s++)
            {
                int a = s * pn;
                int b = (s + 1) * pn;
                for (int p = 0; p < pn - 1; p++)
                {
                    // Outward-facing CW winding in Unity left-handed coords around +X axle
                    tris.Add(a + p); tris.Add(a + p + 1); tris.Add(b + p + 1);
                    tris.Add(a + p); tris.Add(b + p + 1); tris.Add(b + p);
                }
            }
            return FinalizeMesh(verts, uvs, tris);
        }

        private static Mesh BuildBeveledBox(float w, float h, float d, float b)
        {
            float hw = w * 0.5f;
            float hh = h * 0.5f;
            float hd = d * 0.5f;
            float[] zs = { -hd, -hd + b, hd - b, hd };
            float[] sc = { 0.82f, 1.0f, 1.0f, 0.82f };
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            const int ringN = 8;

            for (int s = 0; s < zs.Length; s++)
            {
                float mw = hw * sc[s];
                float mh = hh * sc[s];
                float bb = b * sc[s];
                float z = zs[s];
                Vector3[] ring =
                {
                    new Vector3(-mw + bb, -mh, z),
                    new Vector3(-mw, -mh + bb, z),
                    new Vector3(-mw,  mh - bb, z),
                    new Vector3(-mw + bb,  mh, z),
                    new Vector3( mw - bb,  mh, z),
                    new Vector3( mw,  mh - bb, z),
                    new Vector3( mw, -mh + bb, z),
                    new Vector3( mw - bb, -mh, z),
                };
                for (int r = 0; r < ringN; r++)
                {
                    verts.Add(ring[r]);
                    uvs.Add(new Vector2((float)r / ringN, (float)s / (zs.Length - 1)));
                }
            }

            for (int s = 0; s < zs.Length - 1; s++)
            {
                int a = s * ringN;
                int c = (s + 1) * ringN;
                for (int r = 0; r < ringN; r++)
                {
                    int rn = (r + 1) % ringN;
                    tris.Add(a + r); tris.Add(a + rn); tris.Add(c + rn);
                    tris.Add(a + r); tris.Add(c + rn); tris.Add(c + r);
                }
            }
            CapRing(verts, uvs, tris, 0, ringN, flip: true);
            CapRing(verts, uvs, tris, (zs.Length - 1) * ringN, ringN, flip: false);
            return FinalizeMesh(verts, uvs, tris);
        }

        private static void AddPolylineTube(List<Vector3> verts, List<Vector2> uvs, List<int> tris, Vector3[] pts, float radius, int sides)
        {
            if (pts.Length < 2) return;
            int baseIdx = verts.Count;
            Vector3 prevUp = Vector3.up;

            for (int i = 0; i < pts.Length; i++)
            {
                Vector3 fwd;
                if (i == 0) fwd = (pts[1] - pts[0]).normalized;
                else if (i == pts.Length - 1) fwd = (pts[i] - pts[i - 1]).normalized;
                else fwd = ((pts[i + 1] - pts[i]).normalized + (pts[i] - pts[i - 1]).normalized).normalized;
                if (fwd.sqrMagnitude < 0.001f) fwd = Vector3.forward;

                Vector3 right = Vector3.Cross(prevUp, fwd);
                if (right.sqrMagnitude < 0.001f) right = Vector3.Cross(Vector3.right, fwd);
                right.Normalize();
                Vector3 up = Vector3.Cross(fwd, right).normalized;
                prevUp = up;

                float v = (float)i / (pts.Length - 1);
                for (int s = 0; s <= sides; s++)
                {
                    float ang = ((float)s / sides) * Mathf.PI * 2f;
                    Vector3 offset = (right * Mathf.Cos(ang) + up * Mathf.Sin(ang)) * radius;
                    verts.Add(pts[i] + offset);
                    uvs.Add(new Vector2((float)s / sides, v));
                }
            }

            int stride = sides + 1;
            for (int i = 0; i < pts.Length - 1; i++)
            {
                int a = baseIdx + i * stride;
                int b = baseIdx + (i + 1) * stride;
                for (int s = 0; s < sides; s++)
                {
                    tris.Add(a + s); tris.Add(b + s + 1); tris.Add(a + s + 1);
                    tris.Add(a + s); tris.Add(b + s);     tris.Add(b + s + 1);
                }
            }
        }

        private static void CapRing(List<Vector3> verts, List<Vector2> uvs, List<int> tris, int ringStart, int ringN, bool flip)
        {
            Vector3 center = Vector3.zero;
            for (int i = 0; i < ringN; i++) center += verts[ringStart + i];
            center /= ringN;
            int cIdx = verts.Count;
            verts.Add(center);
            uvs.Add(new Vector2(0.5f, 0.5f));
            for (int i = 0; i < ringN; i++)
            {
                int a = ringStart + i;
                int b = ringStart + ((i + 1) % ringN);
                if (flip) { tris.Add(cIdx); tris.Add(b); tris.Add(a); }
                else { tris.Add(cIdx); tris.Add(a); tris.Add(b); }
            }
        }

        private static Mesh FinalizeMesh(List<Vector3> verts, List<Vector2> uvs, List<int> tris)
        {
            var m = new Mesh();
            m.SetVertices(verts);
            m.SetUVs(0, uvs);
            m.SetTriangles(tris, 0);
            m.RecalculateNormals();
            m.RecalculateTangents();
            m.RecalculateBounds();
            return m;
        }
    }
}
