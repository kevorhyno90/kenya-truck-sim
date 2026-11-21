using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace KenyaStreetSim.Editor
{
    /// <summary>
    /// Builds and persists smooth, sculpted UV-mapped Unity Mesh assets so vehicles, trees,
    /// and the surrounding Kenyan Ngong Hills / Rift Valley landscape look realistic rather than boxy primitives.
    /// </summary>
    public static class KenyaSculptedMeshFactory
    {
        public const string MeshFolder = "Assets/KenyaStreetSim/Meshes";

        public static Mesh EnaCoachUpperMesh { get; private set; }
        public static Mesh EnaCoachSkirtMesh { get; private set; }
        public static Mesh EnaCoachWindshieldMesh { get; private set; }
        public static Mesh IsuzuCabMesh { get; private set; }
        public static Mesh MatatuBodyMesh { get; private set; }
        public static Mesh SedanBodyMesh { get; private set; }
        public static Mesh TreeTrunkMesh { get; private set; }
        public static Mesh TreeFoliageMesh { get; private set; }
        public static Mesh PalmTrunkMesh { get; private set; }
        public static Mesh PalmCrownMesh { get; private set; }
        public static Mesh RiftValleyTerrainMesh { get; private set; }

        public static void BuildAllMeshes()
        {
            if (!Directory.Exists(MeshFolder))
            {
                Directory.CreateDirectory(MeshFolder);
            }

            EnaCoachUpperMesh = SaveMesh("M_EnaCoachUpper", BuildLoftedBoxProfile(
                new[]
                {
                    // (z, halfWidth, bottomY, topY, roofInset) — solid aerodynamic rear-to-A-pillar coach body!
                    (-5.82f, 1.24f, 1.56f, 3.30f, 0.14f),
                    (-5.55f, 1.27f, 1.56f, 3.44f, 0.11f),
                    ( 4.25f, 1.27f, 1.56f, 3.44f, 0.11f),
                    ( 4.65f, 1.25f, 1.56f, 3.42f, 0.12f)
                }));

            EnaCoachSkirtMesh = SaveMesh("M_EnaCoachSkirt", BuildLoftedBoxProfile(
                new[]
                {
                    (-5.84f, 1.23f, 0.56f, 1.58f, 0.02f),
                    (-5.55f, 1.28f, 0.52f, 1.58f, 0.00f),
                    ( 5.45f, 1.28f, 0.52f, 1.58f, 0.00f),
                    ( 5.84f, 1.20f, 0.58f, 1.58f, 0.04f),
                    ( 5.95f, 1.08f, 0.68f, 1.52f, 0.06f)
                }));

            // Sleek aerodynamic sloped front roof cap above the windshield (keeps windshield completely clear for RHD Cockpit!)
            EnaCoachWindshieldMesh = SaveMesh("M_EnaCoachRoofBrow", BuildLoftedBoxProfile(
                new[]
                {
                    ( 4.55f, 1.25f, 2.94f, 3.42f, 0.12f),
                    ( 5.38f, 1.23f, 2.94f, 3.36f, 0.15f),
                    ( 5.72f, 1.18f, 2.86f, 3.16f, 0.18f)
                }));

            IsuzuCabMesh = SaveMesh("M_IsuzuForwardCab", BuildLoftedBoxProfile(
                new[]
                {
                    ( 1.92f, 1.14f, 0.80f, 2.86f, 0.10f),
                    ( 3.72f, 1.14f, 0.80f, 2.86f, 0.10f),
                    ( 4.10f, 1.10f, 0.80f, 2.68f, 0.14f),
                    ( 4.26f, 1.04f, 0.86f, 1.95f, 0.12f)
                }));

            MatatuBodyMesh = SaveMesh("M_KenyanMatatuBody", BuildLoftedBoxProfile(
                new[]
                {
                    (-2.55f, 0.94f, 0.42f, 2.18f, 0.10f),
                    (-2.35f, 0.98f, 0.40f, 2.26f, 0.09f),
                    ( 1.75f, 0.98f, 0.40f, 2.26f, 0.09f),
                    ( 2.30f, 0.94f, 0.42f, 2.05f, 0.14f),
                    ( 2.58f, 0.86f, 0.48f, 1.32f, 0.10f)
                }));

            SedanBodyMesh = SaveMesh("M_KenyanSedanBody", BuildLoftedBoxProfile(
                new[]
                {
                    (-2.22f, 0.84f, 0.36f, 1.02f, 0.08f),
                    (-1.82f, 0.91f, 0.34f, 1.12f, 0.08f),
                    (-1.25f, 0.91f, 0.34f, 1.54f, 0.18f),
                    ( 0.45f, 0.91f, 0.34f, 1.55f, 0.18f),
                    ( 1.18f, 0.90f, 0.34f, 1.06f, 0.08f),
                    ( 2.10f, 0.85f, 0.36f, 0.92f, 0.10f),
                    ( 2.26f, 0.74f, 0.42f, 0.72f, 0.10f)
                }));

            TreeTrunkMesh = SaveMesh("M_AcaciaTreeTrunk", BuildBranchingTrunkMesh());
            TreeFoliageMesh = SaveMesh("M_AcaciaTreeFoliage", BuildLeafyFoliageCardsMesh());
            PalmTrunkMesh = SaveMesh("M_KenyaPalmTrunk", BuildPalmTrunkMesh());
            PalmCrownMesh = SaveMesh("M_KenyaPalmCrown", BuildPalmCrownMesh());
            RiftValleyTerrainMesh = SaveMesh("M_KenyanRiftValleyTerrain", BuildFullKenyaTerrain());

            AssetDatabase.SaveAssets();
        }

        private static Mesh SaveMesh(string name, Mesh mesh)
        {
            mesh.name = name;
            string path = $"{MeshFolder}/{name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                existing.Clear();
                existing.indexFormat = mesh.indexFormat;
                existing.vertices = mesh.vertices;
                existing.uv = mesh.uv;
                existing.triangles = mesh.triangles;
                existing.normals = mesh.normals;
                existing.tangents = mesh.tangents;
                existing.RecalculateBounds();
                Object.DestroyImmediate(mesh);
                EditorUtility.SetDirty(existing);
                return existing;
            }
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        /// <summary>
        /// Lofts a clean, bevel-crowned 8-vertex cross-section along Z slices to produce smooth automotive silhouettes.
        /// </summary>
        private static Mesh BuildLoftedBoxProfile((float z, float hw, float botY, float topY, float inset)[] slices)
        {
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();

            const int ringVerts = 8; // chamfered octagon ring
            for (int s = 0; s < slices.Length; s++)
            {
                var (z, hw, by, ty, inset) = slices[s];
                float midTopY = ty - inset * 0.85f;
                float botChamfY = by + Mathf.Min(0.14f, (ty - by) * 0.15f);
                float botInset = Mathf.Min(0.08f, hw * 0.08f);

                // Clockwise looking down +Z
                Vector3[] ring =
                {
                    new Vector3(-hw + botInset, by, z),
                    new Vector3(-hw, botChamfY, z),
                    new Vector3(-hw, midTopY, z),
                    new Vector3(-hw + inset, ty, z),
                    new Vector3( hw - inset, ty, z),
                    new Vector3( hw, midTopY, z),
                    new Vector3( hw, botChamfY, z),
                    new Vector3( hw - botInset, by, z)
                };

                float vCoord = (float)s / Mathf.Max(1, slices.Length - 1);
                for (int r = 0; r < ringVerts; r++)
                {
                    verts.Add(ring[r]);
                    uvs.Add(new Vector2((float)r / ringVerts, vCoord));
                }
            }

            // Connect consecutive rings with verified OUTWARD-facing left-handed Unity winding!
            for (int s = 0; s < slices.Length - 1; s++)
            {
                int a0 = s * ringVerts;
                int b0 = (s + 1) * ringVerts;
                for (int r = 0; r < ringVerts; r++)
                {
                    int rn = (r + 1) % ringVerts;
                    tris.Add(a0 + r);
                    tris.Add(b0 + r);
                    tris.Add(b0 + rn);

                    tris.Add(a0 + r);
                    tris.Add(b0 + rn);
                    tris.Add(a0 + rn);
                }
            }

            // Dedicated flat-normal Rear Cap (-Z) so rear bus wall renders crisp, bright, and solid!
            int rearCenter = verts.Count;
            var first = slices[0];
            verts.Add(new Vector3(0f, (first.botY + first.topY) * 0.5f, first.z));
            uvs.Add(new Vector2(0.5f, 0.5f));
            int rearRingStart = verts.Count;
            for (int r = 0; r < ringVerts; r++)
            {
                verts.Add(verts[r]);
                uvs.Add(new Vector2(0.5f, 0.5f));
            }
            for (int r = 0; r < ringVerts; r++)
            {
                int rn = (r + 1) % ringVerts;
                tris.Add(rearCenter);
                tris.Add(rearRingStart + r);
                tris.Add(rearRingStart + rn);
            }

            // Dedicated flat-normal Front Cap (+Z)
            int frontCenter = verts.Count;
            int lastStart = (slices.Length - 1) * ringVerts;
            var last = slices[slices.Length - 1];
            verts.Add(new Vector3(0f, (last.botY + last.topY) * 0.5f, last.z));
            uvs.Add(new Vector2(0.5f, 0.5f));
            int frontRingStart = verts.Count;
            for (int r = 0; r < ringVerts; r++)
            {
                verts.Add(verts[lastStart + r]);
                uvs.Add(new Vector2(0.5f, 0.5f));
            }
            for (int r = 0; r < ringVerts; r++)
            {
                int rn = (r + 1) % ringVerts;
                tris.Add(frontCenter);
                tris.Add(frontRingStart + rn);
                tris.Add(frontRingStart + r);
            }

            var mesh = new Mesh();
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh BuildBranchingTrunkMesh()
        {
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();

            // Main slightly curved tapered trunk
            AddTaperedTube(verts, uvs, tris, new Vector3(0f, 0f, 0f), new Vector3(0.08f, 2.75f, 0.04f), 0.24f, 0.14f, 7);

            // 5 forked Kenyan acacia/shade upper boughs
            Vector3 fork = new Vector3(0.06f, 2.35f, 0.03f);
            Vector3[] tips =
            {
                new Vector3( 1.65f, 4.10f,  0.55f),
                new Vector3(-1.55f, 4.20f,  0.65f),
                new Vector3( 0.45f, 4.35f, -1.60f),
                new Vector3(-1.10f, 3.95f, -1.25f),
                new Vector3( 0.15f, 4.75f,  0.30f)
            };

            for (int i = 0; i < tips.Length; i++)
            {
                AddTaperedTube(verts, uvs, tris, fork, tips[i], 0.11f, 0.04f, 6);
            }

            var mesh = new Mesh();
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh BuildLeafyFoliageCardsMesh()
        {
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();

            // 34 overlapping alpha-clipped organic leaf-cluster cards forming a realistic broad Kenyan acacia crown
            var rng = new System.Random(88421);
            Vector3[] centers =
            {
                new Vector3( 1.55f, 4.15f,  0.55f),
                new Vector3(-1.50f, 4.25f,  0.60f),
                new Vector3( 0.45f, 4.35f, -1.55f),
                new Vector3(-1.05f, 4.00f, -1.20f),
                new Vector3( 0.15f, 4.75f,  0.25f),
                new Vector3( 0.95f, 4.55f, -0.55f),
                new Vector3(-0.75f, 4.55f,  0.95f),
                new Vector3( 0.00f, 4.20f,  1.25f),
                new Vector3( 1.15f, 3.95f,  1.15f),
                new Vector3(-1.25f, 4.35f, -0.25f),
                new Vector3( 0.00f, 5.05f, -0.20f)
            };

            foreach (var c in centers)
            {
                // 3 intersecting angled quads per branch cluster for full 3D depth from every camera angle
                for (int q = 0; q < 3; q++)
                {
                    float yaw = q * 60f + (float)(rng.NextDouble() * 35.0);
                    float pitch = (float)(rng.NextDouble() * 48.0 - 24.0);
                    float roll = (float)(rng.NextDouble() * 36.0 - 18.0);
                    Quaternion rot = Quaternion.Euler(pitch, yaw, roll);
                    float size = 1.45f + (float)rng.NextDouble() * 0.55f;
                    Vector3 jitter = new Vector3((float)rng.NextDouble() - 0.5f, ((float)rng.NextDouble() - 0.5f) * 0.45f, (float)rng.NextDouble() - 0.5f) * 0.55f;
                    AddQuad(verts, uvs, tris, c + jitter, rot, size, size);
                }
            }

            var mesh = new Mesh();
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh BuildPalmTrunkMesh()
        {
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            // Gently curving coastal Mombasa / Lake Victoria palm trunk (8.2m tall)
            Vector3[] spine =
            {
                new Vector3(0.00f, 0.0f, 0.00f),
                new Vector3(0.18f, 2.2f, 0.08f),
                new Vector3(0.42f, 4.6f, 0.14f),
                new Vector3(0.55f, 6.8f, 0.16f),
                new Vector3(0.45f, 8.2f, 0.14f)
            };
            float[] radii = { 0.28f, 0.23f, 0.19f, 0.16f, 0.13f };
            for (int i = 0; i < spine.Length - 1; i++)
            {
                AddTaperedTube(verts, uvs, tris, spine[i], spine[i + 1], radii[i], radii[i + 1], 7);
            }
            var mesh = new Mesh();
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh BuildPalmCrownMesh()
        {
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            Vector3 top = new Vector3(0.45f, 8.15f, 0.14f);
            // 10 gracefully arching tropical palm fronds
            for (int i = 0; i < 10; i++)
            {
                float yaw = i * 36f + (i % 2) * 12f;
                float pitch = (i % 2 == 0) ? 28f : 52f;
                Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
                Vector3 frondMid = top + rot * new Vector3(0f, 0.45f, 1.85f);
                AddQuad(verts, uvs, tris, frondMid, rot * Quaternion.Euler(85f, 0f, 0f), 1.35f, 3.6f);
            }
            var mesh = new Mesh();
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh BuildFullKenyaTerrain()
        {
            const int resX = 140;
            const int resZ = 96;
            const float minX = -1520f;
            const float maxX = 1080f;
            const float minZ = -520f;
            const float maxZ = 980f;

            // Pre-sample all highway segments into coarse 2D polyline segments for blazing fast distance checks
            var roadSegs = new List<(Vector2 a, Vector2 b)>();
            for (int h = 0; h < KenyaNetworkData.Highways.Length; h++)
            {
                var pts = KenyaNetworkData.SampleHighway(KenyaNetworkData.Highways[h], 24f);
                for (int i = 0; i < pts.Count - 1; i++)
                {
                    roadSegs.Add((new Vector2(pts[i].x, pts[i].z), new Vector2(pts[i + 1].x, pts[i + 1].z)));
                }
            }

            var verts = new Vector3[(resX + 1) * (resZ + 1)];
            var uvs = new Vector2[verts.Length];
            var tris = new int[resX * resZ * 6];

            for (int iz = 0; iz <= resZ; iz++)
            {
                float vz = (float)iz / resZ;
                float wz = Mathf.Lerp(minZ, maxZ, vz);

                for (int ix = 0; ix <= resX; ix++)
                {
                    float ux = (float)ix / resX;
                    float wx = Mathf.Lerp(minX, maxX, ux);
                    Vector2 p = new Vector2(wx, wz);

                    // Compute shortest distance to ANY Kenyan highway, roundabout, or town center!
                    float minDist = float.MaxValue;
                    for (int s = 0; s < roadSegs.Count; s++)
                    {
                        var (a, b) = roadSegs[s];
                        Vector2 ab = b - a;
                        float lenSq = ab.sqrMagnitude;
                        float t = lenSq > 0.001f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / lenSq) : 0f;
                        float d = Vector2.Distance(p, a + ab * t);
                        if (d < minDist) minDist = d;
                    }
                    for (int h = 0; h < KenyaNetworkData.Hubs.Length; h++)
                    {
                        float dTown = Vector2.Distance(p, KenyaNetworkData.Hubs[h].xz) - 34f;
                        if (dTown < minDist) minDist = Mathf.Max(0f, dTown);
                    }
                    for (int r = 0; r < KenyaNetworkData.Roundabouts.Length; r++)
                    {
                        float dRbt = Vector2.Distance(p, KenyaNetworkData.Roundabouts[r].center) - 32f;
                        if (dRbt < minDist) minDist = Mathf.Max(0f, dRbt);
                    }

                    // Keep flat below Lake Victoria (x < -1190, z > 420) and Indian Ocean (x > 730, z < -180)
                    bool inLakeVictoria = (wx < -1180f && wz > 390f);
                    bool inIndianOcean = (wx > 740f && wz < -170f);

                    float y;
                    if (inLakeVictoria || inIndianOcean)
                    {
                        y = -3.8f;
                    }
                    else
                    {
                        float hillMask = SmoothRange(42f, 155f, minDist);
                        // Taller escarpment hills around Rift Valley / Kericho / Ngong Hills
                        float highlandBoost = (wx < -180f && wz > 120f) ? 1.45f : 1.0f;
                        float n1 = Mathf.PerlinNoise(wx * 0.0028f + 13.7f, wz * 0.0028f + 29.1f);
                        float n2 = Mathf.PerlinNoise(wx * 0.0085f + 71.3f, wz * 0.0085f + 41.9f);
                        float ridge = (Mathf.Pow(n1, 1.35f) * 52f + n2 * 14f) * highlandBoost;
                        y = -0.35f + ridge * hillMask;
                    }

                    int idx = iz * (resX + 1) + ix;
                    verts[idx] = new Vector3(wx, y, wz);
                    uvs[idx] = new Vector2(wx * 0.045f, wz * 0.045f);
                }
            }

            int ti = 0;
            for (int iz = 0; iz < resZ; iz++)
            {
                for (int ix = 0; ix < resX; ix++)
                {
                    int i00 = iz * (resX + 1) + ix;
                    int i10 = i00 + 1;
                    int i01 = i00 + (resX + 1);
                    int i11 = i01 + 1;

                    tris[ti++] = i00;
                    tris[ti++] = i01;
                    tris[ti++] = i10;

                    tris[ti++] = i10;
                    tris[ti++] = i01;
                    tris[ti++] = i11;
                }
            }

            var mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static float SmoothRange(float edge0, float edge1, float value)
        {
            float t = Mathf.InverseLerp(edge0, edge1, value);
            return t * t * (3f - 2f * t);
        }

        private static void AddTaperedTube(List<Vector3> verts, List<Vector2> uvs, List<int> tris, Vector3 a, Vector3 b, float rA, float rB, int sides)
        {
            Vector3 axis = (b - a).normalized;
            Vector3 ortho1 = Vector3.Cross(axis, Mathf.Abs(axis.y) > 0.9f ? Vector3.right : Vector3.up).normalized;
            Vector3 ortho2 = Vector3.Cross(axis, ortho1).normalized;

            int baseIdx = verts.Count;
            for (int s = 0; s <= sides; s++)
            {
                float ang = ((float)s / sides) * Mathf.PI * 2f;
                Vector3 radial = ortho1 * Mathf.Cos(ang) + ortho2 * Mathf.Sin(ang);
                verts.Add(a + radial * rA);
                uvs.Add(new Vector2((float)s / sides, 0f));
                verts.Add(b + radial * rB);
                uvs.Add(new Vector2((float)s / sides, 1f));
            }

            for (int s = 0; s < sides; s++)
            {
                int i0 = baseIdx + s * 2;
                int i1 = i0 + 1;
                int i2 = i0 + 2;
                int i3 = i0 + 3;

                tris.Add(i0);
                tris.Add(i1);
                tris.Add(i3);

                tris.Add(i0);
                tris.Add(i3);
                tris.Add(i2);
            }
        }

        private static void AddQuad(List<Vector3> verts, List<Vector2> uvs, List<int> tris, Vector3 center, Quaternion rot, float w, float h)
        {
            int idx = verts.Count;
            float hw = w * 0.5f;
            float hh = h * 0.5f;
            verts.Add(center + rot * new Vector3(-hw, -hh, 0f));
            verts.Add(center + rot * new Vector3( hw, -hh, 0f));
            verts.Add(center + rot * new Vector3( hw,  hh, 0f));
            verts.Add(center + rot * new Vector3(-hw,  hh, 0f));

            uvs.Add(new Vector2(0f, 0f));
            uvs.Add(new Vector2(1f, 0f));
            uvs.Add(new Vector2(1f, 1f));
            uvs.Add(new Vector2(0f, 1f));

            tris.Add(idx);
            tris.Add(idx + 2);
            tris.Add(idx + 1);
            tris.Add(idx);
            tris.Add(idx + 3);
            tris.Add(idx + 2);
        }
    }
}
