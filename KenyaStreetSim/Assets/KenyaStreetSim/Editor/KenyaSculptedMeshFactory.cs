using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace KenyaStreetSim.Editor
{
    /// <summary>
    /// Builds and persists smooth, sculpted UV-mapped Unity Mesh assets so vehicles,
    /// authentic Kenyan flora (Acacia, Coastal Palm, Montane Conifer, Kakamega Rainforest Giant,
    /// Tsavo Baobab, Awendo Sugarcane & Plucked Kericho Tea Hedges), and the 3D national biome terrain
    /// look realistic and lush across the entire Republic of Kenya.
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
        public static Mesh ConiferTrunkMesh { get; private set; }
        public static Mesh ConiferCrownMesh { get; private set; }
        public static Mesh RainforestTrunkMesh { get; private set; }
        public static Mesh RainforestCrownMesh { get; private set; }
        public static Mesh BaobabTrunkMesh { get; private set; }
        public static Mesh BaobabCrownMesh { get; private set; }
        public static Mesh SugarcaneClusterMesh { get; private set; }
        public static Mesh TeaHedgeSegmentMesh { get; private set; }
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
            ConiferTrunkMesh = SaveMesh("M_KenyaConiferTrunk", BuildConiferTrunkMesh());
            ConiferCrownMesh = SaveMesh("M_KenyaConiferCrown", BuildConiferCrownMesh());
            RainforestTrunkMesh = SaveMesh("M_KenyaRainforestTrunk", BuildRainforestTrunkMesh());
            RainforestCrownMesh = SaveMesh("M_KenyaRainforestCrown", BuildRainforestCrownMesh());
            BaobabTrunkMesh = SaveMesh("M_KenyaBaobabTrunk", BuildBaobabTrunkMesh());
            BaobabCrownMesh = SaveMesh("M_KenyaBaobabCrown", BuildBaobabCrownMesh());
            SugarcaneClusterMesh = SaveMesh("M_KenyaSugarcaneCluster", BuildSugarcaneClusterMesh());
            TeaHedgeSegmentMesh = SaveMesh("M_KenyaTeaHedgeSegment", BuildTeaHedgeSegmentMesh());
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

        private static Mesh BuildLoftedBoxProfile((float z, float hw, float botY, float topY, float inset)[] slices)
        {
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();

            const int ringVerts = 8;
            for (int s = 0; s < slices.Length; s++)
            {
                var (z, hw, by, ty, inset) = slices[s];
                float midTopY = ty - inset * 0.85f;
                float botChamfY = by + Mathf.Min(0.14f, (ty - by) * 0.15f);
                float botInset = Mathf.Min(0.08f, hw * 0.08f);

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

            return BuildSimpleMesh(verts, uvs, tris);
        }

        private static Mesh BuildBranchingTrunkMesh()
        {
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();

            AddTaperedTube(verts, uvs, tris, new Vector3(0f, 0f, 0f), new Vector3(0.08f, 2.75f, 0.04f), 0.24f, 0.14f, 7);

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

            return BuildSimpleMesh(verts, uvs, tris);
        }

        private static Mesh BuildLeafyFoliageCardsMesh()
        {
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();

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

            return BuildSimpleMesh(verts, uvs, tris);
        }

        private static Mesh BuildPalmTrunkMesh()
        {
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
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
            return BuildSimpleMesh(verts, uvs, tris);
        }

        private static Mesh BuildPalmCrownMesh()
        {
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            Vector3 top = new Vector3(0.45f, 8.15f, 0.14f);
            for (int i = 0; i < 10; i++)
            {
                float yaw = i * 36f + (i % 2) * 12f;
                float pitch = (i % 2 == 0) ? 28f : 52f;
                Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
                Vector3 frondMid = top + rot * new Vector3(0f, 0.45f, 1.85f);
                AddQuad(verts, uvs, tris, frondMid, rot * Quaternion.Euler(85f, 0f, 0f), 1.35f, 3.6f);
            }
            return BuildSimpleMesh(verts, uvs, tris);
        }

        // Towering Mau / Timboroa / Karura Highland Pine, Cypress & Cedar Montane Conifer Trunk (14.5m tall!)
        private static Mesh BuildConiferTrunkMesh()
        {
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            AddTaperedTube(verts, uvs, tris, new Vector3(0f, 0f, 0f), new Vector3(0.05f, 7.2f, 0.03f), 0.34f, 0.18f, 6);
            AddTaperedTube(verts, uvs, tris, new Vector3(0.05f, 7.0f, 0.03f), new Vector3(0.02f, 13.8f, 0f), 0.18f, 0.04f, 6);
            return BuildSimpleMesh(verts, uvs, tris);
        }

        // Tiered evergreen Montane Conifer boughs (4 overlapping dark-emerald pine/cypress skirts!)
        private static Mesh BuildConiferCrownMesh()
        {
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            (float botY, float topY, float rad)[] tiers =
            {
                ( 3.6f,  8.2f, 3.35f),
                ( 6.3f, 10.7f, 2.80f),
                ( 8.7f, 12.9f, 2.15f),
                (10.9f, 14.9f, 1.38f)
            };
            const int sides = 8;
            for (int t = 0; t < tiers.Length; t++)
            {
                var (by, ty, r) = tiers[t];
                float yawOff = t * 0.42f;
                int baseIdx = verts.Count;
                verts.Add(new Vector3(0f, ty, 0f));
                uvs.Add(new Vector2(0.5f, 1f));
                for (int s = 0; s <= sides; s++)
                {
                    float a = yawOff + ((float)s / sides) * Mathf.PI * 2f;
                    verts.Add(new Vector3(Mathf.Cos(a) * r, by, Mathf.Sin(a) * r));
                    uvs.Add(new Vector2((float)s / sides, 0f));
                }
                for (int s = 0; s < sides; s++)
                {
                    tris.Add(baseIdx);
                    tris.Add(baseIdx + 1 + s + 1);
                    tris.Add(baseIdx + 1 + s);
                }
            }
            return BuildSimpleMesh(verts, uvs, tris);
        }

        // Towering Kakamega Tropical Rainforest Broadleaf Buttressed Giant (17m tall!)
        private static Mesh BuildRainforestTrunkMesh()
        {
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            AddTaperedTube(verts, uvs, tris, new Vector3(0f, 0f, 0f), new Vector3(0.12f, 9.5f, 0.08f), 0.56f, 0.30f, 7);
            Vector3 fork = new Vector3(0.12f, 8.8f, 0.08f);
            Vector3[] limbs =
            {
                new Vector3( 3.2f, 12.8f,  1.4f),
                new Vector3(-3.0f, 13.0f,  1.8f),
                new Vector3( 1.2f, 13.4f, -3.1f),
                new Vector3(-2.4f, 12.6f, -2.2f),
                new Vector3( 0.2f, 14.5f,  0.4f)
            };
            for (int i = 0; i < limbs.Length; i++)
                AddTaperedTube(verts, uvs, tris, fork, limbs[i], 0.22f, 0.08f, 6);
            return BuildSimpleMesh(verts, uvs, tris);
        }

        private static Mesh BuildRainforestCrownMesh()
        {
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            var rng = new System.Random(55109);
            Vector3[] clusters =
            {
                new Vector3( 0.0f, 14.6f,  0.0f),
                new Vector3( 3.1f, 12.8f,  1.4f),
                new Vector3(-3.0f, 13.0f,  1.7f),
                new Vector3( 1.3f, 13.2f, -3.0f),
                new Vector3(-2.5f, 12.6f, -2.1f),
                new Vector3( 2.2f, 11.4f, -1.5f),
                new Vector3(-1.8f, 11.6f,  2.4f),
                new Vector3( 0.4f, 16.0f,  0.3f)
            };
            foreach (var c in clusters)
            {
                for (int q = 0; q < 3; q++)
                {
                    float yaw = q * 60f + (float)(rng.NextDouble() * 35.0);
                    float pitch = (float)(rng.NextDouble() * 44.0 - 22.0);
                    Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
                    float sz = 3.3f + (float)rng.NextDouble() * 1.1f;
                    AddQuad(verts, uvs, tris, c, rot, sz, sz);
                }
            }
            return BuildSimpleMesh(verts, uvs, tris);
        }

        // Iconic Bulbous Kenyan Tsavo / Coastal Baobab Tree!
        private static Mesh BuildBaobabTrunkMesh()
        {
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            AddTaperedTube(verts, uvs, tris, new Vector3(0f, 0f, 0f), new Vector3(0.06f, 2.8f, 0f), 1.42f, 1.16f, 8);
            AddTaperedTube(verts, uvs, tris, new Vector3(0.06f, 2.6f, 0f), new Vector3(0.08f, 6.2f, 0.04f), 1.16f, 0.74f, 8);
            Vector3 crownBase = new Vector3(0.08f, 5.9f, 0.04f);
            Vector3[] arms =
            {
                new Vector3( 3.6f, 8.6f,  1.2f),
                new Vector3(-3.4f, 8.8f,  1.6f),
                new Vector3( 0.9f, 9.1f, -3.5f),
                new Vector3(-2.6f, 8.4f, -2.3f),
                new Vector3( 2.1f, 9.2f, -1.8f),
                new Vector3(-0.3f, 9.6f,  2.8f)
            };
            for (int i = 0; i < arms.Length; i++)
                AddTaperedTube(verts, uvs, tris, crownBase, arms[i], 0.36f, 0.09f, 6);
            return BuildSimpleMesh(verts, uvs, tris);
        }

        private static Mesh BuildBaobabCrownMesh()
        {
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            var rng = new System.Random(90817);
            Vector3[] tips =
            {
                new Vector3( 3.6f, 8.8f,  1.2f),
                new Vector3(-3.4f, 9.0f,  1.6f),
                new Vector3( 0.9f, 9.3f, -3.5f),
                new Vector3(-2.6f, 8.6f, -2.3f),
                new Vector3( 2.1f, 9.4f, -1.8f),
                new Vector3(-0.3f, 9.8f,  2.8f),
                new Vector3( 0.0f, 10.2f, 0.0f)
            };
            foreach (var c in tips)
            {
                for (int q = 0; q < 2; q++)
                {
                    Quaternion rot = Quaternion.Euler((float)(rng.NextDouble() * 34.0 - 17.0), q * 85f + (float)(rng.NextDouble() * 40.0), 0f);
                    float sz = 2.4f + (float)rng.NextDouble() * 0.7f;
                    AddQuad(verts, uvs, tris, c, rot, sz, sz * 0.85f);
                }
            }
            return BuildSimpleMesh(verts, uvs, tris);
        }

        // Tall Dense Awendo / Muhoroni / Mumias Kenyan Sugarcane Stalk Clump (3.2m tall!)
        private static Mesh BuildSugarcaneClusterMesh()
        {
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            var rng = new System.Random(31415);
            for (int i = 0; i < 6; i++)
            {
                float ox = (float)(rng.NextDouble() * 3.8 - 1.9);
                float oz = (float)(rng.NextDouble() * 3.8 - 1.9);
                float yaw = (float)(rng.NextDouble() * 180.0);
                float h = 2.75f + (float)rng.NextDouble() * 0.85f;
                AddQuad(verts, uvs, tris, new Vector3(ox, h * 0.5f, oz), Quaternion.Euler(0f, yaw, 0f), 3.0f, h);
                AddQuad(verts, uvs, tris, new Vector3(ox, h * 0.5f, oz), Quaternion.Euler(0f, yaw + 90f, 0f), 3.0f, h);
            }
            return BuildSimpleMesh(verts, uvs, tris);
        }

        // Sculpted Rounded Flat-Topped Plucked Kericho / Chebilat Tea Hedge Row Segment (10.4m x 3.9m x 1.3m!)
        private static Mesh BuildTeaHedgeSegmentMesh()
        {
            return BuildLoftedBoxProfile(new[]
            {
                (-5.2f, 1.50f, -0.55f, 0.92f, 0.44f),
                (-4.0f, 1.96f, -0.55f, 1.25f, 0.48f),
                ( 0.0f, 2.02f, -0.55f, 1.32f, 0.52f),
                ( 4.0f, 1.96f, -0.55f, 1.25f, 0.48f),
                ( 5.2f, 1.50f, -0.55f, 0.92f, 0.44f)
            });
        }

        private static Mesh BuildSimpleMesh(List<Vector3> verts, List<Vector2> uvs, List<int> tris)
        {
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
            const int resX = 188;
            const int resZ = 132;
            const float minX = -2280f;
            const float maxX = 1560f;
            const float minZ = -660f;
            const float maxZ = 1800f;

            var roadSegs = new List<(Vector2 a, Vector2 b, float hw)>();
            for (int h = 0; h < KenyaNetworkData.Highways.Length; h++)
            {
                var def = KenyaNetworkData.Highways[h];
                var pts = KenyaNetworkData.SampleHighway(def, 22f);
                for (int i = 0; i < pts.Count - 1; i++)
                {
                    Vector2 a = new Vector2(pts[i].x, pts[i].z);
                    Vector2 b = new Vector2(pts[i + 1].x, pts[i + 1].z);
                    float hw = KenyaNetworkData.GetRoadHalfWidthAt(def, (a + b) * 0.5f);
                    roadSegs.Add((a, b, hw));
                }
            }

            var verts = new Vector3[(resX + 1) * (resZ + 1)];
            var uvs = new Vector2[verts.Length];
            var tris = new int[resX * resZ * 6];

            Vector2[] airportSites =
            {
                new Vector2(  135f,  -45f),
                new Vector2(-1615f,  915f),
                new Vector2(-1075f, 1085f),
                new Vector2(  840f, -455f),
            };

            for (int iz = 0; iz <= resZ; iz++)
            {
                float vz = (float)iz / resZ;
                float wz = Mathf.Lerp(minZ, maxZ, vz);

                for (int ix = 0; ix <= resX; ix++)
                {
                    float ux = (float)ix / resX;
                    float wx = Mathf.Lerp(minX, maxX, ux);
                    Vector2 p = new Vector2(wx, wz);

                    float baseY = KenyaNetworkData.GetElevationAt(p);

                    float minDist = float.MaxValue;
                    for (int s = 0; s < roadSegs.Count; s++)
                    {
                        var (a, b, hw) = roadSegs[s];
                        Vector2 ab = b - a;
                        float lenSq = ab.sqrMagnitude;
                        float t = lenSq > 0.001f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / lenSq) : 0f;
                        float d = Mathf.Max(0f, Vector2.Distance(p, a + ab * t) - hw);
                        if (d < minDist) minDist = d;
                    }
                    for (int h = 0; h < KenyaNetworkData.Hubs.Length; h++)
                    {
                        float townPad = KenyaNetworkData.Hubs[h].tier == KenyaNetworkData.TownTier.MajorCity ? 68f : 44f;
                        float dTown = Vector2.Distance(p, KenyaNetworkData.Hubs[h].xz) - townPad;
                        if (dTown < minDist) minDist = Mathf.Max(0f, dTown);
                    }
                    for (int r = 0; r < KenyaNetworkData.Roundabouts.Length; r++)
                    {
                        float dRbt = Vector2.Distance(p, KenyaNetworkData.Roundabouts[r].center) - (KenyaNetworkData.Roundabouts[r].outerRadius + 16f);
                        if (dRbt < minDist) minDist = Mathf.Max(0f, dRbt);
                    }
                    for (int a = 0; a < airportSites.Length; a++)
                    {
                        float dAir = Vector2.Distance(p, airportSites[a]) - 78f;
                        if (dAir < minDist) minDist = Mathf.Max(0f, dAir);
                    }

                    bool inLakeVictoria = ((wx < -1895f && wz > 120f && wz < 980f) || (wx < -2045f && wz > -260f && wz < 980f) || (wx < -1600f && wz > 560f && wz < 790f && minDist > 42f));
                    bool inIndianOcean  = ((wx > 1075f && wz < -395f) || (wx > 1245f && wz < -180f) || (wx > 1345f && wz < 240f));

                    float y;
                    if ((inLakeVictoria || inIndianOcean) && minDist > 34f)
                    {
                        y = -3.4f;
                    }
                    else
                    {
                        float hillMask = SmoothRange(36f, 150f, minDist);
                        bool isFlatPlain = (wx > 760f && wz < 120f) || (wx < -1720f && wz < 680f) || (wx > -760f && wx < -320f && wz < 260f);
                        float regionScale = isFlatPlain ? 0.30f : ((wx < -180f && wz > 120f) ? 1.35f : 0.95f);

                        float n1 = Mathf.Clamp01(Mathf.PerlinNoise(wx * 0.0026f + 13.7f, wz * 0.0026f + 29.1f));
                        float n2 = Mathf.Clamp01(Mathf.PerlinNoise(wx * 0.0082f + 71.3f, wz * 0.0082f + 41.9f));
                        float ridge = (Mathf.Pow(n1, 1.35f) * 44f + n2 * 12f) * regionScale;
                        y = (baseY - 0.14f) + ridge * hillMask;
                        if (float.IsNaN(y) || float.IsInfinity(y)) y = baseY - 0.14f;
                    }

                    int idx = iz * (resX + 1) + ix;
                    verts[idx] = new Vector3(wx, y, wz);
                    // Exact 0..1 national biome UV matching KenyaBiomeMap.png!
                    uvs[idx] = new Vector2(ux, vz);
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
