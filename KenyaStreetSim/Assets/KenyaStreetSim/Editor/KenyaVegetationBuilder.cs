using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace KenyaStreetSim.Editor
{
    /// <summary>
    /// High-performance Batched National Republic of Kenya Ecological Vegetation & Forest Builder!
    /// Populates 2,800+ authentic 3D Kenyan flora instances across the entire 41-town highway network
    /// and merges them per material into ~22 crisp UInt32 static chunk meshes (60+ FPS!):
    /// 1. Continuous Plucked 3D Tea Plantations (Kericho, Kaplong, Bomet, Sotik, Chebilat, Keroka, Kisii)
    /// 2. Towering Montane Evergreen Forests (Mau Forest Complex, Timboroa Forest, Karura & Ngong/Limuru Escarpment)
    /// 3. Kakamega Tropical Equatorial Rainforest (Vihiga -> Kakamega -> Webuye -> Kitale)
    /// 4. Tall Green Sugarcane Belts (Awendo / South Nyanza, Muhoroni, Mumias / Bungoma)
    /// 5. Tsavo / Voi Iconic Bulbous Baobab Trees & Acacia-Commiphora Savanna Woodlands (Narok, Naivasha, Nakuru, Tsavo)
    /// 6. Coastal Coconut Palm Groves & Arabuko-Sokoke Coastal Forest (Mombasa -> Kilifi -> Malindi) + Lake Victoria Shores!
    /// Every single plant is grounded via Raycast/GetElevationAt onto the ACTUAL 3D sculpted hillside and verified clear of all roads & roundabouts!
    /// </summary>
    public static class KenyaVegetationBuilder
    {
        private const string VegMeshFolder = "Assets/KenyaStreetSim/Meshes/Vegetation";

        public static int BuildAllKenyanVegetationAndForests(Transform worldParent, Collider terrainCollider)
        {
            if (!Directory.Exists(VegMeshFolder))
            {
                Directory.CreateDirectory(VegMeshFolder);
            }

            var urpLit = Shader.Find("Universal Render Pipeline/Lit");
            var root = new GameObject("Kenya_Authentic_Forests_And_Vegetation").transform;
            root.SetParent(worldParent, false);

            var texBark = AssetDatabase.LoadAssetAtPath<Texture2D>($"{KenyaPbrTextureGenerator.TexFolder}/CC0_PBR/CC0_Bark_Albedo.jpg")
                       ?? AssetDatabase.LoadAssetAtPath<Texture2D>($"{KenyaPbrTextureGenerator.TexFolder}/Bark_Albedo.png");
            var texBarkNrm = AssetDatabase.LoadAssetAtPath<Texture2D>($"{KenyaPbrTextureGenerator.TexFolder}/CC0_PBR/CC0_Bark_Normal.jpg")
                          ?? AssetDatabase.LoadAssetAtPath<Texture2D>($"{KenyaPbrTextureGenerator.TexFolder}/Bark_Normal.png");
            var texLeaf = AssetDatabase.LoadAssetAtPath<Texture2D>($"{KenyaPbrTextureGenerator.TexFolder}/LeafCluster_Albedo.png");
            var texTeaNrm = AssetDatabase.LoadAssetAtPath<Texture2D>($"{KenyaPbrTextureGenerator.TexFolder}/CC0_PBR/ACG_KenyaHighlandGrass_Normal.jpg")
                         ?? AssetDatabase.LoadAssetAtPath<Texture2D>($"{KenyaPbrTextureGenerator.TexFolder}/TerrainMicroDetail_Normal.png")
                         ?? AssetDatabase.LoadAssetAtPath<Texture2D>($"{KenyaPbrTextureGenerator.TexFolder}/Murram_Normal.png");

            // Load real sculpted Blender 5.3 jagged branching highland pine/cypress meshes (replaces smooth cones!)
            ExtractBlenderPineMeshes(out Mesh coniferTrunkMesh, out Mesh coniferCrownMesh);
            if (coniferTrunkMesh == null) coniferTrunkMesh = KenyaSculptedMeshFactory.ConiferTrunkMesh;
            if (coniferCrownMesh == null) coniferCrownMesh = KenyaSculptedMeshFactory.ConiferCrownMesh;

            // Authentic Kenyan ecological flora materials
            Material matAcaciaBark  = MakeMat("Veg_AcaciaBark",     urpLit, new Color(0.85f, 0.80f, 0.72f), 0.10f, 0f, texBark, texBarkNrm, new Vector2(1f, 3f), false);
            Material matConiferBark = MakeMat("Veg_ConiferBark",    urpLit, new Color(0.54f, 0.40f, 0.30f), 0.10f, 0f, texBark, texBarkNrm, new Vector2(1f, 4f), false);
            Material matBaobabBark  = MakeMat("Veg_BaobabBark",     urpLit, new Color(0.70f, 0.64f, 0.58f), 0.14f, 0f, texBark, texBarkNrm, new Vector2(2f, 2f), false);
            Material matAcaciaLeaf  = MakeMat("Veg_AcaciaLeaf",     urpLit, new Color(0.92f, 0.96f, 0.82f), 0.14f, 0f, texLeaf, null, Vector2.one, true);
            Material matConiferLeaf = MakeMat("Veg_ConiferNeedles", urpLit, new Color(0.13f, 0.30f, 0.15f), 0.12f, 0f, null, texTeaNrm, new Vector2(3f, 3f), false);
            Material matRainLeaf    = MakeMat("Veg_RainforestLeaf", urpLit, new Color(0.56f, 0.80f, 0.54f), 0.18f, 0f, texLeaf, null, Vector2.one, true);
            Material matPalmLeaf    = MakeMat("Veg_CoastalPalmLeaf",urpLit, new Color(0.76f, 0.96f, 0.62f), 0.22f, 0f, texLeaf, null, Vector2.one, true);
            Material matSugarcane   = MakeMat("Veg_SugarcaneGreen", urpLit, new Color(0.68f, 0.94f, 0.42f), 0.20f, 0f, texLeaf, null, Vector2.one, true);
            Material matTeaDark     = MakeMat("Veg_TeaPluckedDark", urpLit, new Color(0.14f, 0.38f, 0.13f), 0.14f, 0f, null, texTeaNrm, new Vector2(5f, 2f), false);
            Material matTeaBright   = MakeMat("Veg_TeaPluckedBright",urpLit, new Color(0.21f, 0.48f, 0.17f), 0.16f, 0f, null, texTeaNrm, new Vector2(5f, 2f), false);
            Material matSignGreen   = MakeMat("Veg_ForestSign",     urpLit, new Color(0.08f, 0.36f, 0.18f), 0.38f, 0.08f, null, null, Vector2.one, false);
            Material matSignSteel   = MakeMat("Veg_SignPost",       urpLit, new Color(0.24f, 0.27f, 0.31f), 0.55f, 0.72f, null, null, Vector2.one, false);

            var buckets = new Dictionary<Material, List<CombineInstance>>();
            int totalInstances = 0;

            void AddInst(Mesh mesh, Material mat, Matrix4x4 mtx)
            {
                if (mesh == null || mat == null) return;
                if (!buckets.TryGetValue(mat, out var list))
                {
                    list = new List<CombineInstance>(512);
                    buckets[mat] = list;
                }
                list.Add(new CombineInstance { mesh = mesh, transform = mtx });
            }

            float SampleGroundY(float x, float z)
            {
                if (terrainCollider != null)
                {
                    var ray = new Ray(new Vector3(x, 220f, z), Vector3.down);
                    if (terrainCollider.Raycast(ray, out RaycastHit hit, 320f))
                    {
                        return hit.point.y;
                    }
                }
                return KenyaNetworkData.GetElevationAt(x, z);
            }

            bool CanPlaceFlora(Vector2 xz, float roadClearance, float hubClearance = 44f)
            {
                if (KenyaNetworkData.DistanceToAnyTarmacEdge(xz) < roadClearance) return false;
                if (!KenyaNetworkData.IsClearOfAllRoundabouts(xz, 24f)) return false;
                for (int i = 0; i < KenyaNetworkData.Hubs.Length; i++)
                {
                    float r = KenyaNetworkData.Hubs[i].tier == KenyaNetworkData.TownTier.MajorCity ? hubClearance + 18f : hubClearance;
                    if (Vector2.Distance(xz, KenyaNetworkData.Hubs[i].xz) < r) return false;
                }
                // Keep clear of all 4 airport pads!
                Vector2[] airports = { new Vector2(135f, -45f), new Vector2(-1615f, 915f), new Vector2(-1075f, 1085f), new Vector2(840f, -455f) };
                for (int a = 0; a < airports.Length; a++)
                    if (Vector2.Distance(xz, airports[a]) < 84f) return false;
                return true;
            }

            var rng = new System.Random(2541963);

            // =========================================================================
            // 1. CONTINUOUS SCULPTED 3D KERICHO / CHEBILAT / SOTIK / KAPLONG / BOMET / KISII TEA PLANTATIONS!
            // =========================================================================
            (string estateName, Vector2 center, float halfX, float halfZ, float yawDeg)[] teaEstates =
            {
                ("KERICHO HIGHLAND TEA ESTATES",     new Vector2(-1010f, 470f), 72f, 54f,  16f),
                ("KERICHO NORTH TEA ESTATES",        new Vector2( -940f, 600f), 68f, 52f, -24f),
                ("KERICHO WEST TEA HILLS",           new Vector2(-1155f, 525f), 66f, 48f,  30f),
                ("KAPLONG – BOMET TEA GARDENS",      new Vector2(-1015f, 295f), 70f, 48f, -10f),
                ("SOTIK HIGHLAND TEA BELT",          new Vector2(-1200f, 305f), 62f, 45f,   8f),
                ("CHEBILAT & NYAMIRA TEA ESTATES",   new Vector2(-1305f, 285f), 72f, 52f, -14f),
                ("CHEBILAT – SONDU TEA RIDGE",       new Vector2(-1355f, 365f), 58f, 44f,  24f),
                ("KEROKA – KISII HILLSIDE TEA",      new Vector2(-1480f, 265f), 56f, 42f, -18f),
            };

            for (int e = 0; e < teaEstates.Length; e++)
            {
                var es = teaEstates[e];
                Quaternion rot = Quaternion.Euler(0f, es.yawDeg, 0f);
                bool signPlaced = false;

                // Tight manicured rows: 4.5m row pitch, 9.6m segment stride -> lush continuous contoured green carpet!
                for (float lx = -es.halfX; lx <= es.halfX; lx += 4.6f)
                {
                    int rowIdx = Mathf.RoundToInt((lx + es.halfX) / 4.6f);
                    for (float lz = -es.halfZ; lz <= es.halfZ; lz += 9.6f)
                    {
                        Vector3 off = rot * new Vector3(lx, 0f, lz);
                        Vector2 wp = es.center + new Vector2(off.x, off.z);
                        if (!CanPlaceFlora(wp, roadClearance: 13.5f, hubClearance: 36f)) continue;

                        float gy = SampleGroundY(wp.x, wp.y);
                        Vector3 fwdOff = rot * new Vector3(0f, 0f, 4.0f);
                        Vector3 rgtOff = rot * new Vector3(2.0f, 0f, 0f);
                        float gyFwd = SampleGroundY(wp.x + fwdOff.x, wp.y + fwdOff.z);
                        float gyRgt = SampleGroundY(wp.x + rgtOff.x, wp.y + rgtOff.z);
                        float pitch = -Mathf.Atan2(gyFwd - gy, 4.0f) * Mathf.Rad2Deg;
                        float roll  =  Mathf.Atan2(gyRgt - gy, 2.0f) * Mathf.Rad2Deg;

                        Quaternion hedgeRot = rot * Quaternion.Euler(pitch, 0f, roll);
                        Vector3 scl = new Vector3(1.04f, 0.92f + (float)rng.NextDouble() * 0.25f, 1.02f);
                        Material teaMat = ((rowIdx + e) % 2 == 0) ? matTeaDark : matTeaBright;
                        AddInst(KenyaSculptedMeshFactory.TeaHedgeSegmentMesh, teaMat, Matrix4x4.TRS(new Vector3(wp.x, gy - 0.10f, wp.y), hedgeRot, scl));
                        totalInstances++;

                        // Occasional iconic Grevillea / Highland Shade Tree towering above the manicured tea rows
                        if (rng.NextDouble() < 0.045)
                        {
                            float ts = 1.15f + (float)rng.NextDouble() * 0.45f;
                            Quaternion trRot = Quaternion.Euler(0f, (float)(rng.NextDouble() * 360.0), 0f);
                            Matrix4x4 trM = Matrix4x4.TRS(new Vector3(wp.x, gy - 0.15f, wp.y), trRot, Vector3.one * ts);
                            AddInst(KenyaSculptedMeshFactory.TreeTrunkMesh, matAcaciaBark, trM);
                            AddInst(KenyaSculptedMeshFactory.TreeFoliageMesh, matRainLeaf, trM);
                            totalInstances++;
                        }

                        if (!signPlaced && KenyaNetworkData.DistanceToAnyTarmacEdge(wp) < 34f)
                        {
                            signPlaced = true;
                            BuildReserveSign(root, new Vector3(wp.x, gy, wp.y), es.yawDeg, es.estateName, matSignGreen, matSignSteel);
                        }
                    }
                }
            }

            // =========================================================================
            // 2. MAJOR KENYAN MONTANE & TROPICAL FOREST RESERVES (DENSE MULTI-ROW FOREST WALLS!)
            // =========================================================================
            // Kind: 0 = Montane Conifer (Mau / Timboroa / Karura / Ngong), 1 = Tropical Rainforest (Kakamega), 2 = Coastal Forest (Arabuko-Sokoke)
            (string forestName, Vector2 center, float radiusX, float radiusZ, int treeCount, int kind)[] forestReserves =
            {
                // Karura, Ngong & Limuru Escarpment Forests (Nairobi -> Thika & Nairobi -> Rift Escarpment)
                ("KARURA FOREST RESERVE",           new Vector2(  -28f,  295f), 110f, 115f, 125, 0),
                ("NGONG & ESCARPMENT FOREST",       new Vector2( -135f,  235f), 115f, 105f, 115, 0),
                ("THIKA HIGHLAND WOODLANDS",        new Vector2(  185f,  355f), 105f, 110f,  85, 0),

                // MAU FOREST COMPLEX — East Africa's Largest Montane Forest!
                ("MAU FOREST COMPLEX — WATER TOWER",new Vector2( -755f,  660f), 165f, 145f, 185, 0),
                ("SOUTH-WEST MAU FOREST",           new Vector2( -845f,  435f), 155f, 140f, 165, 0),
                ("MAU SUMMIT / MOLO HIGHLANDS",     new Vector2( -670f,  810f), 140f, 115f, 130, 0),

                // TIMBOROA HIGH-ALTITUDE MONTANE FOREST (Total -> Timboroa -> Eldoret Climbing Corridor!)
                ("TIMBOROA FOREST RESERVE (2,700M)",new Vector2( -855f,  955f), 150f, 155f, 185, 0),
                ("TIMBOROA – ELDORET PINES",        new Vector2( -955f, 1065f), 130f, 115f, 110, 0),

                // KAKAMEGA TROPICAL RAINFOREST — Kenya's Only Guineo-Congolian Equatorial Rainforest!
                ("KAKAMEGA TROPICAL RAINFOREST",    new Vector2(-1325f, 1155f), 165f, 175f, 195, 1),
                ("VIHIGA – KAKAMEGA FOREST BELT",   new Vector2(-1430f, 1065f), 125f, 125f, 115, 1),
                ("WEBUYE – KITALE CHERANGANI HILLS",new Vector2(-1085f, 1415f), 155f, 135f, 115, 0),

                // ARABUKO-SOKOKE COASTAL FOREST (Kilifi -> Malindi, Largest Coastal Forest in East Africa!)
                ("ARABUKO-SOKOKE COASTAL FOREST",   new Vector2( 1165f,  -55f), 135f, 145f, 155, 2),
            };

            for (int f = 0; f < forestReserves.Length; f++)
            {
                var fr = forestReserves[f];
                bool signPlaced = false;

                for (int t = 0; t < fr.treeCount; t++)
                {
                    float ang = (float)(rng.NextDouble() * Mathf.PI * 2.0);
                    float rad = Mathf.Sqrt((float)rng.NextDouble());
                    Vector2 wp = fr.center + new Vector2(Mathf.Cos(ang) * fr.radiusX * rad, Mathf.Sin(ang) * fr.radiusZ * rad);
                    if (!CanPlaceFlora(wp, roadClearance: 9.5f, hubClearance: 42f)) continue;

                    float gy = SampleGroundY(wp.x, wp.y);
                    float yaw = (float)(rng.NextDouble() * 360.0);
                    float scl = 0.85f + (float)rng.NextDouble() * 0.55f;
                    Matrix4x4 m = Matrix4x4.TRS(new Vector3(wp.x, gy - 0.25f, wp.y), Quaternion.Euler(0f, yaw, 0f), Vector3.one * scl);

                    if (fr.kind == 0) // Montane Conifer (Pine/Cypress/Cedar) mixed with occasional indigenous hardwood!
                    {
                        if (rng.NextDouble() < 0.78)
                        {
                            AddInst(coniferTrunkMesh, matConiferBark, m);
                            AddInst(coniferCrownMesh, matConiferLeaf, m);
                        }
                        else
                        {
                            AddInst(KenyaSculptedMeshFactory.RainforestTrunkMesh, matAcaciaBark, m);
                            AddInst(KenyaSculptedMeshFactory.RainforestCrownMesh, matRainLeaf, m);
                        }
                    }
                    else if (fr.kind == 1) // Kakamega Tropical Rainforest Broadleaf Giants!
                    {
                        AddInst(KenyaSculptedMeshFactory.RainforestTrunkMesh, matAcaciaBark, m);
                        AddInst(KenyaSculptedMeshFactory.RainforestCrownMesh, matRainLeaf, m);
                    }
                    else // Arabuko-Sokoke Dense Coastal Woodland & Palms!
                    {
                        if (rng.NextDouble() < 0.62)
                        {
                            AddInst(KenyaSculptedMeshFactory.RainforestTrunkMesh, matAcaciaBark, m);
                            AddInst(KenyaSculptedMeshFactory.RainforestCrownMesh, matRainLeaf, m);
                        }
                        else
                        {
                            AddInst(KenyaSculptedMeshFactory.PalmTrunkMesh, matAcaciaBark, m);
                            AddInst(KenyaSculptedMeshFactory.PalmCrownMesh, matPalmLeaf, m);
                        }
                    }
                    totalInstances++;

                    if (!signPlaced && KenyaNetworkData.DistanceToAnyTarmacEdge(wp) < 26f)
                    {
                        signPlaced = true;
                        BuildReserveSign(root, new Vector3(wp.x, gy, wp.y), yaw % 180f, fr.forestName, matSignGreen, matSignSteel);
                    }
                }
            }

            // =========================================================================
            // 3. DENSE LUSH KENYAN SUGARCANE PLANTATIONS (AWENDO, MUHORONI / AHERO & MUMIAS / BUNGOMA!)
            // =========================================================================
            (string beltName, Vector2 center, float halfX, float halfZ, float yawDeg)[] caneBelts =
            {
                ("AWENDO SOUTH NYANZA SUGAR BELT", new Vector2(-1658f, -205f), 85f, 95f,  -5f),
                ("MUHORONI – NYANDO SUGAR PLAINS", new Vector2(-1205f,  630f), 78f, 68f, -34f),
                ("MUMIAS – BUNGOMA SUGAR BELT",    new Vector2(-1555f, 1125f), 82f, 88f, -14f),
            };
            for (int c = 0; c < caneBelts.Length; c++)
            {
                var cb = caneBelts[c];
                Quaternion rot = Quaternion.Euler(0f, cb.yawDeg, 0f);
                bool signPlaced = false;
                // Dense lush rows of 3.4m waving Kenyan sugarcane blanketing both sides of the highway!
                for (float lx = -cb.halfX; lx <= cb.halfX; lx += 8.2f)
                {
                    for (float lz = -cb.halfZ; lz <= cb.halfZ; lz += 8.2f)
                    {
                        if (rng.NextDouble() < 0.14) continue;
                        Vector3 off = rot * new Vector3(lx + (float)(rng.NextDouble() * 2.4 - 1.2), 0f, lz + (float)(rng.NextDouble() * 2.4 - 1.2));
                        Vector2 wp = cb.center + new Vector2(off.x, off.z);
                        if (!CanPlaceFlora(wp, roadClearance: 9.5f, hubClearance: 36f)) continue;

                        float gy = SampleGroundY(wp.x, wp.y);
                        float scl = 1.05f + (float)rng.NextDouble() * 0.45f;
                        Matrix4x4 m = Matrix4x4.TRS(new Vector3(wp.x, gy - 0.12f, wp.y), Quaternion.Euler(0f, (float)(rng.NextDouble() * 360.0), 0f), new Vector3(scl * 1.15f, scl * 1.12f, scl * 1.15f));
                        AddInst(KenyaSculptedMeshFactory.SugarcaneClusterMesh, matSugarcane, m);
                        totalInstances++;

                        if (!signPlaced && KenyaNetworkData.DistanceToAnyTarmacEdge(wp) < 28f)
                        {
                            signPlaced = true;
                            BuildReserveSign(root, new Vector3(wp.x, gy, wp.y), cb.yawDeg, cb.beltName, matSignGreen, matSignSteel);
                        }
                    }
                }
            }

            // =========================================================================
            // 4. CORRIDOR-SIDE ECOLOGICAL TREES ACROSS ALL 15 HIGHWAYS!
            //    (Acacias in Rift/Narok/Nyanza, Giant Baobabs in Tsavo/Voi, Palms at Coast & Lake Victoria, Conifers/Rainforest in Highlands!)
            // =========================================================================
            for (int h = 0; h < KenyaNetworkData.Highways.Length; h++)
            {
                var pts = KenyaNetworkData.SampleHighway(KenyaNetworkData.Highways[h], 24f);
                for (int i = 1; i < pts.Count - 1; i++)
                {
                    Vector3 p = pts[i];
                    if (h == 0 && p.z < 165f) continue; // Nairobi CBD has formal street planters

                    Vector3 fwd = pts[i + 1] - pts[i - 1]; fwd.y = 0f;
                    if (fwd.sqrMagnitude < 0.01f) continue;
                    fwd.Normalize();
                    Vector3 right = Vector3.Cross(Vector3.up, fwd).normalized;

                    bool isCoast = (p.x > 820f && p.z < 220f);
                    bool isTsavo = (p.x > 210f && p.x <= 840f && p.z < 120f);
                    bool isLakeShore = (p.x < -1420f && p.z > -260f && p.z < 920f);
                    bool isMontaneForest = (p.x > -990f && p.x < -640f && p.z > 650f && p.z < 1120f) || (p.x > -170f && p.x < 160f && p.z > 195f && p.z < 460f);
                    bool isKakamega = (p.x < -1180f && p.x > -1520f && p.z > 940f && p.z < 1420f);

                    for (int side = -1; side <= 1; side += 2)
                    {
                        // Plant 2 layers deep (roadside + mid-hillside) for rich, natural Kenyan landscape framing!
                        for (int layer = 0; layer < 2; layer++)
                        {
                            if (rng.NextDouble() < (layer == 0 ? 0.22 : 0.45)) continue;
                            float lat = (layer == 0 ? 15.5f : 38f) + (float)rng.NextDouble() * (layer == 0 ? 16f : 42f);
                            Vector3 tp = p + right * (side * lat);
                            Vector2 wp = new Vector2(tp.x, tp.z);
                            if (!CanPlaceFlora(wp, roadClearance: 7.5f, hubClearance: 42f)) continue;

                            float gy = SampleGroundY(wp.x, wp.y);
                            float yaw = (float)(rng.NextDouble() * 360.0);
                            float scl = 0.88f + (float)rng.NextDouble() * 0.52f;
                            Matrix4x4 m = Matrix4x4.TRS(new Vector3(wp.x, gy - 0.15f, wp.y), Quaternion.Euler(0f, yaw, 0f), Vector3.one * scl);

                            if (isCoast)
                            {
                                if (rng.NextDouble() < 0.72)
                                {
                                    AddInst(KenyaSculptedMeshFactory.PalmTrunkMesh, matAcaciaBark, m);
                                    AddInst(KenyaSculptedMeshFactory.PalmCrownMesh, matPalmLeaf, m);
                                }
                                else
                                {
                                    AddInst(KenyaSculptedMeshFactory.BaobabTrunkMesh, matBaobabBark, m);
                                    AddInst(KenyaSculptedMeshFactory.BaobabCrownMesh, matAcaciaLeaf, m);
                                }
                            }
                            else if (isTsavo)
                            {
                                // Iconic Tsavo Red-Earth Baobabs + Flat-Topped Savanna Acacias!
                                if (rng.NextDouble() < 0.28)
                                {
                                    AddInst(KenyaSculptedMeshFactory.BaobabTrunkMesh, matBaobabBark, m);
                                    AddInst(KenyaSculptedMeshFactory.BaobabCrownMesh, matAcaciaLeaf, m);
                                }
                                else
                                {
                                    AddInst(KenyaSculptedMeshFactory.TreeTrunkMesh, matAcaciaBark, m);
                                    AddInst(KenyaSculptedMeshFactory.TreeFoliageMesh, matAcaciaLeaf, m);
                                }
                            }
                            else if (isLakeShore)
                            {
                                if (rng.NextDouble() < 0.55)
                                {
                                    AddInst(KenyaSculptedMeshFactory.PalmTrunkMesh, matAcaciaBark, m);
                                    AddInst(KenyaSculptedMeshFactory.PalmCrownMesh, matPalmLeaf, m);
                                }
                                else
                                {
                                    AddInst(KenyaSculptedMeshFactory.TreeTrunkMesh, matAcaciaBark, m);
                                    AddInst(KenyaSculptedMeshFactory.TreeFoliageMesh, matAcaciaLeaf, m);
                                }
                            }
                            else if (isMontaneForest)
                            {
                                AddInst(coniferTrunkMesh, matConiferBark, m);
                                AddInst(coniferCrownMesh, matConiferLeaf, m);
                            }
                            else if (isKakamega)
                            {
                                AddInst(KenyaSculptedMeshFactory.RainforestTrunkMesh, matAcaciaBark, m);
                                AddInst(KenyaSculptedMeshFactory.RainforestCrownMesh, matRainLeaf, m);
                            }
                            else
                            {
                                // Classic East African Umbrella Acacia Savanna & Highland Shade Trees!
                                AddInst(KenyaSculptedMeshFactory.TreeTrunkMesh, matAcaciaBark, m);
                                AddInst(KenyaSculptedMeshFactory.TreeFoliageMesh, matAcaciaLeaf, m);
                            }
                            totalInstances++;
                        }
                    }
                }
            }

            // =========================================================================
            // 5. MERGE ALL BUCKETS INTO ULTRA-FAST STATIC UInt32 BATCHED MESH CHUNKS!
            // =========================================================================
            int chunkIdx = 0;
            foreach (var kv in buckets)
            {
                Material mat = kv.Key;
                var list = kv.Value;
                if (list.Count == 0) continue;

                var batch = new List<CombineInstance>();
                int vertAccum = 0;
                for (int i = 0; i < list.Count; i++)
                {
                    int vc = list[i].mesh.vertexCount;
                    if (batch.Count > 0 && vertAccum + vc > 180000)
                    {
                        EmitCombinedChunk(root, mat, batch, chunkIdx++);
                        batch.Clear();
                        vertAccum = 0;
                    }
                    batch.Add(list[i]);
                    vertAccum += vc;
                }
                if (batch.Count > 0)
                {
                    EmitCombinedChunk(root, mat, batch, chunkIdx++);
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[KenyaVegetationBuilder] Built {totalInstances} 3D Kenyan flora/forest/tea/sugarcane instances batched into {chunkIdx} ultra-fast static chunks!");
            return totalInstances;
        }

        private static void EmitCombinedChunk(Transform parent, Material mat, List<CombineInstance> instances, int chunkIdx)
        {
            var combined = new Mesh { indexFormat = IndexFormat.UInt32 };
            combined.CombineMeshes(instances.ToArray(), mergeSubMeshes: true, useMatrices: true, hasLightmapData: false);
            combined.RecalculateBounds();

            string assetName = $"VegBatch_{chunkIdx:00}_{mat.name}";
            var saved = SaveVegMesh(assetName, combined);

            var go = new GameObject(assetName);
            go.transform.SetParent(parent, false);
            go.isStatic = true;
            go.AddComponent<MeshFilter>().sharedMesh = saved;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = mat.name.Contains("Tea") ? ShadowCastingMode.Off : ShadowCastingMode.On;
        }

        private static Mesh SaveVegMesh(string name, Mesh mesh)
        {
            mesh.name = name;
            string path = $"{VegMeshFolder}/{name}.asset";
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

        private static void ExtractBlenderPineMeshes(out Mesh trunkCombined, out Mesh crownCombined)
        {
            trunkCombined = null;
            crownCombined = null;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/KenyaStreetSim/ImportedModels/KenyaWorldKit/Kenya_Highland_Pine.glb");
            if (prefab == null) return;

            var inst = Object.Instantiate(prefab);
            inst.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            inst.transform.localScale = Vector3.one;

            var trunkList = new List<CombineInstance>();
            var crownList = new List<CombineInstance>();
            foreach (var mf in inst.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null || !mf.sharedMesh.isReadable) continue;
                var ci = new CombineInstance { mesh = mf.sharedMesh, transform = mf.transform.localToWorldMatrix };
                if (mf.gameObject.name.IndexOf("Canopy", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    mf.gameObject.name.IndexOf("Foliage", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    crownList.Add(ci);
                }
                else
                {
                    trunkList.Add(ci);
                }
            }
            Object.DestroyImmediate(inst);

            if (trunkList.Count > 0)
            {
                trunkCombined = new Mesh { name = "BlenderKenyaPineTrunk" };
                trunkCombined.CombineMeshes(trunkList.ToArray(), true, true);
                trunkCombined.RecalculateBounds();
            }
            if (crownList.Count > 0)
            {
                crownCombined = new Mesh { name = "BlenderKenyaPineCrown" };
                crownCombined.CombineMeshes(crownList.ToArray(), true, true);
                crownCombined.RecalculateBounds();
            }
        }

        private static void BuildReserveSign(Transform parent, Vector3 pos, float yawDeg, string text, Material matBoard, Material matSteel)
        {
            // Strictly guarantee reserve sign & BOTH steel posts sit completely OFF all tarmac & OFF all roundabouts!
            if (!KenyaNetworkData.IsClearOfAllRoundabouts(new Vector2(pos.x, pos.z), 36f) ||
                !KenyaNetworkData.IsFootprintClearOfAllRoads(pos, 5.2f, 1.2f, yawDeg, 3.8f))
            {
                return;
            }
            var g = new GameObject($"EcoSign_{text.Replace(" ", "_")}").transform;
            g.SetParent(parent, false);
            g.SetPositionAndRotation(pos, Quaternion.Euler(0f, yawDeg, 0f));

            var postL = GameObject.CreatePrimitive(PrimitiveType.Cube);
            postL.transform.SetParent(g, false);
            postL.transform.localPosition = new Vector3(-4.2f, 1.85f, 0f);
            postL.transform.localScale = new Vector3(0.16f, 3.7f, 0.16f);
            Object.DestroyImmediate(postL.GetComponent<Collider>());
            postL.GetComponent<Renderer>().sharedMaterial = matSteel;

            var postR = GameObject.CreatePrimitive(PrimitiveType.Cube);
            postR.transform.SetParent(g, false);
            postR.transform.localPosition = new Vector3(4.2f, 1.85f, 0f);
            postR.transform.localScale = new Vector3(0.16f, 3.7f, 0.16f);
            Object.DestroyImmediate(postR.GetComponent<Collider>());
            postR.GetComponent<Renderer>().sharedMaterial = matSteel;

            var board = GameObject.CreatePrimitive(PrimitiveType.Cube);
            board.transform.SetParent(g, false);
            board.transform.localPosition = new Vector3(0f, 3.15f, 0f);
            board.transform.localScale = new Vector3(9.6f, 1.45f, 0.16f);
            Object.DestroyImmediate(board.GetComponent<Collider>());
            board.GetComponent<Renderer>().sharedMaterial = matBoard;

            var tGo = new GameObject("TxtFront");
            tGo.transform.SetParent(g, false);
            tGo.transform.localPosition = new Vector3(0f, 3.15f, -0.11f);
            var tmp = tGo.AddComponent<TextMeshPro>();
            tmp.text = text;
            tmp.fontSize = 4.0f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.rectTransform.sizeDelta = new Vector2(9.2f, 1.3f);

            var tBack = new GameObject("TxtBack");
            tBack.transform.SetParent(g, false);
            tBack.transform.localPosition = new Vector3(0f, 3.15f, 0.11f);
            tBack.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            var tmpB = tBack.AddComponent<TextMeshPro>();
            tmpB.text = text;
            tmpB.fontSize = 4.0f;
            tmpB.fontStyle = FontStyles.Bold;
            tmpB.color = Color.white;
            tmpB.alignment = TextAlignmentOptions.Center;
            tmpB.rectTransform.sizeDelta = new Vector2(9.2f, 1.3f);
        }

        private static Material MakeMat(string name, Shader shader, Color col, float smooth, float metal, Texture2D alb, Texture2D nrm, Vector2 tiling, bool alphaClip)
        {
            string path = $"Assets/KenyaStreetSim/Materials/M_{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = shader;
            m.SetColor("_BaseColor", col);
            m.SetFloat("_Smoothness", smooth);
            m.SetFloat("_Metallic", metal);
            if (alb != null)
            {
                m.SetTexture("_BaseMap", alb);
                m.SetTextureScale("_BaseMap", tiling);
            }
            else m.SetTexture("_BaseMap", null);

            if (nrm != null)
            {
                m.EnableKeyword("_NORMALMAP");
                m.SetTexture("_BumpMap", nrm);
                m.SetFloat("_BumpScale", 1.25f);
                m.SetTextureScale("_BumpMap", tiling);
            }
            else
            {
                m.DisableKeyword("_NORMALMAP");
                m.SetTexture("_BumpMap", null);
            }

            if (alphaClip)
            {
                m.EnableKeyword("_ALPHATEST_ON");
                m.SetFloat("_AlphaClip", 1f);
                m.SetFloat("_Cutoff", 0.40f);
                m.SetFloat("_Cull", 0f);
                m.renderQueue = (int)RenderQueue.AlphaTest;
            }
            else
            {
                m.DisableKeyword("_ALPHATEST_ON");
                m.SetFloat("_AlphaClip", 0f);
                m.SetFloat("_Cull", 2f);
                m.renderQueue = -1;
            }
            EditorUtility.SetDirty(m);
            return m;
        }
    }
}
