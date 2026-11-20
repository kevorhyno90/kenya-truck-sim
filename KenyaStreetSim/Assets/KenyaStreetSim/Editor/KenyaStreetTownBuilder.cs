using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace KenyaStreetSim.Editor
{
    /// <summary>
    /// RK 254 KE Realism Edition Town & Highway Builder:
    /// Generates high-res PBR Albedo + Normal maps, sculpted aerodynamic vehicle & branching leafy tree meshes,
    /// rolling Kenyan Ngong Hills / Rift Valley terrain, red murram verges, interlocking cabro sidewalks,
    /// KPLC utility poles & sagging overhead power cables, KeNHA overhead highway gantries, a lit Rubis Petrol Station,
    /// ReflectionProbes, Day/Sunset/Night lighting, working 3-color traffic lights, 18 sculpted LHT passing vehicles,
    /// 36 articulated pedestrians, 4 ENA Coach stages, switchable ENA Coach KDK 057M & EPH Isuzu FRR 90, and ZERO speed bumps.
    /// </summary>
    public static class KenyaStreetTownBuilder
    {
        private const string ScenePath = "Assets/KenyaStreetSim/Scenes/KenyaTownStreets.unity";
        private const string MatFolder = "Assets/KenyaStreetSim/Materials";
        private const string SettingsFolder = "Assets/KenyaStreetSim/Settings";
        private const string VolumeProfilePath = "Assets/KenyaStreetSim/Settings/KenyaStreetVolumeProfile.asset";
        private const string SkyMatPath = "Assets/KenyaStreetSim/Materials/KenyaSky.mat";

        private static readonly float[] IntersectionZs = { 95f, 245f, 395f };
        private static readonly string[] ShopNames =
        {
            "NAIROBI CHEMIST", "M-PESA AGENT", "JAVA CAFE", "KISUMU HARDWARE",
            "NAIVAS MART", "SAFARI HOTEL", "CYBER & PRINT", "QUICK BITES",
            "EQUITY AGENT", "SIMBA ELECTRONICS", "ELDORET SPARES", "KENYA PHARMACY",
            "UHURU BAKERY", "RIFT VALLEY OPTICS", "KILIMANI BOOKS", "SAVANNAH GRILL"
        };

        private static Dictionary<string, Material> matCache;
        private static Shader urpLitShader;
        private static Shader urpParticleShader;

        // Cached PBR Textures
        private static Texture2D texTarmacAlb, texTarmacNrm;
        private static Texture2D texMurramAlb, texMurramNrm;
        private static Texture2D texCabroAlb, texCabroNrm;
        private static Texture2D texPlasterAlb, texPlasterNrm;
        private static Texture2D texMabatiAlb, texMabatiNrm;
        private static Texture2D texBarkAlb, texBarkNrm;
        private static Texture2D texLeafCluster;
        private static Texture2D texTireAlb, texTireNrm;
        private static Texture2D texGlassAlb, texGlassNrm;

        [MenuItem("Kenya Street Sim/Build RK 254 KE Realism Scene")]
        public static string BuildKenyaTownScene()
        {
            EnsureFolders();

            // 1. Generate high-res PBR Albedo + Normal Maps & Sculpted 3D Meshes
            KenyaPbrTextureGenerator.GenerateAllTextures();
            KenyaSculptedMeshFactory.BuildAllMeshes();
            LoadGeneratedTextures();

            matCache = new Dictionary<string, Material>();
            urpLitShader = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLitShader == null)
            {
                throw new System.Exception("Universal Render Pipeline/Lit shader not found!");
            }
            urpParticleShader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? urpLitShader;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "KenyaTownStreets";

            var sunLight = ConfigureLightingAndPostProcessing(out var skyMat);
            var camObj = CreateMainCamera();

            var worldRoot = new GameObject("KenyaTownWorld");
            var streetLights = new List<Light>();
            var lampRenderers = new List<Renderer>();

            BuildRiftValleyTerrainAndRoadNetwork(worldRoot.transform);
            var trafficLights = BuildIntersectionsAndTrafficLights(worldRoot.transform);
            BuildTownBuildingsAndShops(worldRoot.transform);
            BuildRubisPetrolStation(worldRoot.transform, streetLights, lampRenderers);
            BuildKenhaHighwayGantriesAndBillboards(worldRoot.transform);
            BuildKplcPowerPolesAndCables(worldRoot.transform);
            BuildStreetLampsAndAcaciaTrees(worldRoot.transform, streetLights, lampRenderers);

            var stages = BuildBusStages(worldRoot.transform);
            BuildPassingTraffic(worldRoot.transform);
            BuildWalkingAndCrossingPedestrians(worldRoot.transform, trafficLights);

            // Build Sculpted Player Fleet (ENA Coach KDK 057M & EPH Isuzu FRR 90)
            var fleetRoot = new GameObject("PlayerFleet");
            var enaCoach = BuildEnaCoach(fleetRoot.transform, new Vector3(-3.5f, 0.28f, -18f));
            var isuzuTruck = BuildIsuzuFrr(fleetRoot.transform, new Vector3(-3.5f, 0.28f, -18f));
            isuzuTruck.gameObject.SetActive(false);

            // Runtime Systems: Audio, Time of Day, Game Loop, ETS2-Style uGUI HUD
            var sysRoot = new GameObject("KenyaStreetSystems");
            sysRoot.AddComponent<KenyaStreetAudio>();
            var tod = sysRoot.AddComponent<KenyaTimeOfDay>();
            tod.sunLight = sunLight;
            tod.skyboxMaterial = skyMat;
            tod.streetPointLights = streetLights.ToArray();
            tod.lampGlowRenderers = lampRenderers.ToArray();

            var hud = sysRoot.AddComponent<KenyaStreetHUD>();
            var loop = sysRoot.AddComponent<StreetGameLoop>();
            loop.fleet = new[] { enaCoach, isuzuTruck };
            loop.stages = stages;
            loop.streetCamera = camObj.GetComponent<KenyaStreetCamera>();
            loop.hud = hud;
            loop.startPosition = new Vector3(-3.5f, 0.28f, -18f);

            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene, ScenePath);
            SetBuildSettingsScene(ScenePath);

            int meshCount = Object.FindObjectsByType<MeshRenderer>().Length;
            int defaultMatCount = CountDefaultMaterials();
            return $"Built RK254KE Realism {ScenePath} | Renderers={meshCount} | DefaultMaterials={defaultMatCount} | Stages={stages.Length} | TrafficLights={trafficLights.Length} | SpeedBumps=0";
        }

        private static void EnsureFolders()
        {
            string[] dirs =
            {
                "Assets/KenyaStreetSim",
                "Assets/KenyaStreetSim/Scenes",
                MatFolder,
                SettingsFolder,
                KenyaPbrTextureGenerator.TexFolder,
                KenyaSculptedMeshFactory.MeshFolder
            };
            foreach (var d in dirs)
            {
                if (!Directory.Exists(d)) Directory.CreateDirectory(d);
            }
            AssetDatabase.Refresh();
        }

        private static void LoadGeneratedTextures()
        {
            string tf = KenyaPbrTextureGenerator.TexFolder;
            texTarmacAlb = AssetDatabase.LoadAssetAtPath<Texture2D>($"{tf}/Tarmac_Albedo.png");
            texTarmacNrm = AssetDatabase.LoadAssetAtPath<Texture2D>($"{tf}/Tarmac_Normal.png");
            texMurramAlb = AssetDatabase.LoadAssetAtPath<Texture2D>($"{tf}/Murram_Albedo.png");
            texMurramNrm = AssetDatabase.LoadAssetAtPath<Texture2D>($"{tf}/Murram_Normal.png");
            texCabroAlb = AssetDatabase.LoadAssetAtPath<Texture2D>($"{tf}/Cabro_Albedo.png");
            texCabroNrm = AssetDatabase.LoadAssetAtPath<Texture2D>($"{tf}/Cabro_Normal.png");
            texPlasterAlb = AssetDatabase.LoadAssetAtPath<Texture2D>($"{tf}/Plaster_Albedo.png");
            texPlasterNrm = AssetDatabase.LoadAssetAtPath<Texture2D>($"{tf}/Plaster_Normal.png");
            texMabatiAlb = AssetDatabase.LoadAssetAtPath<Texture2D>($"{tf}/Mabati_Albedo.png");
            texMabatiNrm = AssetDatabase.LoadAssetAtPath<Texture2D>($"{tf}/Mabati_Normal.png");
            texBarkAlb = AssetDatabase.LoadAssetAtPath<Texture2D>($"{tf}/Bark_Albedo.png");
            texBarkNrm = AssetDatabase.LoadAssetAtPath<Texture2D>($"{tf}/Bark_Normal.png");
            texLeafCluster = AssetDatabase.LoadAssetAtPath<Texture2D>($"{tf}/LeafCluster_Albedo.png");
            texTireAlb = AssetDatabase.LoadAssetAtPath<Texture2D>($"{tf}/TireTread_Albedo.png");
            texTireNrm = AssetDatabase.LoadAssetAtPath<Texture2D>($"{tf}/TireTread_Normal.png");
            texGlassAlb = AssetDatabase.LoadAssetAtPath<Texture2D>($"{tf}/WindowFacade_Albedo.png");
            texGlassNrm = AssetDatabase.LoadAssetAtPath<Texture2D>($"{tf}/WindowFacade_Normal.png");
        }

        private static Material GetMat(
            string key, Color color, float smoothness = 0.35f, float metallic = 0f,
            Color? emission = null, Texture2D albedoTex = null, Texture2D normalTex = null,
            Vector2? tiling = null, float bumpScale = 1f, bool alphaClipDoubleSided = false)
        {
            if (matCache.TryGetValue(key, out var cached) && cached != null)
            {
                return cached;
            }

            string path = $"{MatFolder}/M_{key}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(urpLitShader);
                AssetDatabase.CreateAsset(mat, path);
            }
            else if (mat.shader != urpLitShader)
            {
                mat.shader = urpLitShader;
            }

            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", smoothness);
            mat.SetFloat("_Metallic", metallic);

            if (albedoTex != null)
            {
                mat.SetTexture("_BaseMap", albedoTex);
                if (tiling.HasValue) mat.SetTextureScale("_BaseMap", tiling.Value);
                else mat.SetTextureScale("_BaseMap", Vector2.one);
            }
            else
            {
                mat.SetTexture("_BaseMap", null);
            }

            if (normalTex != null)
            {
                mat.EnableKeyword("_NORMALMAP");
                mat.SetTexture("_BumpMap", normalTex);
                mat.SetFloat("_BumpScale", bumpScale);
                if (tiling.HasValue) mat.SetTextureScale("_BumpMap", tiling.Value);
            }
            else
            {
                mat.DisableKeyword("_NORMALMAP");
                mat.SetTexture("_BumpMap", null);
            }

            if (alphaClipDoubleSided)
            {
                mat.EnableKeyword("_ALPHATEST_ON");
                mat.SetFloat("_AlphaClip", 1f);
                mat.SetFloat("_Cutoff", 0.42f);
                mat.SetFloat("_Cull", 0f); // Double-sided leaves
                mat.renderQueue = (int)RenderQueue.AlphaTest;
            }
            else
            {
                mat.DisableKeyword("_ALPHATEST_ON");
                mat.SetFloat("_AlphaClip", 0f);
                mat.SetFloat("_Cull", 2f); // Backface cull
                mat.renderQueue = -1;
            }

            if (emission.HasValue)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", emission.Value);
            }
            else
            {
                mat.DisableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", Color.black);
            }

            EditorUtility.SetDirty(mat);
            matCache[key] = mat;
            return mat;
        }

        private static Material GetParticleMat(string key, Color color)
        {
            string path = $"{MatFolder}/M_{key}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(urpParticleShader);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetColor("_BaseColor", color);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Light ConfigureLightingAndPostProcessing(out Material skyMat)
        {
            var sunGo = new GameObject("Directional Light");
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = Palette.SunKey;
            sun.intensity = 1.35f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.80f;
            sunGo.transform.rotation = Quaternion.Euler(40f, -34f, 0f);

            skyMat = AssetDatabase.LoadAssetAtPath<Material>(SkyMatPath);
            if (skyMat == null)
            {
                var skyShader = Shader.Find("Skybox/Procedural");
                skyMat = new Material(skyShader);
                AssetDatabase.CreateAsset(skyMat, SkyMatPath);
            }
            skyMat.SetColor("_SkyTint", Palette.SkyTint);
            skyMat.SetColor("_GroundColor", Palette.SkyGround);
            skyMat.SetFloat("_AtmosphereThickness", 0.85f);
            skyMat.SetFloat("_SunSize", 0.032f);
            skyMat.SetFloat("_Exposure", 1.18f);
            EditorUtility.SetDirty(skyMat);

            RenderSettings.skybox = skyMat;
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Palette.AmbientSky;
            RenderSettings.ambientEquatorColor = Palette.AmbientEquator;
            RenderSettings.ambientGroundColor = Palette.AmbientGround;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = Palette.FogColor;
            RenderSettings.fogDensity = 0.0016f;

            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumeProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, VolumeProfilePath);
            }
            if (!profile.TryGet<Tonemapping>(out var tm)) tm = profile.Add<Tonemapping>();
            tm.mode.overrideState = true;
            tm.mode.value = TonemappingMode.ACES;

            if (!profile.TryGet<Bloom>(out var bl)) bl = profile.Add<Bloom>();
            bl.threshold.overrideState = true;
            bl.threshold.value = 1.05f;
            bl.intensity.overrideState = true;
            bl.intensity.value = 0.34f;
            bl.scatter.overrideState = true;
            bl.scatter.value = 0.68f;

            if (!profile.TryGet<Vignette>(out var vig)) vig = profile.Add<Vignette>();
            vig.intensity.overrideState = true;
            vig.intensity.value = 0.16f;
            vig.smoothness.overrideState = true;
            vig.smoothness.value = 0.38f;

            if (!profile.TryGet<ColorAdjustments>(out var ca)) ca = profile.Add<ColorAdjustments>();
            ca.saturation.overrideState = true;
            ca.saturation.value = 10f;
            ca.contrast.overrideState = true;
            ca.contrast.value = 8f;
            ca.postExposure.overrideState = true;
            ca.postExposure.value = 0.06f;
            EditorUtility.SetDirty(profile);

            var volGo = new GameObject("Global Volume");
            var vol = volGo.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.priority = 1f;
            vol.sharedProfile = profile;

            return sun;
        }

        private static GameObject CreateMainCamera()
        {
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            camGo.transform.position = new Vector3(-9f, 4.8f, -30f);
            camGo.transform.LookAt(new Vector3(-2f, 2.2f, 15f));

            var cam = camGo.AddComponent<Camera>();
            cam.allowHDR = true;
            cam.fieldOfView = 54f;
            cam.nearClipPlane = 0.12f;
            cam.farClipPlane = 950f;

            var camData = camGo.AddComponent<UniversalAdditionalCameraData>();
            camData.renderPostProcessing = true;
            camData.antialiasing = AntialiasingMode.FastApproximateAntialiasing;

            camGo.AddComponent<AudioListener>();
            camGo.AddComponent<KenyaStreetCamera>();
            return camGo;
        }

        private static void BuildRiftValleyTerrainAndRoadNetwork(Transform parent)
        {
            var roadRoot = new GameObject("RoadNetworkAndRiftTerrain").transform;
            roadRoot.SetParent(parent, false);

            var matTerrain = GetMat("RiftMurramTerrain", Color.white, 0.08f, 0f, null, texMurramAlb, texMurramNrm, Vector2.one, 1.35f);
            var matMurramEdge = GetMat("MurramShoulder", Color.white, 0.10f, 0f, null, texMurramAlb, texMurramNrm, new Vector2(1.5f, 35f), 1.3f);
            var matTarmacMain = GetMat("TarmacMain", Color.white, 0.30f, 0f, null, texTarmacAlb, texTarmacNrm, new Vector2(2f, 92f), 1.25f);
            var matTarmacCross = GetMat("TarmacCross", Color.white, 0.30f, 0f, null, texTarmacAlb, texTarmacNrm, new Vector2(12f, 2f), 1.25f);
            var matCabroSidewalk = GetMat("CabroSidewalk", Color.white, 0.22f, 0f, null, texCabroAlb, texCabroNrm, new Vector2(2f, 38f), 1.2f);
            var matCabroCross = GetMat("CabroCross", Color.white, 0.22f, 0f, null, texCabroAlb, texCabroNrm, new Vector2(16f, 2f), 1.2f);
            var matCurb = GetMat("CurbStone", Palette.CurbStone, 0.22f, 0f, null, texPlasterAlb, texPlasterNrm, new Vector2(1f, 30f), 0.9f);
            var matWhiteLine = GetMat("RoadLineWhite", Palette.RoadLineWhite, 0.15f);
            var matYellowLine = GetMat("RoadLineYellow", Palette.RoadLineYellow, 0.18f);

            // Rolling Kenyan Ngong Hills / Rift Valley terrain mesh!
            var terrainGo = CreateCustomMesh("KenyanRiftValleyHills", roadRoot, Vector3.zero, Quaternion.identity, Vector3.one, KenyaSculptedMeshFactory.RiftValleyTerrainMesh, matTerrain, withMeshCollider: false);
            terrainGo.isStatic = true;

            // Flat physics road sub-base
            CreateBox("RoadPhysicsBase", roadRoot, new Vector3(0f, -0.35f, 240f), new Vector3(300f, 0.4f, 820f), matTerrain, withCollider: true);

            // Main PBR Textured Tarmac Boulevard: Z = -95 to +575 (670m long, 14m wide, ZERO speed bumps!)
            CreateBox("MainBoulevardTarmac", roadRoot, new Vector3(0f, -0.10f, 240f), new Vector3(14.0f, 0.20f, 670f), matTarmacMain, withCollider: true);

            float[,] segs =
            {
                { -95f, 88f },
                { 102f, 238f },
                { 252f, 388f },
                { 402f, 575f }
            };

            for (int s = 0; s < segs.GetLength(0); s++)
            {
                float z0 = segs[s, 0];
                float z1 = segs[s, 1];
                float len = z1 - z0;
                float zMid = (z0 + z1) * 0.5f;

                for (int side = -1; side <= 1; side += 2)
                {
                    // Curb stone along road edge
                    CreateBox($"Curb_{s}_{side}", roadRoot, new Vector3(side * 7.22f, 0.08f, zMid), new Vector3(0.44f, 0.16f, len), matCurb, withCollider: true);
                    // Interlocking Kenyan Cabro sidewalk
                    CreateBox($"Sidewalk_{s}_{side}", roadRoot, new Vector3(side * 9.85f, 0.08f, zMid), new Vector3(4.82f, 0.16f, len), matCabroSidewalk, withCollider: true);
                    // Red Kenyan murram earth verge behind sidewalks
                    CreateBox($"MurramVerge_{s}_{side}", roadRoot, new Vector3(side * 24f, -0.04f, zMid), new Vector3(24f, 0.12f, len), matMurramEdge, withCollider: false);
                    // Crisp white tarmac edge line
                    CreateBox($"EdgeLine_{s}_{side}", roadRoot, new Vector3(side * 6.55f, 0.008f, zMid), new Vector3(0.16f, 0.016f, len), matWhiteLine, withCollider: false);
                }

                // Kenyan yellow center stripes & dashed white overtake lines
                for (float z = z0 + 4f; z < z1 - 4f; z += 7.5f)
                {
                    bool nearInt = (z1 - z < 16f && s < 3) || (z - z0 < 16f && s > 0);
                    if (nearInt)
                    {
                        CreateBox($"CenterSolidL_{z:0}", roadRoot, new Vector3(-0.18f, 0.008f, z), new Vector3(0.14f, 0.016f, 7.2f), matYellowLine, withCollider: false);
                        CreateBox($"CenterSolidR_{z:0}", roadRoot, new Vector3(0.18f, 0.008f, z), new Vector3(0.14f, 0.016f, 7.2f), matYellowLine, withCollider: false);
                    }
                    else
                    {
                        CreateBox($"CenterDash_{z:0}", roadRoot, new Vector3(0f, 0.008f, z), new Vector3(0.18f, 0.016f, 3.8f), matYellowLine, withCollider: false);
                    }
                }
            }

            // Cross streets extending 85m east and west (zero overlap with main tarmac — zero Z-fighting)
            foreach (float intZ in IntersectionZs)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    float xMid = side * 49.5f;
                    CreateBox($"CrossRoad_{intZ}_{side}", roadRoot, new Vector3(xMid, -0.10f, intZ), new Vector3(85f, 0.20f, 14f), matTarmacCross, withCollider: true);
                    CreateBox($"CrossWalkN_{intZ}_{side}", roadRoot, new Vector3(xMid, 0.08f, intZ + 9.4f), new Vector3(85f, 0.16f, 4.8f), matCabroCross, withCollider: true);
                    CreateBox($"CrossWalkS_{intZ}_{side}", roadRoot, new Vector3(xMid, 0.08f, intZ - 9.4f), new Vector3(85f, 0.16f, 4.8f), matCabroCross, withCollider: true);
                    CreateBox($"CrossCurbN_{intZ}_{side}", roadRoot, new Vector3(xMid, 0.08f, intZ + 7.22f), new Vector3(85f, 0.16f, 0.44f), matCurb, withCollider: true);
                    CreateBox($"CrossCurbS_{intZ}_{side}", roadRoot, new Vector3(xMid, 0.08f, intZ - 7.22f), new Vector3(85f, 0.16f, 0.44f), matCurb, withCollider: true);

                    for (float x = 14f; x < 84f; x += 8f)
                    {
                        CreateBox($"CrossDash_{intZ}_{side}_{x:0}", roadRoot, new Vector3(side * x, 0.008f, intZ), new Vector3(3.8f, 0.016f, 0.18f), matYellowLine, withCollider: false);
                    }
                }

                // Zebra Pedestrian Crossings + Stop Bars
                for (int end = -1; end <= 1; end += 2)
                {
                    float zebraZ = intZ + end * 8.4f;
                    for (float x = -5.4f; x <= 5.5f; x += 1.2f)
                    {
                        CreateBox($"ZebraMain_{intZ}_{end}_{x:0.0}", roadRoot, new Vector3(x, 0.010f, zebraZ), new Vector3(0.58f, 0.014f, 2.8f), matWhiteLine, withCollider: false);
                    }
                    float stopZ = intZ + end * 10.6f;
                    float stopX = end < 0 ? -3.4f : 3.4f;
                    CreateBox($"StopBarMain_{intZ}_{end}", roadRoot, new Vector3(stopX, 0.010f, stopZ), new Vector3(6.2f, 0.014f, 0.42f), matWhiteLine, withCollider: false);
                }

                for (int side = -1; side <= 1; side += 2)
                {
                    float zebraX = side * 8.4f;
                    for (float z = -5.2f; z <= 5.3f; z += 1.2f)
                    {
                        CreateBox($"ZebraCross_{intZ}_{side}_{z:0.0}", roadRoot, new Vector3(zebraX, 0.010f, intZ + z), new Vector3(2.8f, 0.014f, 0.58f), matWhiteLine, withCollider: false);
                    }
                }
            }

            // Kenyan red-and-white chevron safety end markers at far ends
            var matRed = GetMat("ChevronRed", Palette.EnaSkirtRed, 0.35f);
            CreateBox("SouthChevron", roadRoot, new Vector3(0f, 0.45f, -86f), new Vector3(14.5f, 0.9f, 0.8f), matRed, withCollider: true);
            CreateBox("NorthChevron", roadRoot, new Vector3(0f, 0.45f, 558f), new Vector3(14.5f, 0.9f, 0.8f), matRed, withCollider: true);
        }

        private static void BuildRubisPetrolStation(Transform parent, List<Light> pointLights, List<Renderer> glowRenderers)
        {
            // Authentic Kenyan RUBIS / TOTAL Highway Petrol Station on the East side at Z = 152
            var root = new GameObject("RubisKenyaPetrolStation").transform;
            root.SetParent(parent, false);
            root.position = new Vector3(22.5f, 0.16f, 152f);

            var matConcrete = GetMat("StationConcrete", Palette.Sidewalk, 0.25f, 0f, null, texCabroAlb, texCabroNrm, new Vector2(6f, 8f), 1.0f);
            var matGreen = GetMat("RubisGreen", Palette.Hex("#0F8A4B"), 0.55f, 0.15f);
            var matRed = GetMat("RubisRed", Palette.Hex("#D4262C"), 0.55f, 0.15f);
            var matWhite = GetMat("RubisWhite", Palette.EnaBodyWhite, 0.55f);
            var matDark = GetMat("StationDark", Palette.SignalHousing, 0.4f, 0.4f);
            var matGlow = GetMat("CanopyLightGlow", Palette.LampGlow, 0.7f, 0f, Palette.LampGlow * 2.8f);
            var matGlass = GetMat("StationGlass", Color.white, 0.9f, 0.2f, null, texGlassAlb, texGlassNrm, new Vector2(4f, 1f), 0.9f);

            // Paved forecourt apron extending cleanly to the sidewalk
            CreateBox("ForecourtPad", root, new Vector3(-2f, 0.02f, 0f), new Vector3(20f, 0.08f, 34f), matConcrete, withCollider: true);

            // Tall illuminated petrol canopy supported by 4 steel columns
            CreateBox("CanopyTopWhite", root, new Vector3(-1.5f, 5.45f, 0f), new Vector3(11.5f, 0.45f, 20f), matWhite, false);
            CreateBox("CanopyFasciaGreen", root, new Vector3(-1.5f, 5.80f, 0f), new Vector3(11.7f, 0.52f, 20.2f), matGreen, false);
            CreateBox("CanopyStripeRed", root, new Vector3(-1.5f, 5.38f, 0f), new Vector3(11.75f, 0.20f, 20.25f), matRed, false);
            CreateWorldSignText("CanopyBrandSign", root, "RUBIS ENERGY  KENYA", new Vector3(-7.42f, 5.82f, 0f), new Vector2(18f, 0.85f), 5.0f, Palette.EnaBodyWhite, Quaternion.Euler(0f, 90f, 0f), noWrap: true);

            float[] colZ = { -6.5f, 6.5f };
            float[] colX = { -4.5f, 1.5f };
            foreach (float cx in colX)
            {
                foreach (float cz in colZ)
                {
                    CreateBox($"CanopyPillar_{cx}_{cz}", root, new Vector3(cx, 2.7f, cz), new Vector3(0.55f, 5.4f, 0.55f), matWhite, withCollider: true);
                }
            }

            // Fuel pump islands + glowing under-canopy LED fixtures
            for (int i = -1; i <= 1; i += 2)
            {
                float iz = i * 4.5f;
                CreateBox($"PumpIsland_{i}", root, new Vector3(-1.5f, 0.18f, iz), new Vector3(1.6f, 0.24f, 4.2f), matDark, true);
                CreateBox($"PumpA_{i}", root, new Vector3(-1.5f, 1.15f, iz - 1.1f), new Vector3(0.85f, 1.75f, 0.65f), matGreen, true);
                CreateBox($"PumpB_{i}", root, new Vector3(-1.5f, 1.15f, iz + 1.1f), new Vector3(0.85f, 1.75f, 0.65f), matRed, true);

                var led = CreateBox($"CanopyLed_{i}", root, new Vector3(-1.5f, 5.20f, iz), new Vector3(2.2f, 0.08f, 1.4f), matGlow, false);
                glowRenderers.Add(led.GetComponent<Renderer>());

                var plGo = new GameObject($"CanopyPointLight_{i}");
                plGo.transform.SetParent(root, false);
                plGo.transform.localPosition = new Vector3(-1.5f, 4.85f, iz);
                var pl = plGo.AddComponent<Light>();
                pl.type = LightType.Point;
                pl.color = Palette.HeadlightWarm;
                pl.range = 24f;
                pl.intensity = 0f;
                pl.enabled = false;
                pointLights.Add(pl);
            }

            // Convenience store (RUBIS EXPRESS) behind pumps
            CreateBox("ShopBuilding", root, new Vector3(7.2f, 2.2f, 0f), new Vector3(6.5f, 4.4f, 20f), matWhite, true);
            CreateBox("ShopBandGreen", root, new Vector3(7.2f, 4.15f, 0f), new Vector3(6.6f, 0.65f, 20.1f), matGreen, false);
            CreateBox("ShopGlassFront", root, new Vector3(3.92f, 1.75f, 0f), new Vector3(0.14f, 2.7f, 15.5f), matGlass, false);
            CreateWorldSignText("RubisMartSign", root, "RUBIS EXPRESS  •  24 HRS  •  M-PESA", new Vector3(3.85f, 4.15f, 0f), new Vector2(18f, 0.7f), 3.6f, Palette.EnaBodyWhite, Quaternion.Euler(0f, 90f, 0f), noWrap: true);

            // Tall roadside price totem pylon right near the curb!
            CreateBox("PriceTotemPole", root, new Vector3(-9.5f, 3.8f, -13.5f), new Vector3(1.55f, 7.6f, 0.45f), matDark, true);
            CreateBox("PriceTotemTop", root, new Vector3(-9.5f, 6.85f, -13.5f), new Vector3(1.75f, 1.5f, 0.52f), matGreen, false);
            CreateWorldSignText("TotemPriceText", root, "RUBIS\nSUPER 180.50\nDIESEL 168.20", new Vector3(-9.5f, 5.1f, -13.78f), new Vector2(1.65f, 3.6f), 2.1f, Palette.UiAccentAmber, Quaternion.identity, noWrap: false);
        }

        private static void BuildKenhaHighwayGantriesAndBillboards(Transform parent)
        {
            var root = new GameObject("KenhaHighwaySignsAndBillboards").transform;
            root.SetParent(parent, false);

            var matSteel = GetMat("GantrySteel", Palette.LampPostMetal, 0.55f, 0.78f);
            var matKenhaGreen = GetMat("KenhaGreen", Palette.Hex("#0E6B3A"), 0.42f);
            var matSafeGreen = GetMat("SafaricomGreen", Palette.Hex("#28A745"), 0.45f);
            var matEnaRed = GetMat("BillboardEnaRed", Palette.EnaSkirtRed, 0.45f);
            var matWhite = GetMat("SignBorderWhite", Palette.EnaBodyWhite, 0.35f);

            // 2 Overhead KeNHA Kenyan Highway Gantry Signs spanning the entire boulevard!
            var gantries = new (float z, string mainText)[]
            {
                (58f,  "NAIROBI CBD  •  MOI AVENUE  •  WESTLANDS\nKEEP LEFT  •  NO SPEED BUMPS"),
                (305f, "WESTLANDS  •  NAKURU  •  KISUMU  (A104)\nKAJIADO  ◄   |   ►  THIKA ROAD")
            };

            foreach (var g in gantries)
            {
                var tr = new GameObject($"KenhaGantry_{g.z:0}").transform;
                tr.SetParent(root, false);
                tr.position = new Vector3(0f, 0f, g.z);

                // Left & right steel truss legs + horizontal overhead bridge beam (7.4m clearance!)
                CreateCylinder("LegL", tr, new Vector3(-8.4f, 3.85f, 0f), new Vector3(0.36f, 3.85f, 0.36f), matSteel);
                CreateCylinder("LegR", tr, new Vector3( 8.4f, 3.85f, 0f), new Vector3(0.36f, 3.85f, 0.36f), matSteel);
                CreateBox("CrossTrussTop", tr, new Vector3(0f, 7.65f, 0f), new Vector3(17.4f, 0.36f, 0.42f), matSteel, false);
                CreateBox("CrossTrussBot", tr, new Vector3(0f, 6.75f, 0f), new Vector3(17.4f, 0.28f, 0.42f), matSteel, false);

                // White-bordered green KeNHA highway sign board facing oncoming drivers (-Z)
                CreateBox("SignFrame", tr, new Vector3(0f, 7.25f, -0.26f), new Vector3(12.2f, 2.25f, 0.14f), matWhite, false);
                CreateBox("SignBoard", tr, new Vector3(0f, 7.25f, -0.30f), new Vector3(11.9f, 1.98f, 0.14f), matKenhaGreen, false);
                CreateWorldSignText("SignText", tr, g.mainText, new Vector3(0f, 7.25f, -0.40f), new Vector2(11.5f, 1.85f), 4.2f, Palette.EnaBodyWhite, Quaternion.identity, noWrap: false);
            }

            // Roadside Kenyan Billboards (Safaricom M-PESA & ENA Coach Express)
            BuildBillboard(root, new Vector3(12.4f, 0.16f, 22f), -16f, "SAFARICOM 5G  •  M-PESA\nTUWAWEZESHE KENYA", matSafeGreen, matSteel, matWhite);
            BuildBillboard(root, new Vector3(-12.4f, 0.16f, 215f), 16f, "ENA COACH  EXPRESS\nNAIROBI  •  KISUMU  •  KISII  •  MIGORI", matEnaRed, matSteel, matWhite);
        }

        private static void BuildBillboard(Transform parent, Vector3 pos, float yawDeg, string text, Material boardMat, Material poleMat, Material borderMat)
        {
            var bb = new GameObject("KenyaBillboard").transform;
            bb.SetParent(parent, false);
            bb.position = pos;
            bb.rotation = Quaternion.Euler(0f, yawDeg, 0f);

            CreateCylinder("Pole", bb, new Vector3(0f, 3.6f, 0f), new Vector3(0.42f, 3.6f, 0.42f), poleMat);
            CreateBox("Frame", bb, new Vector3(0f, 7.3f, 0f), new Vector3(7.6f, 3.1f, 0.28f), borderMat, false);
            CreateBox("Face", bb, new Vector3(0f, 7.3f, -0.10f), new Vector3(7.3f, 2.82f, 0.22f), boardMat, false);
            CreateWorldSignText("Text", bb, text, new Vector3(0f, 7.3f, -0.24f), new Vector2(7.0f, 2.5f), 3.6f, Palette.EnaBodyWhite, Quaternion.identity, noWrap: false);
        }

        private static void BuildKplcPowerPolesAndCables(Transform parent)
        {
            // Authentic Kenya Power (KPLC) wooden/concrete poles with crossarms & sagging overhead power cables!
            var root = new GameObject("KPLC_PowerLines").transform;
            root.SetParent(parent, false);

            var matWoodPole = GetMat("KplcWoodPole", Palette.Hex("#5B4330"), 0.18f, 0f, null, texBarkAlb, texBarkNrm, new Vector2(1f, 4f), 1.1f);
            var matCrossarm = GetMat("KplcSteel", Palette.LampPostMetal, 0.5f, 0.65f);
            var matWire = GetMat("PowerCableBlack", Palette.SignalHousing, 0.2f);

            var eastPolePos = new List<Vector3>();
            for (float z = -55f; z <= 535f; z += 34f)
            {
                if (IsNearIntersection(z, 13f) || Mathf.Abs(z - 152f) < 22f) continue;

                Vector3 p = new Vector3(11.3f, 0.16f, z);
                var pole = new GameObject($"KPLC_Pole_{z:0}").transform;
                pole.SetParent(root, false);
                pole.position = p;

                CreateCylinder("PoleShaft", pole, new Vector3(0f, 4.9f, 0f), new Vector3(0.24f, 4.9f, 0.24f), matWoodPole);
                CreateBox("CrossarmTop", pole, new Vector3(0f, 9.2f, 0f), new Vector3(2.2f, 0.14f, 0.14f), matCrossarm, false);
                CreateBox("CrossarmLow", pole, new Vector3(0f, 8.3f, 0f), new Vector3(1.8f, 0.12f, 0.12f), matCrossarm, false);
                eastPolePos.Add(p);
            }

            // Connect consecutive KPLC poles with sagging overhead catenary cables (LineRenderer)
            for (int i = 0; i < eastPolePos.Count - 1; i++)
            {
                Vector3 a = eastPolePos[i];
                Vector3 b = eastPolePos[i + 1];
                if (Vector3.Distance(a, b) > 75f) continue;

                float[] offsetsX = { -0.85f, 0f, 0.85f };
                foreach (float ox in offsetsX)
                {
                    CreateSaggingCable(root, $"Cable_{i}_{ox:0.0}", a + new Vector3(ox, 9.2f, 0f), b + new Vector3(ox, 9.2f, 0f), 0.72f, matWire);
                }
            }
        }

        private static void CreateSaggingCable(Transform parent, string name, Vector3 start, Vector3 end, float sagMeters, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.sharedMaterial = mat;
            lr.startWidth = 0.045f;
            lr.endWidth = 0.045f;
            lr.useWorldSpace = true;
            lr.shadowCastingMode = ShadowCastingMode.Off;
            lr.receiveShadows = false;
            const int pts = 9;
            lr.positionCount = pts;
            for (int i = 0; i < pts; i++)
            {
                float t = (float)i / (pts - 1);
                Vector3 pos = Vector3.Lerp(start, end, t);
                pos.y -= Mathf.Sin(t * Mathf.PI) * sagMeters;
                lr.SetPosition(i, pos);
            }
        }

        private static StreetTrafficLight[] BuildIntersectionsAndTrafficLights(Transform parent)
        {
            var root = new GameObject("TrafficIntersections").transform;
            root.SetParent(parent, false);

            var matPole = GetMat("LampPostMetal", Palette.LampPostMetal, 0.55f, 0.75f);
            var matHousing = GetMat("SignalHousing", Palette.SignalHousing, 0.35f, 0.3f);
            var matLens = GetMat("SignalLensBase", Palette.SignalOff, 0.7f, 0.1f, Color.black);

            var list = new List<StreetTrafficLight>();
            for (int i = 0; i < IntersectionZs.Length; i++)
            {
                float intZ = IntersectionZs[i];
                var go = new GameObject($"TrafficIntersection_{i + 1}");
                go.transform.SetParent(root, false);
                go.transform.position = new Vector3(0f, 0f, intZ);

                var ctrl = go.AddComponent<StreetTrafficLight>();
                ctrl.intersectionZ = intZ;
                ctrl.phaseOffsetSeconds = i * 5.5f;

                var mainRed = new List<Renderer>();
                var mainYel = new List<Renderer>();
                var mainGrn = new List<Renderer>();
                var crossRed = new List<Renderer>();
                var crossYel = new List<Renderer>();
                var crossGrn = new List<Renderer>();

                BuildTrafficGantry(go.transform, new Vector3(-8.2f, 0.16f, intZ + 8.2f), 180f, matPole, matHousing, matLens, mainRed, mainYel, mainGrn);
                BuildTrafficGantry(go.transform, new Vector3(8.2f, 0.16f, intZ - 8.2f), 0f, matPole, matHousing, matLens, mainRed, mainYel, mainGrn);
                BuildTrafficGantry(go.transform, new Vector3(8.2f, 0.16f, intZ + 8.2f), 270f, matPole, matHousing, matLens, crossRed, crossYel, crossGrn);
                BuildTrafficGantry(go.transform, new Vector3(-8.2f, 0.16f, intZ - 8.2f), 90f, matPole, matHousing, matLens, crossRed, crossYel, crossGrn);

                ctrl.mainRedLenses = mainRed.ToArray();
                ctrl.mainYellowLenses = mainYel.ToArray();
                ctrl.mainGreenLenses = mainGrn.ToArray();
                ctrl.crossRedLenses = crossRed.ToArray();
                ctrl.crossYellowLenses = crossYel.ToArray();
                ctrl.crossGreenLenses = crossGrn.ToArray();

                list.Add(ctrl);
            }

            return list.ToArray();
        }

        private static void BuildTrafficGantry(
            Transform parent, Vector3 worldPos, float yawDeg,
            Material matPole, Material matHousing, Material matLens,
            List<Renderer> redList, List<Renderer> yelList, List<Renderer> grnList)
        {
            var g = new GameObject("TrafficGantry").transform;
            g.SetParent(parent, true);
            g.position = worldPos;
            g.rotation = Quaternion.Euler(0f, yawDeg, 0f);

            CreateBox("Pole", g, new Vector3(0f, 3.1f, 0f), new Vector3(0.24f, 6.2f, 0.24f), matPole, withCollider: true);
            CreateBox("Arm", g, new Vector3(-2.4f, 5.9f, 0f), new Vector3(5.0f, 0.20f, 0.20f), matPole, withCollider: false);

            BuildSignalHead(g, new Vector3(-4.2f, 5.35f, -0.18f), matHousing, matLens, redList, yelList, grnList);
            BuildSignalHead(g, new Vector3(-0.3f, 3.4f, -0.18f), matHousing, matLens, redList, yelList, grnList);
        }

        private static void BuildSignalHead(
            Transform parent, Vector3 localPos,
            Material matHousing, Material matLens,
            List<Renderer> redList, List<Renderer> yelList, List<Renderer> grnList)
        {
            var head = new GameObject("SignalHead").transform;
            head.SetParent(parent, false);
            head.localPosition = localPos;

            CreateBox("Box", head, Vector3.zero, new Vector3(0.46f, 1.24f, 0.34f), matHousing, withCollider: false);
            redList.Add(CreateSphere("LensRed", head, new Vector3(0f, 0.36f, -0.16f), Vector3.one * 0.28f, matLens));
            yelList.Add(CreateSphere("LensYel", head, new Vector3(0f, 0.0f, -0.16f), Vector3.one * 0.28f, matLens));
            grnList.Add(CreateSphere("LensGrn", head, new Vector3(0f, -0.36f, -0.16f), Vector3.one * 0.28f, matLens));
        }

        private static void BuildTownBuildingsAndShops(Transform parent)
        {
            var bRoot = new GameObject("KenyanTownBuildings").transform;
            bRoot.SetParent(parent, false);

            var matTrim = GetMat("TrimDark", Palette.TrimDark, 0.25f, 0.1f, null, texPlasterAlb, texPlasterNrm, new Vector2(2f, 1f), 0.7f);
            var matGlass = GetMat("GlassWindowPBR", Color.white, 0.90f, 0.30f, null, texGlassAlb, texGlassNrm, Vector2.one, 1.0f);
            var matShopGlass = GetMat("ShopGlassPBR", Color.white, 0.88f, 0.25f, null, texGlassAlb, texGlassNrm, new Vector2(4f, 1f), 1.0f);
            var matFrame = GetMat("WindowFrame", Palette.WindowFrame, 0.4f, 0.2f);
            var matRoofMabati = GetMat("RoofMabati", Palette.RoofIron, 0.38f, 0.45f, null, texMabatiAlb, texMabatiNrm, new Vector2(4f, 3f), 1.35f);
            var matSign = GetMat("ShopSignBoard", Palette.ShopSignBoard, 0.35f);
            var matTankBlk = GetMat("WaterTankBlack", Palette.WaterTankBlack, 0.35f);
            var matTankBlu = GetMat("WaterTankBlue", Palette.WaterTankBlue, 0.38f);
            var matRail = GetMat("BalconyRailMetal", Palette.LampPostMetal, 0.5f, 0.7f);

            var rng = new System.Random(202607);
            int shopIdx = 0;

            float[,] blocks =
            {
                { -68f, 76f },
                { 112f, 228f },
                { 262f, 378f },
                { 412f, 535f }
            };

            for (int b = 0; b < blocks.GetLength(0); b++)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    float z = blocks[b, 0];
                    while (z < blocks[b, 1] - 11f)
                    {
                        float widthZ = 12f + (float)rng.NextDouble() * 7.5f;
                        if (z + widthZ > blocks[b, 1]) widthZ = blocks[b, 1] - z;
                        if (widthZ < 9f) break;

                        float centerZ = z + widthZ * 0.5f;

                        // Leave wide open space for the Rubis Petrol Station on East side around Z = 152!
                        if (side == 1 && Mathf.Abs(centerZ - 152f) < 22f)
                        {
                            z += widthZ + 0.8f;
                            continue;
                        }

                        int stories = rng.Next(2, 5);
                        float depthX = 11.5f + (float)rng.NextDouble() * 4.5f;
                        float centerX = side * (12.4f + depthX * 0.5f);

                        bool behindStage = (side == -1) && IsNearBusStage(centerZ, 10f);
                        if (behindStage) centerX -= 2.2f;

                        Color facadeCol = Palette.BuildingFacades[rng.Next(Palette.BuildingFacades.Length)];
                        Color awningCol = Palette.ShopAwnings[rng.Next(Palette.ShopAwnings.Length)];
                        string shopTitle = ShopNames[shopIdx++ % ShopNames.Length];

                        var facadeMat = GetMat($"FacadePBR_{ColorUtility.ToHtmlStringRGB(facadeCol)}", facadeCol, 0.16f, 0f, null, texPlasterAlb, texPlasterNrm, new Vector2(3f, 2.5f), 1.1f);
                        var awningMat = GetMat($"AwningMabati_{ColorUtility.ToHtmlStringRGB(awningCol)}", awningCol, 0.35f, 0.25f, null, texMabatiAlb, texMabatiNrm, new Vector2(5f, 1f), 1.25f);

                        BuildKenyanShopBuilding(
                            bRoot,
                            new Vector3(centerX, 0.16f, centerZ),
                            yawDeg: side < 0 ? 90f : -90f,
                            widthFront: widthZ - 0.65f,
                            depth: depthX,
                            stories: stories,
                            facadeMat, awningMat, matTrim, matGlass, matShopGlass, matFrame, matRoofMabati, matSign,
                            rng.NextDouble() > 0.45 ? matTankBlk : matTankBlu, matRail,
                            shopTitle,
                            hasWaterTank: rng.NextDouble() > 0.32);

                        z += widthZ + 0.8f;
                    }
                }
            }

            // Cross-street buildings
            foreach (float intZ in IntersectionZs)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    for (int ns = -1; ns <= 1; ns += 2)
                    {
                        for (int k = 0; k < 3; k++)
                        {
                            float bx = side * (31f + k * 17.5f);
                            float bz = intZ + ns * 18.2f;
                            Color facadeCol = Palette.BuildingFacades[rng.Next(Palette.BuildingFacades.Length)];
                            Color awningCol = Palette.ShopAwnings[rng.Next(Palette.ShopAwnings.Length)];

                            var facadeMat = GetMat($"FacadePBR_{ColorUtility.ToHtmlStringRGB(facadeCol)}", facadeCol, 0.16f, 0f, null, texPlasterAlb, texPlasterNrm, new Vector2(3f, 2.5f), 1.1f);
                            var awningMat = GetMat($"AwningMabati_{ColorUtility.ToHtmlStringRGB(awningCol)}", awningCol, 0.35f, 0.25f, null, texMabatiAlb, texMabatiNrm, new Vector2(5f, 1f), 1.25f);

                            BuildKenyanShopBuilding(
                                bRoot,
                                new Vector3(bx, 0.16f, bz),
                                yawDeg: ns > 0 ? 180f : 0f,
                                widthFront: 15.2f,
                                depth: 11f,
                                stories: rng.Next(2, 4),
                                facadeMat, awningMat, matTrim, matGlass, matShopGlass, matFrame, matRoofMabati, matSign,
                                k % 2 == 0 ? matTankBlk : matTankBlu, matRail,
                                ShopNames[shopIdx++ % ShopNames.Length],
                                hasWaterTank: k != 1);
                        }
                    }
                }
            }
        }

        private static void BuildKenyanShopBuilding(
            Transform parent, Vector3 basePos, float yawDeg,
            float widthFront, float depth, int stories,
            Material facadeMat, Material awningMat,
            Material matTrim, Material matGlass, Material matShopGlass, Material matFrame,
            Material matRoofMabati, Material matSign, Material matTank, Material matRail,
            string shopName, bool hasWaterTank)
        {
            var b = new GameObject($"ShopBuilding_{shopName}").transform;
            b.SetParent(parent, false);
            b.position = basePos;
            b.rotation = Quaternion.Euler(0f, yawDeg, 0f);

            float groundH = 3.9f;
            float floorH = 3.2f;
            float totalH = groundH + (stories - 1) * floorH;
            float frontZ = depth * 0.5f;

            CreateBox("MainBlock", b, new Vector3(0f, totalH * 0.5f, 0f), new Vector3(widthFront, totalH, depth), facadeMat, withCollider: true);
            CreateBox("GroundPlinth", b, new Vector3(0f, 0.35f, frontZ + 0.04f), new Vector3(widthFront + 0.08f, 0.7f, 0.16f), matTrim, false);
            CreateBox("ShopGlass", b, new Vector3(0f, 1.80f, frontZ + 0.05f), new Vector3(widthFront * 0.84f, 2.25f, 0.12f), matShopGlass, false);
            CreateBox("ShopDoorFrame", b, new Vector3(0f, 1.38f, frontZ + 0.09f), new Vector3(1.85f, 2.55f, 0.12f), matFrame, false);

            // Corrugated mabati cantilevered shop awning
            var awning = CreateBox("ShopAwningMabati", b, new Vector3(0f, 3.08f, frontZ + 1.02f), new Vector3(widthFront * 0.94f, 0.16f, 2.08f), awningMat, false);
            awning.transform.localRotation = Quaternion.Euler(14f, 0f, 0f);

            CreateBox("ShopSignBoard", b, new Vector3(0f, 3.74f, frontZ + 0.16f), new Vector3(widthFront * 0.88f, 0.80f, 0.18f), matSign, false);
            CreateWorldSignText("ShopSignText", b, shopName, new Vector3(0f, 3.74f, frontZ + 0.27f), new Vector2(widthFront * 0.84f, 0.68f), 3.4f, Palette.UiAccentAmber);

            for (int f = 1; f < stories; f++)
            {
                float fy = groundH + (f - 1) * floorH;
                CreateBox($"Cornice_{f}", b, new Vector3(0f, fy + 0.12f, frontZ + 0.28f), new Vector3(widthFront + 0.35f, 0.24f, 0.65f), matTrim, false);
                // Kenyan metal balcony railing bar on 2nd floor
                if (f == 1)
                {
                    CreateBox($"BalconyRail_{f}", b, new Vector3(0f, fy + 0.92f, frontZ + 0.56f), new Vector3(widthFront * 0.92f, 0.07f, 0.07f), matRail, false);
                }

                int winCount = Mathf.Max(2, Mathf.FloorToInt(widthFront / 3.2f));
                float step = (widthFront - 2.0f) / winCount;
                for (int w = 0; w < winCount; w++)
                {
                    float wx = -(widthFront - 2.0f) * 0.5f + step * (w + 0.5f);
                    CreateBox($"WinFrame_{f}_{w}", b, new Vector3(wx, fy + 1.65f, frontZ + 0.05f), new Vector3(1.68f, 1.58f, 0.14f), matFrame, false);
                    CreateBox($"WinGlass_{f}_{w}", b, new Vector3(wx, fy + 1.65f, frontZ + 0.09f), new Vector3(1.42f, 1.32f, 0.10f), matGlass, false);
                }
            }

            // Corrugated iron mabati sloped roof cap + parapet
            CreateBox("RoofParapet", b, new Vector3(0f, totalH + 0.36f, 0f), new Vector3(widthFront + 0.26f, 0.72f, depth + 0.26f), matRoofMabati, false);

            if (hasWaterTank)
            {
                float tx = (widthFront * 0.22f) * (yawDeg > 0 ? 1f : -1f);
                CreateBox("TankStand", b, new Vector3(tx, totalH + 0.95f, -depth * 0.15f), new Vector3(1.8f, 0.7f, 1.8f), matTrim, false);
                CreateCylinder("WaterTank", b, new Vector3(tx, totalH + 2.25f, -depth * 0.15f), new Vector3(1.85f, 1.05f, 1.85f), matTank);
            }
        }

        private static void BuildStreetLampsAndAcaciaTrees(Transform parent, List<Light> pointLights, List<Renderer> glowRenderers)
        {
            var root = new GameObject("StreetFurnitureAndAcaciaTrees").transform;
            root.SetParent(parent, false);

            var matPole = GetMat("LampPostMetal", Palette.LampPostMetal, 0.55f, 0.75f);
            var matLampGlow = GetMat("LampGlow", Palette.LampGlow, 0.6f, 0f, Palette.LampGlow * 0.5f);
            var matCurb = GetMat("CurbStone", Palette.CurbStone, 0.22f, 0f, null, texPlasterAlb, texPlasterNrm, new Vector2(1f, 1f), 0.9f);
            var matSoil = GetMat("PlanterMurram", Color.white, 0.08f, 0f, null, texMurramAlb, texMurramNrm, Vector2.one, 1.2f);
            var matBark = GetMat("AcaciaBarkPBR", Color.white, 0.10f, 0f, null, texBarkAlb, texBarkNrm, new Vector2(1f, 3f), 1.35f);
            var matLeaf = GetMat("AcaciaLeafAlpha", Color.white, 0.12f, 0f, null, texLeafCluster, null, Vector2.one, 1f, alphaClipDoubleSided: true);

            var rng = new System.Random(25499);
            int lampIdx = 0;

            for (float z = -60f; z <= 530f; z += 26f)
            {
                if (IsNearIntersection(z, 14f)) continue;

                for (int side = -1; side <= 1; side += 2)
                {
                    if (side == -1 && IsNearBusStage(z, 10f)) continue;
                    if (side == 1 && Mathf.Abs(z - 152f) < 20f) continue;

                    // Curved urban street lamp with Night PointLight on every other lamp
                    var lamp = new GameObject($"StreetLamp_{z:0}_{side}").transform;
                    lamp.SetParent(root, false);
                    lamp.position = new Vector3(side * 7.75f, 0.16f, z);
                    lamp.rotation = Quaternion.Euler(0f, side < 0 ? 0f : 180f, 0f);

                    CreateCylinder("Post", lamp, new Vector3(0f, 3.8f, 0f), new Vector3(0.18f, 3.8f, 0.18f), matPole);
                    CreateBox("Arm", lamp, new Vector3(1.1f, 7.45f, 0f), new Vector3(2.4f, 0.14f, 0.14f), matPole, false);
                    var head = CreateBox("Head", lamp, new Vector3(2.15f, 7.32f, 0f), new Vector3(0.68f, 0.16f, 0.36f), matLampGlow, false);
                    glowRenderers.Add(head.GetComponent<Renderer>());

                    if (lampIdx++ % 3 == 0)
                    {
                        var plGo = new GameObject("LampLight");
                        plGo.transform.SetParent(lamp, false);
                        plGo.transform.localPosition = new Vector3(2.15f, 6.85f, 0f);
                        var pl = plGo.AddComponent<Light>();
                        pl.type = LightType.Point;
                        pl.color = Palette.LampGlow;
                        pl.range = 24f;
                        pl.intensity = 0f;
                        pl.enabled = false;
                        pointLights.Add(pl);
                    }

                    // Realistic branching alpha-clipped Kenyan Acacia / Shade Tree in stone planter!
                    float treeZ = z + 13f;
                    if (!IsNearIntersection(treeZ, 15f) && !(side == -1 && IsNearBusStage(treeZ, 11f)) && !(side == 1 && Mathf.Abs(treeZ - 152f) < 22f) && treeZ <= 535f)
                    {
                        var tree = new GameObject($"KenyanAcaciaTree_{treeZ:0}_{side}").transform;
                        tree.SetParent(root, false);
                        tree.position = new Vector3(side * 9.8f, 0.16f, treeZ);

                        CreateBox("Planter", tree, new Vector3(0f, 0.22f, 0f), new Vector3(1.65f, 0.44f, 1.65f), matCurb, withCollider: true);
                        CreateBox("Soil", tree, new Vector3(0f, 0.45f, 0f), new Vector3(1.42f, 0.05f, 1.42f), matSoil, false);

                        float yaw = (float)(rng.NextDouble() * 360.0);
                        float scl = 0.92f + (float)rng.NextDouble() * 0.28f;
                        CreateCustomMesh("BranchingTrunk", tree, new Vector3(0f, 0.42f, 0f), Quaternion.Euler(0f, yaw, 0f), Vector3.one * scl, KenyaSculptedMeshFactory.TreeTrunkMesh, matBark, false);
                        CreateCustomMesh("LeafyCrown", tree, new Vector3(0f, 0.42f, 0f), Quaternion.Euler(0f, yaw, 0f), Vector3.one * scl, KenyaSculptedMeshFactory.TreeFoliageMesh, matLeaf, false);
                    }
                }
            }

            // Scatter additional Kenyan Acacia trees on the red-murram savanna slopes beyond the buildings for RK 254 KE skyline depth!
            for (int i = 0; i < 42; i++)
            {
                float side = (i % 2 == 0) ? -1f : 1f;
                float tx = side * (38f + (float)rng.NextDouble() * 55f);
                float tz = -65f + (float)rng.NextDouble() * 610f;
                if (IsNearIntersection(tz, 24f)) continue;
                float scl = 1.25f + (float)rng.NextDouble() * 0.85f;
                float yaw = (float)(rng.NextDouble() * 360.0);

                var wild = new GameObject($"SavannaAcacia_{i}").transform;
                wild.SetParent(root, false);
                wild.position = new Vector3(tx, 0.0f, tz);
                CreateCustomMesh("Trunk", wild, Vector3.zero, Quaternion.Euler(0f, yaw, 0f), Vector3.one * scl, KenyaSculptedMeshFactory.TreeTrunkMesh, matBark, false);
                CreateCustomMesh("Crown", wild, Vector3.zero, Quaternion.Euler(0f, yaw, 0f), Vector3.one * scl, KenyaSculptedMeshFactory.TreeFoliageMesh, matLeaf, false);
            }
        }

        private static void BuildReflectionProbes(Transform parent)
        {
            var root = new GameObject("ReflectionProbes").transform;
            root.SetParent(parent, false);

            float[] probeZs = { 20f, 160f, 320f, 470f };
            for (int i = 0; i < probeZs.Length; i++)
            {
                var go = new GameObject($"StreetReflectionProbe_{i}");
                go.transform.SetParent(root, false);
                go.transform.position = new Vector3(0f, 4.5f, probeZs[i]);
                var probe = go.AddComponent<ReflectionProbe>();
                probe.mode = ReflectionProbeMode.Realtime;
                probe.refreshMode = ReflectionProbeRefreshMode.OnAwake;
                probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.IndividualFaces;
                probe.size = new Vector3(42f, 24f, 170f);
                probe.resolution = 128;
                probe.intensity = 1.05f;
            }
        }

        private static BusStageZone[] BuildBusStages(Transform parent)
        {
            var root = new GameObject("BusStages").transform;
            root.SetParent(parent, false);

            var matFrame = GetMat("BusShelterFrame", Palette.BusShelterFrame, 0.45f, 0.5f);
            var matRoof = GetMat("BusShelterRoofMabati", Palette.BusShelterRoof, 0.42f, 0.25f, null, texMabatiAlb, texMabatiNrm, new Vector2(4f, 2f), 1.2f);
            var matBench = GetMat("BenchWood", Palette.BenchWood, 0.25f, 0f, null, texBarkAlb, texBarkNrm, Vector2.one, 0.8f);
            var matZone = GetMat("StageZonePad", Palette.StageZoneAccent, 0.3f, 0f, Palette.StageZoneAccent * 0.7f);
            var matBeacon = GetMat("StageBeacon", Palette.StageZoneAccent, 0.5f, 0.2f, Palette.StageZoneAccent * 1.6f);
            var matParticle = GetParticleMat("StageConfetti", Palette.UiAccentAmber);

            var defs = new (string name, float z, int kes, int pax)[]
            {
                ("CBD Stage", 45f, 1200, 8),
                ("Moi Avenue Stage", 175f, 1500, 10),
                ("Westlands Stage", 325f, 1800, 12),
                ("Kisumu Express Terminal", 475f, 2500, 16)
            };

            var stages = new BusStageZone[defs.Length];
            for (int i = 0; i < defs.Length; i++)
            {
                var d = defs[i];
                var go = new GameObject($"BusStage_{i + 1}_{d.name}");
                go.transform.SetParent(root, false);
                go.transform.position = new Vector3(-4.2f, 0f, d.z);

                var zone = go.AddComponent<BusStageZone>();
                zone.stageIndex = i;
                zone.stageName = d.name;
                zone.rewardKes = d.kes;
                zone.passengerCount = d.pax;

                var padGo = CreateBox("StageFloorPad", go.transform, new Vector3(0.4f, 0.015f, 0f), new Vector3(5.2f, 0.02f, 15.5f), matZone, false);
                zone.floorPadRenderer = padGo.GetComponent<Renderer>();

                var beaconGo = CreateBox("StageBeaconDiamond", go.transform, new Vector3(0.4f, 5.4f, 0f), new Vector3(1.05f, 1.05f, 1.05f), matBeacon, false);
                beaconGo.transform.localRotation = Quaternion.Euler(45f, 45f, 45f);
                zone.beaconVisual = beaconGo.transform;

                var shelter = new GameObject("BusShelter").transform;
                shelter.SetParent(go.transform, false);
                shelter.localPosition = new Vector3(-5.4f, 0.16f, 0f);

                CreateBox("BackWall", shelter, new Vector3(-1.1f, 1.45f, 0f), new Vector3(0.14f, 2.7f, 7.6f), matFrame, true);
                CreateBox("PostFL", shelter, new Vector3(0.95f, 1.45f, -3.5f), new Vector3(0.16f, 2.9f, 0.16f), matFrame, true);
                CreateBox("PostFR", shelter, new Vector3(0.95f, 1.45f, 3.5f), new Vector3(0.16f, 2.9f, 0.16f), matFrame, true);
                var roof = CreateBox("ShelterRoof", shelter, new Vector3(0f, 3.0f, 0f), new Vector3(2.85f, 0.20f, 8.4f), matRoof, false);
                roof.transform.localRotation = Quaternion.Euler(0f, 0f, -6f);
                CreateBox("BenchSeat", shelter, new Vector3(-0.45f, 0.55f, 0f), new Vector3(0.65f, 0.14f, 5.4f), matBench, true);
                CreateWorldSignText("StageSignText", shelter, $"ENA COACH STAGE\n{d.name.ToUpperInvariant()}", new Vector3(1.08f, 2.72f, 0f), new Vector2(6.5f, 0.85f), 2.5f, Palette.EnaBodyWhite, Quaternion.Euler(0f, -90f, 0f));

                var psGo = new GameObject("ConfettiBurst");
                psGo.transform.SetParent(go.transform, false);
                psGo.transform.localPosition = new Vector3(-2.5f, 2.2f, 0f);
                var ps = psGo.AddComponent<ParticleSystem>();
                var main = ps.main;
                main.playOnAwake = false;
                main.loop = false;
                main.duration = 0.6f;
                main.startLifetime = 1.1f;
                main.startSpeed = new ParticleSystem.MinMaxCurve(4f, 8.5f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.14f, 0.28f);
                main.gravityModifier = 1.4f;
                main.startColor = new ParticleSystem.MinMaxGradient(Palette.UiAccentAmber, Palette.UiAccentGreen);
                var em = ps.emission;
                em.rateOverTime = 0f;
                em.SetBursts(new[] { new ParticleSystem.Burst(0f, 36) });
                var shape = ps.shape;
                shape.shapeType = ParticleSystemShapeType.Cone;
                shape.angle = 32f;
                ps.GetComponent<ParticleSystemRenderer>().sharedMaterial = matParticle;
                zone.confettiParticles = ps;

                var commuters = new KenyaPedestrianAI[3];
                for (int c = 0; c < 3; c++)
                {
                    Vector3 cPos = new Vector3(-8.4f + (c % 2) * 0.6f, 0.16f, d.z - 2.2f + c * 2.1f);
                    var ped = CreateArticulatedPedestrian(go.transform, $"StageCommuter_{i}_{c}", cPos, 90f, i * 3 + c);
                    ped.mode = KenyaPedestrianAI.BehaviorMode.BusStageWaiter;
                    ped.stageIndex = i;
                    commuters[c] = ped;
                }
                zone.waitingCommuters = commuters;
                stages[i] = zone;
            }

            return stages;
        }

        private static void BuildPassingTraffic(Transform parent)
        {
            var root = new GameObject("PassingTrafficLHT").transform;
            root.SetParent(parent, false);

            var matTire = GetMat("TireRubberPBR", Color.white, 0.22f, 0f, null, texTireAlb, texTireNrm, new Vector2(1f, 3f), 1.2f);
            var matRim = GetMat("WheelRimAlloy", Palette.WheelRimAlloy, 0.85f, 0.85f);
            var matGlass = GetMat("EnaGlassDark", Palette.EnaGlassDark, 0.94f, 0.30f);
            var matHeadlight = GetMat("HeadlightWarm", Palette.HeadlightWarm, 0.85f, 0f, Palette.HeadlightWarm * 2.6f);
            var matTail = GetMat("TaillightRed", Palette.TaillightRed, 0.65f, 0f, Palette.TaillightRed * 1.6f);

            float[] northZs = { 28f, 118f, 205f, 295f, 372f, 455f };
            for (int i = 0; i < northZs.Length; i++)
            {
                var veh = CreateTrafficVehicle(root, $"TrafficNorth_{i}", i % 3, new Vector3(-3.4f, 0f, northZs[i]), i, matTire, matRim, matGlass, matHeadlight, matTail);
                veh.axis = KenyaTrafficVehicle.RouteAxis.BoulevardNorthSouth;
                veh.directionSign = 1;
                veh.laneCoordinate = -3.4f;
                veh.cruiseSpeed = 9.2f + (i % 3) * 1.3f;
                veh.minLoopCoord = -78f;
                veh.maxLoopCoord = 538f;
            }

            float[] southZs = { 20f, 92f, 168f, 260f, 355f, 468f };
            for (int i = 0; i < southZs.Length; i++)
            {
                var veh = CreateTrafficVehicle(root, $"TrafficSouth_{i}", (i + 1) % 3, new Vector3(3.4f, 0f, southZs[i]), i + 10, matTire, matRim, matGlass, matHeadlight, matTail);
                veh.axis = KenyaTrafficVehicle.RouteAxis.BoulevardNorthSouth;
                veh.directionSign = -1;
                veh.laneCoordinate = 3.4f;
                veh.cruiseSpeed = 11.4f + (i % 3) * 1.2f;
                veh.minLoopCoord = -78f;
                veh.maxLoopCoord = 538f;
            }

            for (int i = 0; i < IntersectionZs.Length; i++)
            {
                float intZ = IntersectionZs[i];
                var eastVeh = CreateTrafficVehicle(root, $"TrafficCrossE_{i}", i % 2, new Vector3(-42f, 0f, intZ + 3.4f), i + 20, matTire, matRim, matGlass, matHeadlight, matTail);
                eastVeh.axis = KenyaTrafficVehicle.RouteAxis.CrossStreetEastWest;
                eastVeh.directionSign = 1;
                eastVeh.cruiseSpeed = 10.5f;
                eastVeh.minLoopCoord = -76f;
                eastVeh.maxLoopCoord = 76f;

                var westVeh = CreateTrafficVehicle(root, $"TrafficCrossW_{i}", (i + 1) % 3, new Vector3(44f, 0f, intZ - 3.4f), i + 30, matTire, matRim, matGlass, matHeadlight, matTail);
                westVeh.axis = KenyaTrafficVehicle.RouteAxis.CrossStreetEastWest;
                westVeh.directionSign = -1;
                westVeh.cruiseSpeed = 10.8f;
                westVeh.minLoopCoord = -76f;
                westVeh.maxLoopCoord = 76f;
            }
        }

        private static KenyaTrafficVehicle CreateTrafficVehicle(
            Transform parent, string name, int kind, Vector3 pos, int colorSeed,
            Material matTire, Material matRim, Material matGlass, Material matHeadlight, Material matTail)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;

            var ai = go.AddComponent<KenyaTrafficVehicle>();
            var col = go.AddComponent<BoxCollider>();

            Transform[] wheels;
            if (kind == 0)
            {
                // Sculpted Kenyan 14-Seater Hiace Matatu (glossy white lofted shell + Kenya yellow PSV stripe + teal lower trim)
                var matBody = GetMat("MatatuGlossWhite", Palette.MatatuWhite, 0.78f, 0.18f);
                var matStripe = GetMat("MatatuStripeYellow", Palette.MatatuStripeYellow, 0.55f, 0.1f);
                var matAccent = GetMat("MatatuAccentTeal", Palette.MatatuAccentTeal, 0.65f, 0.15f);

                CreateCustomMesh("SculptedMatatuShell", go.transform, Vector3.zero, Quaternion.identity, Vector3.one, KenyaSculptedMeshFactory.MatatuBodyMesh, matBody, false);
                CreateBox("PsvYellowStripe", go.transform, new Vector3(0f, 1.06f, -0.15f), new Vector3(1.99f, 0.22f, 4.45f), matStripe, false);
                CreateBox("LowerTrim", go.transform, new Vector3(0f, 0.68f, -0.15f), new Vector3(1.98f, 0.48f, 4.42f), matAccent, false);
                CreateBox("SideWindows", go.transform, new Vector3(0f, 1.66f, -0.22f), new Vector3(1.98f, 0.58f, 3.85f), matGlass, false);
                var ws = CreateBox("FrontWindshield", go.transform, new Vector3(0f, 1.64f, 2.25f), new Vector3(1.74f, 0.68f, 0.10f), matGlass, false);
                ws.transform.localRotation = Quaternion.Euler(-22f, 0f, 0f);
                CreateBox("HeadL", go.transform, new Vector3(-0.66f, 0.80f, 2.52f), new Vector3(0.32f, 0.18f, 0.08f), matHeadlight, false);
                CreateBox("HeadR", go.transform, new Vector3(0.66f, 0.80f, 2.52f), new Vector3(0.32f, 0.18f, 0.08f), matHeadlight, false);
                CreateBox("TailL", go.transform, new Vector3(-0.72f, 0.90f, -2.54f), new Vector3(0.24f, 0.42f, 0.08f), matTail, false);
                CreateBox("TailR", go.transform, new Vector3(0.72f, 0.90f, -2.54f), new Vector3(0.24f, 0.42f, 0.08f), matTail, false);

                wheels = CreateTrafficWheelSet(go.transform, 0.96f, 1.52f, -1.48f, 0.36f, matTire, matRim);
                col.center = new Vector3(0f, 1.15f, 0f);
                col.size = new Vector3(1.98f, 2.0f, 5.1f);
                ai.vehicleHalfLength = 2.68f;
                ai.wheelRadius = 0.36f;
            }
            else if (kind == 1)
            {
                // Sculpted Kenyan Sedan / Toyota Fielder / Probox with sloped hood & glossy metallic car paint
                Color[] carColors = { Palette.CarSilver, Palette.CarMaroon, Palette.CarBlue, Palette.CarGreen };
                Color c = carColors[colorSeed % carColors.Length];
                var matCar = GetMat($"CarMetal_{ColorUtility.ToHtmlStringRGB(c)}", c, 0.82f, 0.38f);

                CreateCustomMesh("SculptedSedanShell", go.transform, Vector3.zero, Quaternion.identity, Vector3.one, KenyaSculptedMeshFactory.SedanBodyMesh, matCar, false);
                CreateBox("CabinGlass", go.transform, new Vector3(0f, 1.24f, -0.35f), new Vector3(1.76f, 0.44f, 1.95f), matGlass, false);
                CreateBox("HeadL", go.transform, new Vector3(-0.62f, 0.66f, 2.22f), new Vector3(0.32f, 0.16f, 0.08f), matHeadlight, false);
                CreateBox("HeadR", go.transform, new Vector3(0.62f, 0.66f, 2.22f), new Vector3(0.32f, 0.16f, 0.08f), matHeadlight, false);
                CreateBox("TailL", go.transform, new Vector3(-0.62f, 0.74f, -2.22f), new Vector3(0.30f, 0.18f, 0.08f), matTail, false);
                CreateBox("TailR", go.transform, new Vector3(0.62f, 0.74f, -2.22f), new Vector3(0.30f, 0.18f, 0.08f), matTail, false);

                wheels = CreateTrafficWheelSet(go.transform, 0.90f, 1.32f, -1.32f, 0.34f, matTire, matRim);
                col.center = new Vector3(0f, 0.88f, 0f);
                col.size = new Vector3(1.86f, 1.48f, 4.45f);
                ai.vehicleHalfLength = 2.32f;
                ai.wheelRadius = 0.34f;
            }
            else
            {
                // Delivery Freight Lorry with corrugated cargo body
                var matCab = GetMat("TruckCabGloss", Palette.TruckCabWhite, 0.75f, 0.2f);
                var matBox = GetMat("TruckCargoMabati", Palette.TruckCargoBlue, 0.42f, 0.32f, null, texMabatiAlb, texMabatiNrm, new Vector2(4f, 2f), 1.15f);

                CreateBox("Cab", go.transform, new Vector3(0f, 1.45f, 2.15f), new Vector3(2.12f, 1.85f, 1.95f), matCab, false);
                CreateBox("CabGlass", go.transform, new Vector3(0f, 1.78f, 3.12f), new Vector3(1.92f, 0.78f, 0.10f), matGlass, false);
                CreateBox("CargoBox", go.transform, new Vector3(0f, 1.88f, -0.95f), new Vector3(2.24f, 2.35f, 4.15f), matBox, false);
                CreateBox("HeadL", go.transform, new Vector3(-0.78f, 0.85f, 3.14f), new Vector3(0.32f, 0.20f, 0.08f), matHeadlight, false);
                CreateBox("HeadR", go.transform, new Vector3(0.78f, 0.85f, 3.14f), new Vector3(0.32f, 0.20f, 0.08f), matHeadlight, false);
                CreateBox("TailL", go.transform, new Vector3(-0.85f, 0.95f, -3.04f), new Vector3(0.32f, 0.20f, 0.08f), matTail, false);
                CreateBox("TailR", go.transform, new Vector3(0.85f, 0.95f, -3.04f), new Vector3(0.32f, 0.20f, 0.08f), matTail, false);

                wheels = CreateTrafficWheelSet(go.transform, 1.05f, 2.15f, -1.55f, 0.44f, matTire, matRim);
                col.center = new Vector3(0f, 1.55f, 0.05f);
                col.size = new Vector3(2.26f, 2.85f, 6.25f);
                ai.vehicleHalfLength = 3.25f;
                ai.wheelRadius = 0.44f;
            }

            ai.wheelVisuals = wheels;
            return ai;
        }

        private static Transform[] CreateTrafficWheelSet(Transform parent, float halfTrack, float frontZ, float rearZ, float radius, Material matTire, Material matRim)
        {
            Vector3[] offsets =
            {
                new Vector3(-halfTrack, radius, frontZ),
                new Vector3( halfTrack, radius, frontZ),
                new Vector3(-halfTrack, radius, rearZ),
                new Vector3( halfTrack, radius, rearZ)
            };
            var list = new Transform[4];
            for (int i = 0; i < 4; i++)
            {
                var pivot = new GameObject($"Wheel_{i}").transform;
                pivot.SetParent(parent, false);
                pivot.localPosition = offsets[i];

                var tire = CreateCylinder("Tire", pivot, Vector3.zero, new Vector3(radius * 2f, 0.14f, radius * 2f), matTire);
                tire.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                var rim = CreateCylinder("Rim", pivot, Vector3.zero, new Vector3(radius * 1.15f, 0.155f, radius * 1.15f), matRim);
                rim.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);

                list[i] = pivot;
            }
            return list;
        }

        private static void BuildWalkingAndCrossingPedestrians(Transform parent, StreetTrafficLight[] lights)
        {
            var root = new GameObject("KenyanPedestrians").transform;
            root.SetParent(parent, false);

            int seed = 100;
            float[] patrolRanges = { -45f, 15f, 125f, 185f, 275f, 335f, 425f, 485f, 520f };
            for (int i = 0; i < patrolRanges.Length; i++)
            {
                float zStart = patrolRanges[i];
                for (int side = -1; side <= 1; side += 2)
                {
                    float xWalk = side * (8.6f + (i % 2) * 1.4f);
                    float zA = zStart;
                    float zB = zStart + 36f;
                    var ped = CreateArticulatedPedestrian(root, $"SidewalkWalker_{i}_{side}", new Vector3(xWalk, 0.16f, zA), 0f, seed++);
                    ped.mode = KenyaPedestrianAI.BehaviorMode.SidewalkPatrol;
                    ped.pointA = new Vector3(xWalk, 0.16f, zA);
                    ped.pointB = new Vector3(xWalk, 0.16f, zB);
                    ped.walkSpeed = 1.25f + (seed % 4) * 0.16f;
                }
            }

            for (int i = 0; i < IntersectionZs.Length; i++)
            {
                float intZ = IntersectionZs[i];
                var light = i < lights.Length ? lights[i] : null;

                var pNorth = CreateArticulatedPedestrian(root, $"ZebraCrosserN_{i}", new Vector3(-8.2f, 0.16f, intZ + 8.4f), 90f, seed++);
                pNorth.mode = KenyaPedestrianAI.BehaviorMode.ZebraCrossing;
                pNorth.pointA = new Vector3(-8.2f, 0.16f, intZ + 8.4f);
                pNorth.pointB = new Vector3(8.2f, 0.16f, intZ + 8.4f);
                pNorth.walkSpeed = 1.45f;
                pNorth.crossingLight = light;

                var pSouth = CreateArticulatedPedestrian(root, $"ZebraCrosserS_{i}", new Vector3(8.2f, 0.16f, intZ - 8.4f), -90f, seed++);
                pSouth.mode = KenyaPedestrianAI.BehaviorMode.ZebraCrossing;
                pSouth.pointA = new Vector3(8.2f, 0.16f, intZ - 8.4f);
                pSouth.pointB = new Vector3(-8.2f, 0.16f, intZ - 8.4f);
                pSouth.walkSpeed = 1.55f;
                pSouth.crossingLight = light;
            }
        }

        private static KenyaPedestrianAI CreateArticulatedPedestrian(Transform parent, string name, Vector3 pos, float yawDeg, int seed)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0f, yawDeg, 0f);

            var col = go.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0f, 0.88f, 0f);
            col.radius = 0.28f;
            col.height = 1.72f;

            var ai = go.AddComponent<KenyaPedestrianAI>();

            Color skin = Palette.SkinTones[seed % Palette.SkinTones.Length];
            Color shirt = Palette.ShirtColors[(seed * 3) % Palette.ShirtColors.Length];
            Color trouser = Palette.TrouserColors[(seed * 5) % Palette.TrouserColors.Length];

            var matSkin = GetMat($"Skin_{ColorUtility.ToHtmlStringRGB(skin)}", skin, 0.25f);
            var matShirt = GetMat($"Shirt_{ColorUtility.ToHtmlStringRGB(shirt)}", shirt, 0.2f);
            var matTrouser = GetMat($"Trouser_{ColorUtility.ToHtmlStringRGB(trouser)}", trouser, 0.18f);
            var matHair = GetMat("PedHair", Palette.TireRubber, 0.15f);

            var bodyPivot = new GameObject("BodyPivot").transform;
            bodyPivot.SetParent(go.transform, false);
            bodyPivot.localPosition = new Vector3(0f, 0.92f, 0f);
            ai.bodyPivot = bodyPivot;

            CreateBox("Torso", bodyPivot, new Vector3(0f, 0.24f, 0f), new Vector3(0.42f, 0.56f, 0.24f), matShirt, false);
            CreateSphere("Head", bodyPivot, new Vector3(0f, 0.68f, 0f), new Vector3(0.26f, 0.28f, 0.26f), matSkin);
            CreateSphere("Hair", bodyPivot, new Vector3(0f, 0.74f, -0.02f), new Vector3(0.27f, 0.18f, 0.27f), matHair);

            ai.leftArm = CreateLimbPivot(bodyPivot, "LeftArm", new Vector3(-0.27f, 0.46f, 0f), new Vector3(0.13f, 0.52f, 0.13f), matShirt, matSkin);
            ai.rightArm = CreateLimbPivot(bodyPivot, "RightArm", new Vector3(0.27f, 0.46f, 0f), new Vector3(0.13f, 0.52f, 0.13f), matShirt, matSkin);
            ai.leftLeg = CreateLimbPivot(bodyPivot, "LeftLeg", new Vector3(-0.11f, -0.06f, 0f), new Vector3(0.15f, 0.80f, 0.16f), matTrouser, matHair);
            ai.rightLeg = CreateLimbPivot(bodyPivot, "RightLeg", new Vector3(0.11f, -0.06f, 0f), new Vector3(0.15f, 0.80f, 0.16f), matTrouser, matHair);

            return ai;
        }

        private static Transform CreateLimbPivot(Transform parent, string name, Vector3 pivotLocal, Vector3 size, Material upperMat, Material tipMat)
        {
            var pivot = new GameObject(name).transform;
            pivot.SetParent(parent, false);
            pivot.localPosition = pivotLocal;

            CreateBox("Segment", pivot, new Vector3(0f, -size.y * 0.46f, 0f), size, upperMat, false);
            CreateBox("Tip", pivot, new Vector3(0f, -size.y * 0.96f, 0.02f), new Vector3(size.x * 0.95f, 0.10f, size.z * 1.25f), tipMat, false);
            return pivot;
        }

        private static KenyaStreetVehicle BuildEnaCoach(Transform parent, Vector3 spawnPos)
        {
            var root = new GameObject("ENA_Coach_KDK057M");
            root.transform.SetParent(parent, false);
            root.transform.position = spawnPos;

            var rb = root.AddComponent<Rigidbody>();
            rb.mass = 9200f;
#if UNITY_6000_0_OR_NEWER
            rb.linearDamping = 0.04f;
            rb.angularDamping = 0.35f;
#else
            rb.drag = 0.04f;
            rb.angularDrag = 0.35f;
#endif

            var ctrl = root.AddComponent<KenyaStreetVehicle>();
            ctrl.displayName = "ENA Coach KDK 057M";
            ctrl.plateNumber = "KDK 057M";
            ctrl.isCoach = true;
            ctrl.motorTorque = 2850f;
            ctrl.brakeTorque = 5800f;
            ctrl.maxSpeedKmh = 84f;
            ctrl.centerOfMassOffset = new Vector3(0f, -0.58f, 0.15f);

            var chassisCol = root.AddComponent<BoxCollider>();
            chassisCol.center = new Vector3(0f, 1.98f, 0f);
            chassisCol.size = new Vector3(2.48f, 2.68f, 11.55f);

            // Crisp automotive glossy URP materials reflecting the sunlit Kenyan procedural skybox!
            var matWhite = GetMat("EnaBodyPearlGloss", Palette.EnaBodyWhite, 0.72f, 0.08f);
            var matRed = GetMat("EnaSkirtRedGloss", Palette.EnaSkirtRed, 0.72f, 0.10f);
            var matBlack = GetMat("EnaStripeBlackGloss", Palette.EnaStripeBlack, 0.75f, 0.12f);
            var matOrange = GetMat("EnaLogoOrange", Palette.EnaLogoOrange, 0.65f, 0.15f, Palette.EnaLogoOrange * 0.6f);
            var matChrome = GetMat("EnaChrome", Palette.EnaChrome, 0.96f, 0.94f);
            var matGlass = GetMat("EnaGlassDark", Palette.EnaGlassDark, 0.96f, 0.30f);
            var matHead = GetMat("HeadlightWarm", Palette.HeadlightWarm, 0.85f, 0f, Palette.HeadlightWarm * 1.4f);
            var matTail = GetMat("TaillightRed", Palette.TaillightRed, 0.7f, 0f, Palette.TaillightRed * 1.2f);
            var matIndicator = GetMat("IndicatorAmber", Palette.UiAccentAmber, 0.7f, 0f, Color.black);
            var matPlate = GetMat("PlateYellow", Palette.PlateYellow, 0.45f);
            var matInterior = GetMat("CabInterior", Palette.CabInterior, 0.28f);
            var matDashGlow = GetMat("DashGaugeGlow", Palette.UiAccentGreen, 0.5f, 0f, Palette.UiAccentGreen * 1.8f);
            var matTire = GetMat("TireRubberPBR", Color.white, 0.22f, 0f, null, texTireAlb, texTireNrm, new Vector2(1f, 3f), 1.3f);
            var matRim = GetMat("WheelRimAlcoa", Palette.WheelRimAlloy, 0.92f, 0.92f);

            var body = new GameObject("VisualBodyRoot").transform;
            body.SetParent(root.transform, false);

            // Sculpted Aerodynamic MAN/Scania Master Apollo Lofted Coach Shell (100% outward solid normals + open panoramic RHD cockpit bay!)
            CreateCustomMesh("SculptedUpperBody", body, Vector3.zero, Quaternion.identity, Vector3.one, KenyaSculptedMeshFactory.EnaCoachUpperMesh, matWhite, false);
            CreateCustomMesh("SculptedLowerSkirt", body, Vector3.zero, Quaternion.identity, Vector3.one, KenyaSculptedMeshFactory.EnaCoachSkirtMesh, matRed, false);
            CreateCustomMesh("SculptedRoofBrow", body, Vector3.zero, Quaternion.identity, Vector3.one, KenyaSculptedMeshFactory.EnaCoachWindshieldMesh, matWhite, false);

            // Real translucent tinted panoramic windshield (gleams outside, crystal clear looking out from RHD Cockpit!)
            var matClearWindshield = GetTransparentGlassMat("EnaPanoramicWindshield", Palette.WithAlpha(Palette.EnaGlassDark, 0.24f));
            var frontGlass = CreateBox("PanoramicWindshieldGlass", body, new Vector3(0f, 2.26f, 5.76f), new Vector3(2.26f, 1.28f, 0.04f), matClearWindshield, false);
            frontGlass.transform.localRotation = Quaternion.Euler(-11f, 0f, 0f);
            // Side cockpit windows (transparent tinted so you can see both sides cleanly from RHD driver seat!)
            CreateBox("CockpitGlassL", body, new Vector3(-1.25f, 2.26f, 5.18f), new Vector3(0.04f, 1.24f, 1.08f), matClearWindshield, false);
            CreateBox("CockpitGlassR", body, new Vector3( 1.25f, 2.26f, 5.18f), new Vector3(0.04f, 1.24f, 1.08f), matClearWindshield, false);

            // Crisp gloss-black beltline pinstripe + Rooftop A/C Pod
            CreateBox("AccentStripeBlack", body, new Vector3(0f, 1.57f, -0.04f), new Vector3(2.58f, 0.13f, 11.45f), matBlack, false);
            CreateBox("RoofACPod", body, new Vector3(0f, 3.56f, 0.35f), new Vector3(1.68f, 0.28f, 3.65f), matWhite, false);
            CreateBox("RoofACGrille", body, new Vector3(0f, 3.58f, 0.35f), new Vector3(1.42f, 0.30f, 2.85f), matBlack, false);

            // Gloss-black front brow + orange ENA oval + crisp single-line ENA COACH header
            var brow = CreateBox("FrontBrowBlack", body, new Vector3(0f, 3.04f, 5.56f), new Vector3(2.38f, 0.48f, 0.18f), matBlack, false);
            brow.transform.localRotation = Quaternion.Euler(-16f, 0f, 0f);
            CreateSphere("EnaOvalLogo", body, new Vector3(-0.82f, 3.06f, 5.69f), new Vector3(0.40f, 0.25f, 0.08f), matOrange);
            CreateWorldSignText("FrontEnaHeader", body, "ENA COACH", new Vector3(0.22f, 3.06f, 5.70f), new Vector2(1.85f, 0.40f), 2.0f, Palette.EnaBodyWhite, Quaternion.Euler(-16f, 180f, 0f), noWrap: true);

            // Tinted panoramic side windows with sculpted mullions
            CreateBox("SideWindowsBand", body, new Vector3(0f, 2.42f, -0.35f), new Vector3(2.57f, 1.08f, 9.6f), matGlass, false);
            for (float pz = -4.2f; pz <= 3.9f; pz += 1.62f)
            {
                CreateBox($"WindowPillar_{pz:0.0}", body, new Vector3(0f, 2.42f, pz), new Vector3(2.58f, 1.10f, 0.14f), matBlack, false);
            }

            // Side livery lettering
            CreateWorldSignText("SideSignL", body, "ENA COACH  •  KDK 057M", new Vector3(-1.30f, 1.15f, 0.1f), new Vector2(8.0f, 0.65f), 4.2f, Palette.EnaBodyWhite, Quaternion.Euler(0f, 90f, 0f));
            CreateWorldSignText("SideSignR", body, "ENA COACH  •  KDK 057M", new Vector3(1.30f, 1.15f, 0.1f), new Vector2(8.0f, 0.65f), 4.2f, Palette.EnaBodyWhite, Quaternion.Euler(0f, -90f, 0f));

            // Chrome V-grille, headlights + SpotLights, blinking amber indicators, KDK 057M plate
            CreateBox("FrontGrille", body, new Vector3(0f, 1.14f, 5.93f), new Vector3(1.48f, 0.54f, 0.08f), matBlack, false);
            CreateBox("ChromeVBar1", body, new Vector3(0f, 1.28f, 5.98f), new Vector3(1.42f, 0.08f, 0.06f), matChrome, false);
            CreateBox("ChromeVBar2", body, new Vector3(0f, 1.08f, 5.98f), new Vector3(1.18f, 0.07f, 0.06f), matChrome, false);
            CreateBox("FrontPlate", body, new Vector3(0f, 0.72f, 5.96f), new Vector3(0.68f, 0.19f, 0.05f), matPlate, false);
            CreateWorldSignText("FrontPlateTxt", body, "KDK 057M", new Vector3(0f, 0.72f, 6.00f), new Vector2(0.66f, 0.18f), 1.3f, Palette.EnaStripeBlack, Quaternion.Euler(0f, 180f, 0f), noWrap: true);

            var headL = CreateBox("HeadlightL", body, new Vector3(-0.92f, 0.96f, 5.92f), new Vector3(0.38f, 0.20f, 0.08f), matHead, false);
            var headR = CreateBox("HeadlightR", body, new Vector3( 0.92f, 0.96f, 5.92f), new Vector3(0.38f, 0.20f, 0.08f), matHead, false);
            var indFL = CreateBox("IndFrontL", body, new Vector3(-1.14f, 0.96f, 5.86f), new Vector3(0.16f, 0.20f, 0.10f), matIndicator, false);
            var indFR = CreateBox("IndFrontR", body, new Vector3( 1.14f, 0.96f, 5.86f), new Vector3(0.16f, 0.20f, 0.10f), matIndicator, false);

            // Real URP SpotLight beams for Night / Sunset highway driving!
            var spotL = CreateHeadlightSpot(root.transform, "HeadlightBeamL", new Vector3(-0.92f, 0.96f, 6.05f));
            var spotR = CreateHeadlightSpot(root.transform, "HeadlightBeamR", new Vector3( 0.92f, 0.96f, 6.05f));
            ctrl.headlightSpots = new[] { spotL, spotR };
            ctrl.headlightRenderers = new[] { headL.GetComponent<Renderer>(), headR.GetComponent<Renderer>() };

            // Curved white elephant-ear forward mirrors
            for (int side = -1; side <= 1; side += 2)
            {
                CreateBox($"MirrorArm_{side}", body, new Vector3(side * 1.36f, 2.54f, 5.92f), new Vector3(0.34f, 0.07f, 0.46f), matWhite, false);
                CreateBox($"MirrorHead_{side}", body, new Vector3(side * 1.52f, 2.26f, 6.12f), new Vector3(0.14f, 0.48f, 0.22f), matWhite, false);
                CreateBox($"MirrorGlass_{side}", body, new Vector3(side * 1.52f, 2.26f, 6.00f), new Vector3(0.12f, 0.42f, 0.03f), matChrome, false);
            }

            // Detailed Rear Fascia + Brake Lights + Rear Indicators (crisp white upper + crimson lower skirt clearly visible!)
            CreateBox("RearGlass", body, new Vector3(0f, 2.50f, -5.85f), new Vector3(1.88f, 0.64f, 0.05f), matGlass, false);
            CreateBox("RearEngineVent", body, new Vector3(0f, 1.32f, -5.86f), new Vector3(1.24f, 0.22f, 0.04f), matBlack, false);
            CreateBox("RearChromeTrim", body, new Vector3(0f, 1.57f, -5.87f), new Vector3(2.38f, 0.06f, 0.05f), matChrome, false);
            CreateBox("RearPlate", body, new Vector3(0f, 0.86f, -5.87f), new Vector3(0.72f, 0.20f, 0.05f), matPlate, false);
            CreateWorldSignText("RearPlateTxt", body, "KDK 057M", new Vector3(0f, 0.86f, -5.91f), new Vector2(0.70f, 0.18f), 1.3f, Palette.EnaStripeBlack, Quaternion.identity, noWrap: true);
            CreateWorldSignText("RearSignTxt", body, "ENA COACH", new Vector3(0f, 3.06f, -5.86f), new Vector2(2.2f, 0.44f), 2.45f, Palette.EnaSkirtRed, Quaternion.identity, noWrap: true);

            var tailL = CreateBox("BrakeLightL", body, new Vector3(-1.02f, 1.15f, -5.86f), new Vector3(0.28f, 0.52f, 0.06f), matTail, false);
            var tailR = CreateBox("BrakeLightR", body, new Vector3( 1.02f, 1.15f, -5.86f), new Vector3(0.28f, 0.52f, 0.06f), matTail, false);
            var indRL = CreateBox("IndRearL", body, new Vector3(-1.02f, 0.82f, -5.86f), new Vector3(0.28f, 0.16f, 0.06f), matIndicator, false);
            var indRR = CreateBox("IndRearR", body, new Vector3( 1.02f, 0.82f, -5.86f), new Vector3(0.28f, 0.16f, 0.06f), matIndicator, false);
            ctrl.brakeLightRenderers = new[] { tailL.GetComponent<Renderer>(), tailR.GetComponent<Renderer>() };
            ctrl.leftIndicatorRenderers = new[] { indFL.GetComponent<Renderer>(), indRL.GetComponent<Renderer>() };
            ctrl.rightIndicatorRenderers = new[] { indFR.GetComponent<Renderer>(), indRR.GetComponent<Renderer>() };

            // Realistic ETS2-Style RHD Driver Cockpit Interior (dashboard, glowing cluster, A-pillars, sun visor, turning steering wheel!)
            var cockpit = new GameObject("CockpitInterior").transform;
            cockpit.SetParent(body, false);
            CreateBox("RhdDashboard", cockpit, new Vector3(0f, 1.56f, 5.25f), new Vector3(2.22f, 0.46f, 0.72f), matInterior, false);
            CreateBox("DashInstrumentHood", cockpit, new Vector3(0.68f, 1.84f, 5.22f), new Vector3(0.62f, 0.22f, 0.36f), matBlack, false);
            CreateBox("DashGlowingCluster", cockpit, new Vector3(0.68f, 1.82f, 5.03f), new Vector3(0.50f, 0.14f, 0.03f), matDashGlow, false);
            CreateBox("APillarL", cockpit, new Vector3(-1.14f, 2.38f, 5.45f), new Vector3(0.11f, 1.45f, 0.14f), matBlack, false);
            CreateBox("APillarR", cockpit, new Vector3( 1.14f, 2.38f, 5.45f), new Vector3(0.11f, 1.45f, 0.14f), matBlack, false);
            CreateBox("SunVisor", cockpit, new Vector3(0f, 2.94f, 5.38f), new Vector3(2.24f, 0.22f, 0.30f), matBlack, false);

            var steerPivot = new GameObject("SteeringWheelPivot").transform;
            steerPivot.SetParent(cockpit, false);
            steerPivot.localPosition = new Vector3(0.68f, 1.88f, 4.86f);
            steerPivot.localRotation = Quaternion.Euler(24f, 0f, 0f);
            CreateCylinder("SteerRim", steerPivot, Vector3.zero, new Vector3(0.48f, 0.045f, 0.48f), matBlack);
            CreateBox("SteerSpokeH", steerPivot, Vector3.zero, new Vector3(0.44f, 0.055f, 0.06f), matChrome, false);
            CreateBox("SteerSpokeV", steerPivot, new Vector3(0f, 0f, -0.10f), new Vector3(0.06f, 0.055f, 0.22f), matChrome, false);
            ctrl.steeringWheelVisual = steerPivot;

            // Camera Anchors (Cockpit RHD, Highway Dashcam, Axle View)
            var cockpitCam = new GameObject("CockpitCameraAnchor").transform;
            cockpitCam.SetParent(root.transform, false);
            cockpitCam.localPosition = new Vector3(0.68f, 2.38f, 4.78f);
            cockpitCam.localRotation = Quaternion.Euler(4f, 0f, 0f);
            ctrl.cockpitCameraAnchor = cockpitCam;

            var bumperCam = new GameObject("DashcamAnchor").transform;
            bumperCam.SetParent(root.transform, false);
            bumperCam.localPosition = new Vector3(0f, 1.98f, 6.08f);
            bumperCam.localRotation = Quaternion.Euler(2f, 0f, 0f);
            ctrl.bumperCameraAnchor = bumperCam;

            var axleCam = new GameObject("AxleCameraAnchor").transform;
            axleCam.SetParent(root.transform, false);
            axleCam.localPosition = new Vector3(-2.1f, 0.95f, 1.8f);
            axleCam.localRotation = Quaternion.Euler(4f, 14f, 0f);
            ctrl.axleCameraAnchor = axleCam;

            float[] axleZs = { 4.25f, 2.75f, -2.55f, -4.05f };
            bool[] steerFlags = { true, true, false, false };
            bool[] driveFlags = { false, false, true, true };
            ctrl.wheels = BuildWheelColliderSet(root.transform, axleZs, steerFlags, driveFlags, halfTrack: 1.18f, radius: 0.50f, suspension: 0.24f, matTire, matRim);

            return ctrl;
        }

        private static KenyaStreetVehicle BuildIsuzuFrr(Transform parent, Vector3 spawnPos)
        {
            var root = new GameObject("EPH_Isuzu_FRR90");
            root.transform.SetParent(parent, false);
            root.transform.position = spawnPos;

            var rb = root.AddComponent<Rigidbody>();
            rb.mass = 7600f;
#if UNITY_6000_0_OR_NEWER
            rb.linearDamping = 0.04f;
            rb.angularDamping = 0.35f;
#else
            rb.drag = 0.04f;
            rb.angularDrag = 0.35f;
#endif

            var ctrl = root.AddComponent<KenyaStreetVehicle>();
            ctrl.displayName = "EPH Isuzu FRR 90 (6W)";
            ctrl.plateNumber = "KDL 409G";
            ctrl.isCoach = false;
            ctrl.motorTorque = 2700f;
            ctrl.brakeTorque = 5400f;
            ctrl.maxSpeedKmh = 80f;
            ctrl.centerOfMassOffset = new Vector3(0f, -0.55f, 0.1f);

            var chassisCol = root.AddComponent<BoxCollider>();
            chassisCol.center = new Vector3(0f, 1.85f, 0f);
            chassisCol.size = new Vector3(2.42f, 2.55f, 8.65f);

            var matNavy = GetMat("IsuzuCabNavyGloss", Palette.IsuzuCabNavy, 0.80f, 0.32f);
            var matCargo = GetMat("IsuzuCargoMabati", Palette.IsuzuCargoSilver, 0.48f, 0.38f, null, texMabatiAlb, texMabatiNrm, new Vector2(5f, 2f), 1.2f);
            var matGold = GetMat("IsuzuStripeGold", Palette.IsuzuStripeGold, 0.65f, 0.35f);
            var matBullbar = GetMat("BullbarSteel", Palette.BullbarSteel, 0.6f, 0.8f);
            var matWhite = GetMat("EnaBodyPearlGloss", Palette.EnaBodyWhite, 0.84f, 0.22f);
            var matChrome = GetMat("EnaChrome", Palette.EnaChrome, 0.96f, 0.94f);
            var matGlass = GetMat("EnaGlassDark", Palette.EnaGlassDark, 0.96f, 0.30f);
            var matHead = GetMat("HeadlightWarm", Palette.HeadlightWarm, 0.85f, 0f, Palette.HeadlightWarm * 1.4f);
            var matTail = GetMat("TaillightRed", Palette.TaillightRed, 0.7f, 0f, Palette.TaillightRed * 1.2f);
            var matIndicator = GetMat("IndicatorAmber", Palette.UiAccentAmber, 0.7f, 0f, Color.black);
            var matPlate = GetMat("PlateYellow", Palette.PlateYellow, 0.45f);
            var matInterior = GetMat("CabInterior", Palette.CabInterior, 0.28f);
            var matDashGlow = GetMat("DashGaugeGlow", Palette.UiAccentGreen, 0.5f, 0f, Palette.UiAccentGreen * 1.8f);
            var matTire = GetMat("TireRubberPBR", Color.white, 0.22f, 0f, null, texTireAlb, texTireNrm, new Vector2(1f, 3f), 1.3f);
            var matRim = GetMat("WheelRimAlcoa", Palette.WheelRimAlloy, 0.92f, 0.92f);

            var body = new GameObject("VisualBodyRoot").transform;
            body.SetParent(root.transform, false);

            CreateBox("ChassisFrame", body, new Vector3(0f, 0.82f, -0.2f), new Vector3(1.2f, 0.28f, 8.1f), matBullbar, false);

            // Sculpted Forward Cab + aerodynamic roof fairing
            CreateCustomMesh("SculptedForwardCab", body, Vector3.zero, Quaternion.identity, Vector3.one, KenyaSculptedMeshFactory.IsuzuCabMesh, matNavy, false);
            CreateBox("RoofFairing", body, new Vector3(0f, 3.06f, 2.75f), new Vector3(2.10f, 0.46f, 1.55f), matWhite, false);
            var ws = CreateBox("CabWindshield", body, new Vector3(0f, 2.22f, 4.10f), new Vector3(1.98f, 0.88f, 0.10f), matGlass, false);
            ws.transform.localRotation = Quaternion.Euler(-14f, 0f, 0f);
            CreateBox("CabSideGlass", body, new Vector3(0f, 2.15f, 3.05f), new Vector3(2.30f, 0.80f, 1.38f), matGlass, false);
            CreateBox("CabGrille", body, new Vector3(0f, 1.42f, 4.24f), new Vector3(1.62f, 0.48f, 0.08f), matChrome, false);

            // Welded Kikapu steel bull-bar + vertical air snorkel
            CreateBox("BullbarBase", body, new Vector3(0f, 0.92f, 4.44f), new Vector3(2.34f, 0.28f, 0.24f), matBullbar, false);
            CreateBox("BullbarTop", body, new Vector3(0f, 1.56f, 4.46f), new Vector3(2.12f, 0.10f, 0.10f), matBullbar, false);
            CreateBox("BullbarPostL", body, new Vector3(-0.72f, 1.24f, 4.46f), new Vector3(0.10f, 0.68f, 0.10f), matBullbar, false);
            CreateBox("BullbarPostR", body, new Vector3( 0.72f, 1.24f, 4.46f), new Vector3(0.10f, 0.68f, 0.10f), matBullbar, false);
            CreateCylinder("AirSnorkel", body, new Vector3(-1.20f, 2.25f, 2.05f), new Vector3(0.16f, 1.15f, 0.16f), matBullbar);

            // Corrugated EPH Logistics Cargo Box
            CreateBox("CargoBoxMain", body, new Vector3(0f, 2.18f, -1.25f), new Vector3(2.42f, 2.48f, 5.95f), matCargo, false);
            CreateBox("CargoGoldStripe", body, new Vector3(0f, 1.35f, -1.25f), new Vector3(2.46f, 0.28f, 5.98f), matGold, false);
            CreateWorldSignText("CargoSignL", body, "EPH LOGISTICS  •  ISUZU FRR 90", new Vector3(-1.24f, 2.28f, -1.25f), new Vector2(5.4f, 0.72f), 3.4f, Palette.IsuzuCabNavy, Quaternion.Euler(0f, 90f, 0f));
            CreateWorldSignText("CargoSignR", body, "EPH LOGISTICS  •  ISUZU FRR 90", new Vector3(1.24f, 2.28f, -1.25f), new Vector2(5.4f, 0.72f), 3.4f, Palette.IsuzuCabNavy, Quaternion.Euler(0f, -90f, 0f));

            CreateBox("FrontPlate", body, new Vector3(0f, 0.74f, 4.58f), new Vector3(0.66f, 0.18f, 0.05f), matPlate, false);
            var headL = CreateBox("HeadL", body, new Vector3(-0.88f, 1.06f, 4.26f), new Vector3(0.36f, 0.22f, 0.08f), matHead, false);
            var headR = CreateBox("HeadR", body, new Vector3( 0.88f, 1.06f, 4.26f), new Vector3(0.36f, 0.22f, 0.08f), matHead, false);
            var indFL = CreateBox("IndFrontL", body, new Vector3(-1.08f, 1.06f, 4.22f), new Vector3(0.14f, 0.20f, 0.10f), matIndicator, false);
            var indFR = CreateBox("IndFrontR", body, new Vector3( 1.08f, 1.06f, 4.22f), new Vector3(0.14f, 0.20f, 0.10f), matIndicator, false);

            var spotL = CreateHeadlightSpot(root.transform, "HeadlightBeamL", new Vector3(-0.88f, 1.06f, 4.50f));
            var spotR = CreateHeadlightSpot(root.transform, "HeadlightBeamR", new Vector3( 0.88f, 1.06f, 4.50f));
            ctrl.headlightSpots = new[] { spotL, spotR };
            ctrl.headlightRenderers = new[] { headL.GetComponent<Renderer>(), headR.GetComponent<Renderer>() };

            var tailL = CreateBox("TailL", body, new Vector3(-0.96f, 0.96f, -4.25f), new Vector3(0.38f, 0.20f, 0.08f), matTail, false);
            var tailR = CreateBox("TailR", body, new Vector3( 0.96f, 0.96f, -4.25f), new Vector3(0.38f, 0.20f, 0.08f), matTail, false);
            var indRL = CreateBox("IndRearL", body, new Vector3(-0.96f, 0.72f, -4.25f), new Vector3(0.28f, 0.14f, 0.08f), matIndicator, false);
            var indRR = CreateBox("IndRearR", body, new Vector3( 0.96f, 0.72f, -4.25f), new Vector3(0.28f, 0.14f, 0.08f), matIndicator, false);
            ctrl.brakeLightRenderers = new[] { tailL.GetComponent<Renderer>(), tailR.GetComponent<Renderer>() };
            ctrl.leftIndicatorRenderers = new[] { indFL.GetComponent<Renderer>(), indRL.GetComponent<Renderer>() };
            ctrl.rightIndicatorRenderers = new[] { indFR.GetComponent<Renderer>(), indRR.GetComponent<Renderer>() };

            CreateBox("RhdDash", body, new Vector3(0f, 1.65f, 3.62f), new Vector3(2.0f, 0.40f, 0.55f), matInterior, false);
            CreateBox("DashCluster", body, new Vector3(0.58f, 1.88f, 3.45f), new Vector3(0.46f, 0.14f, 0.04f), matDashGlow, false);
            var steerPivot = new GameObject("SteeringWheelPivot").transform;
            steerPivot.SetParent(body, false);
            steerPivot.localPosition = new Vector3(0.58f, 1.92f, 3.35f);
            steerPivot.localRotation = Quaternion.Euler(24f, 0f, 0f);
            CreateCylinder("SteerHub", steerPivot, Vector3.zero, new Vector3(0.44f, 0.04f, 0.44f), matBullbar);
            ctrl.steeringWheelVisual = steerPivot;

            var cockpitCam = new GameObject("CockpitCameraAnchor").transform;
            cockpitCam.SetParent(root.transform, false);
            cockpitCam.localPosition = new Vector3(0.58f, 2.38f, 2.95f);
            ctrl.cockpitCameraAnchor = cockpitCam;

            var bumperCam = new GameObject("DashcamAnchor").transform;
            bumperCam.SetParent(root.transform, false);
            bumperCam.localPosition = new Vector3(0f, 1.92f, 4.48f);
            ctrl.bumperCameraAnchor = bumperCam;

            var axleCam = new GameObject("AxleCameraAnchor").transform;
            axleCam.SetParent(root.transform, false);
            axleCam.localPosition = new Vector3(-2.0f, 0.95f, 1.2f);
            axleCam.localRotation = Quaternion.Euler(4f, 14f, 0f);
            ctrl.axleCameraAnchor = axleCam;

            float[] axleZs = { 2.85f, -1.85f, -2.95f };
            bool[] steerFlags = { true, false, false };
            bool[] driveFlags = { false, true, true };
            ctrl.wheels = BuildWheelColliderSet(root.transform, axleZs, steerFlags, driveFlags, halfTrack: 1.12f, radius: 0.48f, suspension: 0.24f, matTire, matRim);

            return ctrl;
        }

        private static Material GetTransparentGlassMat(string key, Color color)
        {
            string path = $"{MatFolder}/M_{key}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(urpLitShader);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetFloat("_Surface", 1f); // Transparent
            mat.SetFloat("_Blend", 0f);   // Alpha
            mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)RenderQueue.Transparent;
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", 0.96f);
            mat.SetFloat("_Metallic", 0.15f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Light CreateHeadlightSpot(Transform parent, string name, Vector3 localPos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(9f, 0f, 0f);
            var spot = go.AddComponent<Light>();
            spot.type = LightType.Spot;
            spot.color = Palette.HeadlightWarm;
            spot.range = 120f;
            spot.spotAngle = 72f;
            spot.innerSpotAngle = 36f;
            spot.intensity = 480f;
            spot.shadows = LightShadows.None;
            spot.enabled = false;
            return spot;
        }

        private static KenyaStreetVehicle.WheelEntry[] BuildWheelColliderSet(
            Transform vehicleRoot, float[] axleZs, bool[] steerFlags, bool[] driveFlags,
            float halfTrack, float radius, float suspension, Material matTire, Material matRim)
        {
            var colParent = new GameObject("WheelColliders").transform;
            colParent.SetParent(vehicleRoot, false);
            var visParent = new GameObject("WheelVisuals").transform;
            visParent.SetParent(vehicleRoot, false);

            var entries = new KenyaStreetVehicle.WheelEntry[axleZs.Length * 2];
            int idx = 0;

            for (int a = 0; a < axleZs.Length; a++)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    bool isLeft = side < 0;
                    string tag = $"A{a}_{(isLeft ? "L" : "R")}";

                    var wcGo = new GameObject($"WC_{tag}");
                    wcGo.transform.SetParent(colParent, false);
                    wcGo.transform.localPosition = new Vector3(side * halfTrack, radius + suspension * 0.4f, axleZs[a]);

                    var wc = wcGo.AddComponent<WheelCollider>();
                    wc.radius = radius;
                    wc.suspensionDistance = suspension;
                    wc.mass = 65f;
                    wc.wheelDampingRate = 0.5f;

                    var spring = wc.suspensionSpring;
                    spring.spring = 68000f;
                    spring.damper = 7200f;
                    spring.targetPosition = 0.45f;
                    wc.suspensionSpring = spring;

                    var fwd = wc.forwardFriction;
                    fwd.stiffness = 1.65f;
                    wc.forwardFriction = fwd;

                    var sideFric = wc.sidewaysFriction;
                    sideFric.stiffness = 1.85f;
                    wc.sidewaysFriction = sideFric;

                    var vis = new GameObject($"WV_{tag}").transform;
                    vis.SetParent(visParent, false);
                    vis.localPosition = wcGo.transform.localPosition;

                    var tire = CreateCylinder("Tire", vis, Vector3.zero, new Vector3(radius * 2f, 0.19f, radius * 2f), matTire);
                    tire.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                    var rim = CreateCylinder("Rim", vis, Vector3.zero, new Vector3(radius * 1.18f, 0.205f, radius * 1.18f), matRim);
                    rim.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                    var hub = CreateCylinder("HubCap", vis, Vector3.zero, new Vector3(radius * 0.46f, 0.225f, radius * 0.46f), matTire);
                    hub.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);

                    entries[idx++] = new KenyaStreetVehicle.WheelEntry
                    {
                        collider = wc,
                        visualMesh = vis,
                        isSteer = steerFlags[a],
                        isDrive = driveFlags[a],
                        isLeft = isLeft
                    };
                }
            }

            return entries;
        }

        private static bool IsNearIntersection(float z, float margin)
        {
            foreach (float iz in IntersectionZs)
            {
                if (Mathf.Abs(z - iz) < margin) return true;
            }
            return false;
        }

        private static bool IsNearBusStage(float z, float margin)
        {
            float[] stageZs = { 45f, 175f, 325f, 475f };
            foreach (float sz in stageZs)
            {
                if (Mathf.Abs(z - sz) < margin) return true;
            }
            return false;
        }

        private static GameObject CreateCustomMesh(string name, Transform parent, Vector3 localPos, Quaternion localRot, Vector3 localScale, Mesh mesh, Material mat, bool withMeshCollider)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot;
            go.transform.localScale = localScale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            if (withMeshCollider)
            {
                go.AddComponent<MeshCollider>().sharedMesh = mesh;
            }
            return go;
        }

        private static GameObject CreateBox(string name, Transform parent, Vector3 localPos, Vector3 scale, Material mat, bool withCollider)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            if (!withCollider)
            {
                Object.DestroyImmediate(go.GetComponent<Collider>());
            }
            return go;
        }

        private static Renderer CreateSphere(string name, Transform parent, Vector3 localPos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            return r;
        }

        private static GameObject CreateCylinder(string name, Transform parent, Vector3 localPos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        private static void CreateWorldSignText(string name, Transform parent, string text, Vector3 localPos, Vector2 rectSize, float fontSize, Color color, Quaternion? localRot = null, bool noWrap = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot ?? Quaternion.Euler(0f, 180f, 0f);

            var tmp = go.AddComponent<TextMeshPro>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = color;
            tmp.textWrappingMode = noWrap ? TextWrappingModes.NoWrap : TextWrappingModes.Normal;
            tmp.rectTransform.sizeDelta = rectSize;
        }

        private static void SetBuildSettingsScene(string path)
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(path, true)
            };
        }

        private static int CountDefaultMaterials()
        {
            int count = 0;
            foreach (var r in Object.FindObjectsByType<Renderer>())
            {
                if (r is ParticleSystemRenderer) continue;
                var m = r.sharedMaterial;
                if (m == null || m.name.Contains("Default-Material") || m.name.Contains("Lit (Instance)"))
                {
                    count++;
                }
            }
            return count;
        }
    }
}
