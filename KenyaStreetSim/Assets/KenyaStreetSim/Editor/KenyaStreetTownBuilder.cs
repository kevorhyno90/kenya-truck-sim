using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace KenyaStreetSim.Editor
{
    /// <summary>
    /// Builds the complete sunlit Kenyan town street scene (KenyaTownStreets.unity):
    /// two-lane Left-Hand Traffic tarmac boulevard (ZERO speed bumps!), 3 cross-street intersections,
    /// working 3-color traffic lights, zebra crossings, raised sidewalks & curbs, 50+ Kenyan shops & buildings
    /// with awnings & 3D signs & rooftop water tanks, 4 shaded ENA Coach bus stages, 18 passing AI vehicles,
    /// 36 articulated walking/crossing/waiting pedestrians, and switchable ENA Coach KDK 057M & EPH Isuzu FRR 90.
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
            "TAUSI BOUTIQUE", "SIMBA ELECTRONICS", "ELDORET SPARES", "KENYA PHARMACY",
            "UHURU BAKERY", "RIFT VALLEY OPTICS", "KILIMANI BOOKS", "SAVANNAH GRILL"
        };

        private static Dictionary<string, Material> matCache;
        private static Shader urpLitShader;
        private static Shader urpParticleShader;

        [MenuItem("Kenya Street Sim/Build Kenyan Town Streets Scene")]
        public static string BuildKenyaTownScene()
        {
            EnsureFolders();
            matCache = new Dictionary<string, Material>();
            urpLitShader = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLitShader == null)
            {
                throw new System.Exception("Universal Render Pipeline/Lit shader not found!");
            }
            urpParticleShader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? urpLitShader;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "KenyaTownStreets";

            ConfigureLightingAndPostProcessing();
            var camObj = CreateMainCamera();

            var worldRoot = new GameObject("KenyaTownWorld");
            BuildRoadNetworkAndSidewalks(worldRoot.transform);
            var trafficLights = BuildIntersectionsAndTrafficLights(worldRoot.transform);
            BuildTownBuildingsAndShops(worldRoot.transform);
            BuildStreetLampsAndTrees(worldRoot.transform);

            var stages = BuildBusStages(worldRoot.transform);
            BuildPassingTraffic(worldRoot.transform);
            BuildWalkingAndCrossingPedestrians(worldRoot.transform, trafficLights);

            // Build Player Fleet (ENA Coach KDK 057M & EPH Isuzu FRR 90)
            var fleetRoot = new GameObject("PlayerFleet");
            var enaCoach = BuildEnaCoach(fleetRoot.transform, new Vector3(-3.5f, 0.28f, -18f));
            var isuzuTruck = BuildIsuzuFrr(fleetRoot.transform, new Vector3(-3.5f, 0.28f, -18f));
            isuzuTruck.gameObject.SetActive(false);

            // Audio + Game Loop + uGUI HUD
            var sysRoot = new GameObject("KenyaStreetSystems");
            sysRoot.AddComponent<KenyaStreetAudio>();
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
            return $"Built {ScenePath} | Renderers={meshCount} | DefaultMaterials={defaultMatCount} | Stages={stages.Length} | TrafficLights={trafficLights.Length} | SpeedBumps=0";
        }

        private static void EnsureFolders()
        {
            string[] dirs =
            {
                "Assets/KenyaStreetSim",
                "Assets/KenyaStreetSim/Scenes",
                MatFolder,
                SettingsFolder
            };
            foreach (var d in dirs)
            {
                if (!Directory.Exists(d))
                {
                    Directory.CreateDirectory(d);
                }
            }
            AssetDatabase.Refresh();
        }

        private static Material GetMat(string key, Color color, float smoothness = 0.35f, float metallic = 0f, Color? emission = null)
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

        private static void ConfigureLightingAndPostProcessing()
        {
            // Directional mid-afternoon Kenyan sun
            var sunGo = new GameObject("Directional Light");
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = Palette.SunKey;
            sun.intensity = 1.25f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.78f;
            sunGo.transform.rotation = Quaternion.Euler(42f, -35f, 0f);

            // Skybox
            var skyMat = AssetDatabase.LoadAssetAtPath<Material>(SkyMatPath);
            if (skyMat == null)
            {
                var skyShader = Shader.Find("Skybox/Procedural");
                skyMat = new Material(skyShader);
                AssetDatabase.CreateAsset(skyMat, SkyMatPath);
            }
            skyMat.SetColor("_SkyTint", Palette.SkyTint);
            skyMat.SetColor("_GroundColor", Palette.SkyGround);
            skyMat.SetFloat("_AtmosphereThickness", 0.85f);
            skyMat.SetFloat("_SunSize", 0.035f);
            skyMat.SetFloat("_Exposure", 1.15f);
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
            RenderSettings.fogDensity = 0.0018f;

            // Global Post-Processing Volume
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
            bl.threshold.value = 1.1f;
            bl.intensity.overrideState = true;
            bl.intensity.value = 0.28f;
            bl.scatter.overrideState = true;
            bl.scatter.value = 0.65f;

            if (!profile.TryGet<Vignette>(out var vig)) vig = profile.Add<Vignette>();
            vig.intensity.overrideState = true;
            vig.intensity.value = 0.18f;
            vig.smoothness.overrideState = true;
            vig.smoothness.value = 0.35f;

            if (!profile.TryGet<ColorAdjustments>(out var ca)) ca = profile.Add<ColorAdjustments>();
            ca.saturation.overrideState = true;
            ca.saturation.value = 8f;
            ca.contrast.overrideState = true;
            ca.contrast.value = 6f;
            ca.postExposure.overrideState = true;
            ca.postExposure.value = 0.05f;
            EditorUtility.SetDirty(profile);

            var volGo = new GameObject("Global Volume");
            var vol = volGo.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.priority = 1f;
            vol.sharedProfile = profile;
        }

        private static GameObject CreateMainCamera()
        {
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            camGo.transform.position = new Vector3(-9f, 5.5f, -30f);
            camGo.transform.LookAt(new Vector3(-3.5f, 1.8f, -12f));

            var cam = camGo.AddComponent<Camera>();
            cam.allowHDR = true;
            cam.fieldOfView = 58f;
            cam.nearClipPlane = 0.15f;
            cam.farClipPlane = 700f;

            var camData = camGo.AddComponent<UniversalAdditionalCameraData>();
            camData.renderPostProcessing = true;
            camData.antialiasing = AntialiasingMode.FastApproximateAntialiasing;

            camGo.AddComponent<AudioListener>();
            camGo.AddComponent<KenyaStreetCamera>();
            return camGo;
        }

        private static void BuildRoadNetworkAndSidewalks(Transform parent)
        {
            var roadRoot = new GameObject("RoadNetwork").transform;
            roadRoot.SetParent(parent, false);

            var matGround = GetMat("TownGround", Palette.TownGround, 0.12f);
            var matGrass = GetMat("GrassVerge", Palette.GrassVerge, 0.15f);
            var matTarmac = GetMat("Tarmac", Palette.Tarmac, 0.24f);
            var matSidewalk = GetMat("Sidewalk", Palette.Sidewalk, 0.18f);
            var matCurb = GetMat("CurbStone", Palette.CurbStone, 0.22f);
            var matWhiteLine = GetMat("RoadLineWhite", Palette.RoadLineWhite, 0.15f);
            var matYellowLine = GetMat("RoadLineYellow", Palette.RoadLineYellow, 0.18f);

            // Broad town ground base (well beneath tarmac — no z-fighting)
            CreateBox("TownBaseGround", roadRoot, new Vector3(0f, -0.35f, 235f), new Vector3(360f, 0.4f, 820f), matGround, withCollider: true);

            // Main flat smooth tarmac boulevard: Z = -95 to +575 (670m long, 14m wide, ZERO speed bumps!)
            CreateBox("MainBoulevardTarmac", roadRoot, new Vector3(0f, -0.10f, 240f), new Vector3(14.0f, 0.20f, 670f), matTarmac, withCollider: true);

            // Outer shoulder strips & continuous sidewalk blocks segmented cleanly around cross-street openings
            // Road segments between intersections: [-95..88], [102..238], [252..388], [402..575]
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
                    // Raised concrete sidewalk
                    CreateBox($"Sidewalk_{s}_{side}", roadRoot, new Vector3(side * 9.85f, 0.08f, zMid), new Vector3(4.82f, 0.16f, len), matSidewalk, withCollider: true);
                    // Green verge behind buildings
                    CreateBox($"BackVerge_{s}_{side}", roadRoot, new Vector3(side * 30f, -0.05f, zMid), new Vector3(24f, 0.12f, len), matGrass, withCollider: false);
                    // White edge line on tarmac
                    CreateBox($"EdgeLine_{s}_{side}", roadRoot, new Vector3(side * 6.55f, 0.008f, zMid), new Vector3(0.16f, 0.016f, len), matWhiteLine, withCollider: false);
                }

                // Dashed yellow/white center divider lines
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
                        CreateBox($"CenterDash_{z:0}", roadRoot, new Vector3(0f, 0.008f, z), new Vector3(0.18f, 0.016f, 3.8f), matWhiteLine, withCollider: false);
                    }
                }
            }

            // Cross streets extending 80m east and west at each intersection (zero overlap with main tarmac!)
            foreach (float intZ in IntersectionZs)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    float xMid = side * 47f; // spans 7..87
                    CreateBox($"CrossRoad_{intZ}_{side}", roadRoot, new Vector3(xMid, -0.10f, intZ), new Vector3(80f, 0.20f, 14f), matTarmac, withCollider: true);

                    // Cross-street sidewalks north and south
                    CreateBox($"CrossWalkN_{intZ}_{side}", roadRoot, new Vector3(xMid, 0.08f, intZ + 9.4f), new Vector3(80f, 0.16f, 4.8f), matSidewalk, withCollider: true);
                    CreateBox($"CrossWalkS_{intZ}_{side}", roadRoot, new Vector3(xMid, 0.08f, intZ - 9.4f), new Vector3(80f, 0.16f, 4.8f), matSidewalk, withCollider: true);
                    CreateBox($"CrossCurbN_{intZ}_{side}", roadRoot, new Vector3(xMid, 0.08f, intZ + 7.22f), new Vector3(80f, 0.16f, 0.44f), matCurb, withCollider: true);
                    CreateBox($"CrossCurbS_{intZ}_{side}", roadRoot, new Vector3(xMid, 0.08f, intZ - 7.22f), new Vector3(80f, 0.16f, 0.44f), matCurb, withCollider: true);

                    // Cross-street center dashes
                    for (float x = 14f; x < 82f; x += 8f)
                    {
                        CreateBox($"CrossDash_{intZ}_{side}_{x:0}", roadRoot, new Vector3(side * x, 0.008f, intZ), new Vector3(3.8f, 0.016f, 0.18f), matWhiteLine, withCollider: false);
                    }
                }

                // Zebra Pedestrian Crossings on North & South approaches of main boulevard
                for (int end = -1; end <= 1; end += 2)
                {
                    float zebraZ = intZ + end * 8.4f;
                    for (float x = -5.4f; x <= 5.5f; x += 1.2f)
                    {
                        CreateBox($"ZebraMain_{intZ}_{end}_{x:0.0}", roadRoot, new Vector3(x, 0.010f, zebraZ), new Vector3(0.58f, 0.014f, 2.8f), matWhiteLine, withCollider: false);
                    }
                    // Stop bar before zebra
                    float stopZ = intZ + end * 10.6f;
                    float stopX = end < 0 ? -3.4f : 3.4f;
                    CreateBox($"StopBarMain_{intZ}_{end}", roadRoot, new Vector3(stopX, 0.010f, stopZ), new Vector3(6.2f, 0.014f, 0.42f), matWhiteLine, withCollider: false);
                }

                // Zebra crossings on East & West side-street approaches
                for (int side = -1; side <= 1; side += 2)
                {
                    float zebraX = side * 8.4f;
                    for (float z = -5.2f; z <= 5.3f; z += 1.2f)
                    {
                        CreateBox($"ZebraCross_{intZ}_{side}_{z:0.0}", roadRoot, new Vector3(zebraX, 0.010f, intZ + z), new Vector3(2.8f, 0.014f, 0.58f), matWhiteLine, withCollider: false);
                    }
                }
            }

            // Road end decorative barrier plazas at Z = -85 and Z = 555 so the street ends intentionally
            CreateBox("SouthTownArch", roadRoot, new Vector3(0f, 0.4f, -86f), new Vector3(16f, 0.8f, 1.2f), matCurb, withCollider: true);
            CreateBox("NorthTownArch", roadRoot, new Vector3(0f, 0.4f, 558f), new Vector3(16f, 0.8f, 1.2f), matCurb, withCollider: true);
        }

        private static StreetTrafficLight[] BuildIntersectionsAndTrafficLights(Transform parent)
        {
            var root = new GameObject("TrafficIntersections").transform;
            root.SetParent(parent, false);

            var matPole = GetMat("LampPostMetal", Palette.LampPostMetal, 0.55f, 0.7f);
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

                // Two diagonal cantilevered gantries per intersection (NW & SE)
                BuildTrafficGantry(go.transform, new Vector3(-8.2f, 0.16f, intZ + 8.2f), 180f, matPole, matHousing, matLens, mainRed, mainYel, mainGrn);
                BuildTrafficGantry(go.transform, new Vector3(8.2f, 0.16f, intZ - 8.2f), 0f, matPole, matHousing, matLens, mainRed, mainYel, mainGrn);

                // Two cross-street signal poles (NE & SW)
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

            // Vertical mast + cantilevered arm reaching over lane
            CreateBox("Pole", g, new Vector3(0f, 3.1f, 0f), new Vector3(0.24f, 6.2f, 0.24f), matPole, withCollider: true);
            CreateBox("Arm", g, new Vector3(-2.4f, 5.9f, 0f), new Vector3(5.0f, 0.20f, 0.20f), matPole, withCollider: false);

            // Overhead signal head
            BuildSignalHead(g, new Vector3(-4.2f, 5.35f, -0.18f), matHousing, matLens, redList, yelList, grnList);
            // Side post signal head for lower sightline
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

            var matTrim = GetMat("TrimDark", Palette.TrimDark, 0.25f);
            var matGlass = GetMat("GlassWindow", Palette.GlassWindow, 0.85f, 0.3f);
            var matFrame = GetMat("WindowFrame", Palette.WindowFrame, 0.35f);
            var matRoof = GetMat("RoofIron", Palette.RoofIron, 0.28f, 0.35f);
            var matSign = GetMat("ShopSignBoard", Palette.ShopSignBoard, 0.3f);
            var matTankBlk = GetMat("WaterTankBlack", Palette.WaterTankBlack, 0.3f);
            var matTankBlu = GetMat("WaterTankBlue", Palette.WaterTankBlue, 0.35f);

            var rng = new System.Random(202607);
            int shopIdx = 0;

            // Main boulevard building blocks on both West (x = -19.2) and East (x = +19.2)
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

                        int stories = rng.Next(2, 5); // 2 to 4 stories
                        float depthX = 11.5f + (float)rng.NextDouble() * 4.5f;
                        float centerZ = z + widthZ * 0.5f;
                        float centerX = side * (12.4f + depthX * 0.5f);

                        // Keep a clear setback right behind bus stages on West side
                        bool behindStage = (side == -1) &&
                            (Mathf.Abs(centerZ - 45f) < 10f || Mathf.Abs(centerZ - 175f) < 10f ||
                             Mathf.Abs(centerZ - 325f) < 10f || Mathf.Abs(centerZ - 475f) < 10f);
                        if (behindStage) centerX -= 2.2f;

                        Color facadeCol = Palette.BuildingFacades[rng.Next(Palette.BuildingFacades.Length)];
                        Color awningCol = Palette.ShopAwnings[rng.Next(Palette.ShopAwnings.Length)];
                        string shopTitle = ShopNames[shopIdx++ % ShopNames.Length];

                        BuildKenyanShopBuilding(
                            bRoot,
                            new Vector3(centerX, 0.16f, centerZ),
                            yawDeg: side < 0 ? 90f : -90f,
                            widthFront: widthZ - 0.65f,
                            depth: depthX,
                            stories: stories,
                            facadeMat: GetMat($"Facade_{ColorUtility.ToHtmlStringRGB(facadeCol)}", facadeCol, 0.18f),
                            awningMat: GetMat($"Awning_{ColorUtility.ToHtmlStringRGB(awningCol)}", awningCol, 0.25f),
                            matTrim, matGlass, matFrame, matRoof, matSign,
                            rng.NextDouble() > 0.45 ? matTankBlk : matTankBlu,
                            shopTitle,
                            hasWaterTank: rng.NextDouble() > 0.35);

                        z += widthZ + 0.8f;
                    }
                }
            }

            // Cross-street buildings so side avenues look like a true continuous Kenyan town
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

                            BuildKenyanShopBuilding(
                                bRoot,
                                new Vector3(bx, 0.16f, bz),
                                yawDeg: ns > 0 ? 180f : 0f,
                                widthFront: 15.2f,
                                depth: 11f,
                                stories: rng.Next(2, 4),
                                facadeMat: GetMat($"Facade_{ColorUtility.ToHtmlStringRGB(facadeCol)}", facadeCol, 0.18f),
                                awningMat: GetMat($"Awning_{ColorUtility.ToHtmlStringRGB(awningCol)}", awningCol, 0.25f),
                                matTrim, matGlass, matFrame, matRoof, matSign,
                                k % 2 == 0 ? matTankBlk : matTankBlu,
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
            Material matTrim, Material matGlass, Material matFrame, Material matRoof, Material matSign, Material matTank,
            string shopName, bool hasWaterTank)
        {
            var b = new GameObject($"ShopBuilding_{shopName}").transform;
            b.SetParent(parent, false);
            b.position = basePos;
            b.rotation = Quaternion.Euler(0f, yawDeg, 0f);

            float groundH = 3.8f;
            float floorH = 3.2f;
            float totalH = groundH + (stories - 1) * floorH;
            float frontZ = depth * 0.5f;

            // Main building volume
            CreateBox("MainBlock", b, new Vector3(0f, totalH * 0.5f, 0f), new Vector3(widthFront, totalH, depth), facadeMat, withCollider: true);

            // Ground-floor dark plinth + recessed shopfront glass + entrance door
            CreateBox("GroundPlinth", b, new Vector3(0f, 0.35f, frontZ + 0.04f), new Vector3(widthFront + 0.08f, 0.7f, 0.16f), matTrim, withCollider: false);
            CreateBox("ShopGlass", b, new Vector3(0f, 1.75f, frontZ + 0.05f), new Vector3(widthFront * 0.82f, 2.2f, 0.12f), matGlass, withCollider: false);
            CreateBox("ShopDoorFrame", b, new Vector3(0f, 1.35f, frontZ + 0.09f), new Vector3(1.8f, 2.5f, 0.12f), matFrame, withCollider: false);

            // Slanted colorful shop awning projecting over sidewalk
            var awning = CreateBox("ShopAwning", b, new Vector3(0f, 3.05f, frontZ + 0.95f), new Vector3(widthFront * 0.92f, 0.18f, 1.95f), awningMat, withCollider: false);
            awning.transform.localRotation = Quaternion.Euler(14f, 0f, 0f);

            // Dark shop signboard fascia + crisp 3D TMP signage facing the street!
            CreateBox("ShopSignBoard", b, new Vector3(0f, 3.68f, frontZ + 0.16f), new Vector3(widthFront * 0.88f, 0.78f, 0.18f), matSign, withCollider: false);
            CreateWorldSignText("ShopSignText", b, shopName, new Vector3(0f, 3.68f, frontZ + 0.27f), new Vector2(widthFront * 0.84f, 0.68f), 3.4f, Palette.UiAccentAmber);

            // Upper stories: horizontal cornice ledge + framed windows + small balcony rails
            for (int f = 1; f < stories; f++)
            {
                float fy = groundH + (f - 1) * floorH;
                CreateBox($"Cornice_{f}", b, new Vector3(0f, fy + 0.12f, frontZ + 0.18f), new Vector3(widthFront + 0.35f, 0.24f, 0.42f), matTrim, withCollider: false);

                int winCount = Mathf.Max(2, Mathf.FloorToInt(widthFront / 3.2f));
                float step = (widthFront - 2.0f) / winCount;
                for (int w = 0; w < winCount; w++)
                {
                    float wx = -(widthFront - 2.0f) * 0.5f + step * (w + 0.5f);
                    CreateBox($"WinFrame_{f}_{w}", b, new Vector3(wx, fy + 1.65f, frontZ + 0.05f), new Vector3(1.65f, 1.55f, 0.14f), matFrame, withCollider: false);
                    CreateBox($"WinGlass_{f}_{w}", b, new Vector3(wx, fy + 1.65f, frontZ + 0.09f), new Vector3(1.38f, 1.28f, 0.10f), matGlass, withCollider: false);
                }
            }

            // Rooftop parapet wall + iron cap
            CreateBox("RoofParapet", b, new Vector3(0f, totalH + 0.35f, 0f), new Vector3(widthFront + 0.24f, 0.70f, depth + 0.24f), matRoof, withCollider: false);

            // Iconic Kenyan rooftop cylindrical water tank!
            if (hasWaterTank)
            {
                float tx = (widthFront * 0.22f) * (yawDeg > 0 ? 1f : -1f);
                CreateBox("TankStand", b, new Vector3(tx, totalH + 0.95f, -depth * 0.15f), new Vector3(1.8f, 0.7f, 1.8f), matTrim, withCollider: false);
                CreateCylinder("WaterTank", b, new Vector3(tx, totalH + 2.25f, -depth * 0.15f), new Vector3(1.85f, 1.05f, 1.85f), matTank);
            }
        }

        private static void BuildStreetLampsAndTrees(Transform parent)
        {
            var root = new GameObject("StreetFurnitureAndTrees").transform;
            root.SetParent(parent, false);

            var matPole = GetMat("LampPostMetal", Palette.LampPostMetal, 0.55f, 0.7f);
            var matLampGlow = GetMat("LampGlow", Palette.LampGlow, 0.6f, 0f, Palette.LampGlow * 1.4f);
            var matCurb = GetMat("CurbStone", Palette.CurbStone, 0.22f);
            var matSoil = GetMat("PlanterSoil", Palette.PlanterSoil, 0.1f);
            var matTrunk = GetMat("TreeTrunk", Palette.TreeTrunk, 0.15f);
            var matFoliage1 = GetMat("FoliageRich", Palette.FoliageRich, 0.04f);
            var matFoliage2 = GetMat("FoliageHighlight", Palette.FoliageHighlight, 0.05f);

            for (float z = -60f; z <= 530f; z += 26f)
            {
                if (IsNearIntersection(z, 14f)) continue;

                for (int side = -1; side <= 1; side += 2)
                {
                    // Skip lamp/tree right inside bus stage bay on West side
                    if (side == -1 && IsNearBusStage(z, 10f)) continue;

                    // Street lamp at curb edge
                    var lamp = new GameObject($"StreetLamp_{z:0}_{side}").transform;
                    lamp.SetParent(root, false);
                    lamp.position = new Vector3(side * 7.75f, 0.16f, z);
                    lamp.rotation = Quaternion.Euler(0f, side < 0 ? 0f : 180f, 0f);

                    CreateCylinder("Post", lamp, new Vector3(0f, 3.7f, 0f), new Vector3(0.18f, 3.7f, 0.18f), matPole);
                    CreateBox("Arm", lamp, new Vector3(1.1f, 7.25f, 0f), new Vector3(2.4f, 0.14f, 0.14f), matPole, withCollider: false);
                    CreateBox("Head", lamp, new Vector3(2.15f, 7.12f, 0f), new Vector3(0.65f, 0.16f, 0.34f), matLampGlow, withCollider: false);

                    // Shade tree in stone sidewalk planter offset 13m from lamp
                    float treeZ = z + 13f;
                    if (!IsNearIntersection(treeZ, 15f) && !(side == -1 && IsNearBusStage(treeZ, 11f)) && treeZ <= 535f)
                    {
                        var tree = new GameObject($"ShadeTree_{treeZ:0}_{side}").transform;
                        tree.SetParent(root, false);
                        tree.position = new Vector3(side * 9.8f, 0.16f, treeZ);

                        CreateBox("Planter", tree, new Vector3(0f, 0.22f, 0f), new Vector3(1.65f, 0.44f, 1.65f), matCurb, withCollider: true);
                        CreateBox("Soil", tree, new Vector3(0f, 0.45f, 0f), new Vector3(1.42f, 0.05f, 1.42f), matSoil, withCollider: false);
                        CreateCylinder("Trunk", tree, new Vector3(0f, 1.75f, 0f), new Vector3(0.36f, 1.4f, 0.36f), matTrunk);

                        // Layered East African acacia / shade canopy (3 organic overlapping crowns)
                        CreateSphere("CanopyBase", tree, new Vector3(0f, 3.65f, 0f), new Vector3(3.4f, 1.55f, 3.4f), matFoliage1);
                        CreateSphere("CanopyMid", tree, new Vector3(0.75f, 4.35f, 0.45f), new Vector3(2.4f, 1.35f, 2.3f), matFoliage2);
                        CreateSphere("CanopyTop", tree, new Vector3(-0.6f, 4.45f, -0.4f), new Vector3(2.2f, 1.25f, 2.2f), matFoliage1);
                    }
                }
            }
        }

        private static BusStageZone[] BuildBusStages(Transform parent)
        {
            var root = new GameObject("BusStages").transform;
            root.SetParent(parent, false);

            var matFrame = GetMat("BusShelterFrame", Palette.BusShelterFrame, 0.45f, 0.4f);
            var matRoof = GetMat("BusShelterRoof", Palette.BusShelterRoof, 0.35f);
            var matBench = GetMat("BenchWood", Palette.BenchWood, 0.25f);
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

                // Illuminated road curb pickup strip
                var padGo = CreateBox("StageFloorPad", go.transform, new Vector3(0.4f, 0.015f, 0f), new Vector3(5.2f, 0.02f, 15.5f), matZone, withCollider: false);
                zone.floorPadRenderer = padGo.GetComponent<Renderer>();

                // Floating rotating diamond beacon above the stage
                var beaconGo = CreateBox("StageBeaconDiamond", go.transform, new Vector3(0.4f, 5.4f, 0f), new Vector3(1.1f, 1.1f, 1.1f), matBeacon, withCollider: false);
                beaconGo.transform.localRotation = Quaternion.Euler(45f, 45f, 45f);
                zone.beaconVisual = beaconGo.transform;

                // Shaded ENA Coach Bus Shelter on the west sidewalk (x = -9.6 world => local x = -5.4)
                var shelter = new GameObject("BusShelter").transform;
                shelter.SetParent(go.transform, false);
                shelter.localPosition = new Vector3(-5.4f, 0.16f, 0f);

                CreateBox("BackWall", shelter, new Vector3(-1.1f, 1.45f, 0f), new Vector3(0.14f, 2.7f, 7.6f), matFrame, withCollider: true);
                CreateBox("PostFL", shelter, new Vector3(0.95f, 1.45f, -3.5f), new Vector3(0.16f, 2.9f, 0.16f), matFrame, withCollider: true);
                CreateBox("PostFR", shelter, new Vector3(0.95f, 1.45f, 3.5f), new Vector3(0.16f, 2.9f, 0.16f), matFrame, withCollider: true);
                var roof = CreateBox("ShelterRoof", shelter, new Vector3(0f, 3.0f, 0f), new Vector3(2.85f, 0.22f, 8.4f), matRoof, withCollider: false);
                roof.transform.localRotation = Quaternion.Euler(0f, 0f, -6f);
                CreateBox("BenchSeat", shelter, new Vector3(-0.45f, 0.55f, 0f), new Vector3(0.65f, 0.14f, 5.4f), matBench, withCollider: true);
                CreateWorldSignText("StageSignText", shelter, $"ENA COACH STAGE\n{d.name.ToUpperInvariant()}", new Vector3(1.08f, 2.72f, 0f), new Vector2(6.5f, 0.85f), 2.5f, Palette.EnaBodyWhite, Quaternion.Euler(0f, -90f, 0f));

                // Confetti particle system for boarding celebration
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
                var psRend = ps.GetComponent<ParticleSystemRenderer>();
                psRend.sharedMaterial = matParticle;
                zone.confettiParticles = ps;

                // Spawn 3 waiting Kenyan commuters at each stage
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

            var matTire = GetMat("TireRubber", Palette.TireRubber, 0.2f);
            var matRim = GetMat("WheelRimAlloy", Palette.WheelRimAlloy, 0.72f, 0.75f);
            var matGlass = GetMat("EnaGlassDark", Palette.EnaGlassDark, 0.9f, 0.2f);
            var matHeadlight = GetMat("HeadlightWarm", Palette.HeadlightWarm, 0.8f, 0f, Palette.HeadlightWarm * 2.2f);
            var matTail = GetMat("TaillightRed", Palette.TaillightRed, 0.6f, 0f, Palette.TaillightRed * 1.4f);

            // 6 Northbound LHT vehicles (left lane x = -3.4, moving +Z so player drives alongside & overtakes!)
            float[] northZs = { 28f, 118f, 205f, 295f, 372f, 455f };
            for (int i = 0; i < northZs.Length; i++)
            {
                int kind = i % 3; // 0=Matatu, 1=Sedan/Probox, 2=Delivery Truck
                var veh = CreateTrafficVehicle(root, $"TrafficNorth_{i}", kind, new Vector3(-3.4f, 0f, northZs[i]), i, matTire, matRim, matGlass, matHeadlight, matTail);
                veh.axis = KenyaTrafficVehicle.RouteAxis.BoulevardNorthSouth;
                veh.directionSign = 1;
                veh.laneCoordinate = -3.4f;
                veh.cruiseSpeed = 9.2f + (i % 3) * 1.3f;
                veh.minLoopCoord = -78f;
                veh.maxLoopCoord = 538f;
            }

            // 6 Southbound LHT vehicles (oncoming right lane x = +3.4, moving -Z passing right beside the player!)
            float[] southZs = { 20f, 92f, 168f, 260f, 355f, 468f };
            for (int i = 0; i < southZs.Length; i++)
            {
                int kind = (i + 1) % 3;
                var veh = CreateTrafficVehicle(root, $"TrafficSouth_{i}", kind, new Vector3(3.4f, 0f, southZs[i]), i + 10, matTire, matRim, matGlass, matHeadlight, matTail);
                veh.axis = KenyaTrafficVehicle.RouteAxis.BoulevardNorthSouth;
                veh.directionSign = -1;
                veh.laneCoordinate = 3.4f;
                veh.cruiseSpeed = 11.4f + (i % 3) * 1.2f;
                veh.minLoopCoord = -78f;
                veh.maxLoopCoord = 538f;
            }

            // 6 Cross-street vehicles crossing East/West at the 3 intersections when their lights turn green
            for (int i = 0; i < IntersectionZs.Length; i++)
            {
                float intZ = IntersectionZs[i];
                // Eastbound (+X, LHT north side of cross road: z = intZ + 3.4)
                var eastVeh = CreateTrafficVehicle(root, $"TrafficCrossE_{i}", i % 2, new Vector3(-42f, 0f, intZ + 3.4f), i + 20, matTire, matRim, matGlass, matHeadlight, matTail);
                eastVeh.axis = KenyaTrafficVehicle.RouteAxis.CrossStreetEastWest;
                eastVeh.directionSign = 1;
                eastVeh.cruiseSpeed = 10.5f;
                eastVeh.minLoopCoord = -76f;
                eastVeh.maxLoopCoord = 76f;

                // Westbound (-X, LHT south side of cross road: z = intZ - 3.4)
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
                // Kenyan 14-Seater Matatu (White body + Yellow PSV stripe + Teal lower accent!)
                var matBody = GetMat("MatatuWhite", Palette.MatatuWhite, 0.5f);
                var matStripe = GetMat("MatatuStripeYellow", Palette.MatatuStripeYellow, 0.45f);
                var matAccent = GetMat("MatatuAccentTeal", Palette.MatatuAccentTeal, 0.45f);

                CreateBox("BodyLower", go.transform, new Vector3(0f, 0.65f, 0f), new Vector3(1.92f, 0.72f, 4.95f), matAccent, false);
                CreateBox("PsvStripe", go.transform, new Vector3(0f, 1.05f, 0f), new Vector3(1.96f, 0.20f, 4.98f), matStripe, false);
                CreateBox("BodyUpper", go.transform, new Vector3(0f, 1.65f, -0.08f), new Vector3(1.88f, 1.05f, 4.78f), matBody, false);
                CreateBox("Windshield", go.transform, new Vector3(0f, 1.62f, 2.34f), new Vector3(1.74f, 0.72f, 0.10f), matGlass, false);
                CreateBox("SideWindows", go.transform, new Vector3(0f, 1.66f, -0.15f), new Vector3(1.92f, 0.62f, 3.85f), matGlass, false);
                CreateBox("HeadL", go.transform, new Vector3(-0.68f, 0.76f, 2.48f), new Vector3(0.32f, 0.18f, 0.08f), matHeadlight, false);
                CreateBox("HeadR", go.transform, new Vector3(0.68f, 0.76f, 2.48f), new Vector3(0.32f, 0.18f, 0.08f), matHeadlight, false);
                CreateBox("TailL", go.transform, new Vector3(-0.72f, 0.85f, -2.48f), new Vector3(0.26f, 0.36f, 0.08f), matTail, false);
                CreateBox("TailR", go.transform, new Vector3(0.72f, 0.85f, -2.48f), new Vector3(0.26f, 0.36f, 0.08f), matTail, false);

                wheels = CreateTrafficWheelSet(go.transform, 0.96f, 1.52f, -1.48f, 0.36f, matTire, matRim);
                col.center = new Vector3(0f, 1.15f, 0f);
                col.size = new Vector3(1.95f, 2.0f, 5.0f);
                ai.vehicleHalfLength = 2.65f;
                ai.wheelRadius = 0.36f;
            }
            else if (kind == 1)
            {
                // Kenyan Town Sedan / Probox
                Color[] carColors = { Palette.CarSilver, Palette.CarMaroon, Palette.CarBlue, Palette.CarGreen };
                Color c = carColors[colorSeed % carColors.Length];
                var matCar = GetMat($"Car_{ColorUtility.ToHtmlStringRGB(c)}", c, 0.65f, 0.25f);

                CreateBox("Chassis", go.transform, new Vector3(0f, 0.62f, 0f), new Vector3(1.82f, 0.64f, 4.35f), matCar, false);
                CreateBox("Cabin", go.transform, new Vector3(0f, 1.24f, -0.22f), new Vector3(1.66f, 0.66f, 2.45f), matCar, false);
                CreateBox("CabinGlass", go.transform, new Vector3(0f, 1.24f, -0.22f), new Vector3(1.70f, 0.48f, 2.18f), matGlass, false);
                CreateBox("HeadL", go.transform, new Vector3(-0.62f, 0.68f, 2.18f), new Vector3(0.32f, 0.16f, 0.08f), matHeadlight, false);
                CreateBox("HeadR", go.transform, new Vector3(0.62f, 0.68f, 2.18f), new Vector3(0.32f, 0.16f, 0.08f), matHeadlight, false);
                CreateBox("TailL", go.transform, new Vector3(-0.64f, 0.72f, -2.18f), new Vector3(0.30f, 0.18f, 0.08f), matTail, false);
                CreateBox("TailR", go.transform, new Vector3(0.64f, 0.72f, -2.18f), new Vector3(0.30f, 0.18f, 0.08f), matTail, false);

                wheels = CreateTrafficWheelSet(go.transform, 0.90f, 1.32f, -1.32f, 0.33f, matTire, matRim);
                col.center = new Vector3(0f, 0.88f, 0f);
                col.size = new Vector3(1.86f, 1.48f, 4.4f);
                ai.vehicleHalfLength = 2.3f;
                ai.wheelRadius = 0.33f;
            }
            else
            {
                // Delivery Lorry
                var matCab = GetMat("TruckCabWhite", Palette.TruckCabWhite, 0.5f);
                var matBox = GetMat("TruckCargoBlue", Palette.TruckCargoBlue, 0.35f);

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
            // 18 Sidewalk pedestrians patrolling both West & East sidewalks
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

            // 6 Zebra-crossing pedestrians at the 3 traffic-light intersections
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

            // Torso + Head + Hair
            CreateBox("Torso", bodyPivot, new Vector3(0f, 0.24f, 0f), new Vector3(0.42f, 0.56f, 0.24f), matShirt, false);
            CreateSphere("Head", bodyPivot, new Vector3(0f, 0.68f, 0f), new Vector3(0.26f, 0.28f, 0.26f), matSkin);
            CreateSphere("Hair", bodyPivot, new Vector3(0f, 0.74f, -0.02f), new Vector3(0.27f, 0.18f, 0.27f), matHair);

            // Pivoted Arms
            ai.leftArm = CreateLimbPivot(bodyPivot, "LeftArm", new Vector3(-0.27f, 0.46f, 0f), new Vector3(0.13f, 0.52f, 0.13f), matShirt, matSkin);
            ai.rightArm = CreateLimbPivot(bodyPivot, "RightArm", new Vector3(0.27f, 0.46f, 0f), new Vector3(0.13f, 0.52f, 0.13f), matShirt, matSkin);

            // Pivoted Legs
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
            ctrl.motorTorque = 2800f;
            ctrl.brakeTorque = 5800f;
            ctrl.maxSpeedKmh = 82f;
            ctrl.centerOfMassOffset = new Vector3(0f, -0.58f, 0.15f);

            // Physics collision box (elevated cleanly above wheel contact height)
            var chassisCol = root.AddComponent<BoxCollider>();
            chassisCol.center = new Vector3(0f, 1.98f, 0f);
            chassisCol.size = new Vector3(2.48f, 2.68f, 11.45f);

            // Materials from Palette
            var matWhite = GetMat("EnaBodyWhite", Palette.EnaBodyWhite, 0.65f, 0.1f);
            var matRed = GetMat("EnaSkirtRed", Palette.EnaSkirtRed, 0.58f, 0.12f);
            var matBlack = GetMat("EnaStripeBlack", Palette.EnaStripeBlack, 0.72f, 0.15f);
            var matOrange = GetMat("EnaLogoOrange", Palette.EnaLogoOrange, 0.55f, 0.1f, Palette.EnaLogoOrange * 0.5f);
            var matChrome = GetMat("EnaChrome", Palette.EnaChrome, 0.9f, 0.85f);
            var matGlass = GetMat("EnaGlassDark", Palette.EnaGlassDark, 0.92f, 0.22f);
            var matHead = GetMat("HeadlightWarm", Palette.HeadlightWarm, 0.8f, 0f, Palette.HeadlightWarm * 2.4f);
            var matTail = GetMat("TaillightRed", Palette.TaillightRed, 0.6f, 0f, Palette.TaillightRed * 1.4f);
            var matPlate = GetMat("PlateYellow", Palette.PlateYellow, 0.4f);
            var matInterior = GetMat("CabInterior", Palette.CabInterior, 0.25f);
            var matTire = GetMat("TireRubber", Palette.TireRubber, 0.2f);
            var matRim = GetMat("WheelRimAlloy", Palette.WheelRimAlloy, 0.78f, 0.82f);

            // Swappable VisualBodyRoot child
            var body = new GameObject("VisualBodyRoot").transform;
            body.SetParent(root.transform, false);

            // Pearl white upper coach shell + red lower skirt + crisp black accent pinstripe
            CreateBox("LowerSkirtRed", body, new Vector3(0f, 1.04f, 0f), new Vector3(2.54f, 0.92f, 11.55f), matRed, false);
            CreateBox("AccentStripeBlack", body, new Vector3(0f, 1.54f, 0f), new Vector3(2.56f, 0.14f, 11.58f), matBlack, false);
            CreateBox("UpperBodyWhite", body, new Vector3(0f, 2.48f, -0.05f), new Vector3(2.52f, 1.76f, 11.46f), matWhite, false);
            CreateBox("RoofCrown", body, new Vector3(0f, 3.38f, -0.10f), new Vector3(2.34f, 0.16f, 11.1f), matWhite, false);

            // Rooftop A/C Unit
            CreateBox("RoofACPod", body, new Vector3(0f, 3.58f, 0.4f), new Vector3(1.65f, 0.30f, 3.6f), matWhite, false);
            CreateBox("RoofACGrille", body, new Vector3(0f, 3.60f, 0.4f), new Vector3(1.42f, 0.32f, 2.8f), matBlack, false);

            // Gloss-black front upper brow + ENA orange emblem + crisp single-line 3D "ENA COACH" header
            CreateBox("FrontBrowBlack", body, new Vector3(0f, 3.06f, 5.72f), new Vector3(2.44f, 0.56f, 0.16f), matBlack, false);
            CreateSphere("EnaOvalLogo", body, new Vector3(-0.84f, 3.06f, 5.82f), new Vector3(0.42f, 0.26f, 0.08f), matOrange);
            CreateWorldSignText("FrontEnaHeader", body, "ENA COACH", new Vector3(0.24f, 3.06f, 5.83f), new Vector2(1.85f, 0.42f), 2.05f, Palette.EnaBodyWhite, Quaternion.Euler(0f, 180f, 0f), noWrap: true);

            // Wraparound dark windshield + side passenger window band + pillars
            CreateBox("FrontWindshield", body, new Vector3(0f, 2.18f, 5.71f), new Vector3(2.40f, 1.18f, 0.14f), matGlass, false);
            CreateBox("SideWindowsBand", body, new Vector3(0f, 2.38f, -0.25f), new Vector3(2.55f, 1.10f, 9.8f), matGlass, false);
            for (float pz = -4.2f; pz <= 4.0f; pz += 1.65f)
            {
                CreateBox($"WindowPillar_{pz:0.0}", body, new Vector3(0f, 2.38f, pz), new Vector3(2.56f, 1.12f, 0.16f), matBlack, false);
            }

            // Side ENA COACH livery lettering (Left & Right flanks)
            CreateWorldSignText("SideSignL", body, "ENA COACH  •  KDK 057M", new Vector3(-1.29f, 1.18f, 0.2f), new Vector2(8.0f, 0.65f), 4.2f, Palette.EnaBodyWhite, Quaternion.Euler(0f, 90f, 0f));
            CreateWorldSignText("SideSignR", body, "ENA COACH  •  KDK 057M", new Vector3(1.29f, 1.18f, 0.2f), new Vector2(8.0f, 0.65f), 4.2f, Palette.EnaBodyWhite, Quaternion.Euler(0f, -90f, 0f));

            // Chrome V-grille + front bumper + headlights + yellow KDK 057M plate
            CreateBox("FrontGrille", body, new Vector3(0f, 1.12f, 5.79f), new Vector3(1.55f, 0.56f, 0.08f), matBlack, false);
            CreateBox("ChromeVBar1", body, new Vector3(0f, 1.26f, 5.84f), new Vector3(1.48f, 0.08f, 0.06f), matChrome, false);
            CreateBox("ChromeVBar2", body, new Vector3(0f, 1.06f, 5.84f), new Vector3(1.25f, 0.07f, 0.06f), matChrome, false);
            CreateBox("FrontPlate", body, new Vector3(0f, 0.72f, 5.84f), new Vector3(0.68f, 0.19f, 0.05f), matPlate, false);
            CreateWorldSignText("FrontPlateTxt", body, "KDK 057M", new Vector3(0f, 0.72f, 5.88f), new Vector2(0.66f, 0.18f), 1.3f, Palette.EnaStripeBlack, Quaternion.Euler(0f, 180f, 0f));
            CreateBox("HeadlightL", body, new Vector3(-0.98f, 0.94f, 5.80f), new Vector3(0.42f, 0.22f, 0.08f), matHead, false);
            CreateBox("HeadlightR", body, new Vector3(0.98f, 0.94f, 5.80f), new Vector3(0.42f, 0.22f, 0.08f), matHead, false);

            // White elephant-ear forward mirrors
            for (int side = -1; side <= 1; side += 2)
            {
                CreateBox($"MirrorArm_{side}", body, new Vector3(side * 1.38f, 2.52f, 5.86f), new Vector3(0.32f, 0.07f, 0.44f), matWhite, false);
                CreateBox($"MirrorHead_{side}", body, new Vector3(side * 1.52f, 2.26f, 6.06f), new Vector3(0.14f, 0.46f, 0.22f), matWhite, false);
            }

            // Rear fascia: tinted rear glass + black engine vent + chrome bumper + yellow KDK 057M plate + Brake Taillights
            CreateBox("RearGlass", body, new Vector3(0f, 2.48f, -5.78f), new Vector3(2.14f, 0.78f, 0.08f), matGlass, false);
            CreateBox("RearEngineVent", body, new Vector3(0f, 1.38f, -5.79f), new Vector3(1.52f, 0.34f, 0.06f), matBlack, false);
            CreateBox("RearChromeTrim", body, new Vector3(0f, 1.56f, -5.81f), new Vector3(2.38f, 0.06f, 0.06f), matChrome, false);
            CreateBox("RearPlate", body, new Vector3(0f, 0.86f, -5.80f), new Vector3(0.72f, 0.20f, 0.05f), matPlate, false);
            CreateWorldSignText("RearPlateTxt", body, "KDK 057M", new Vector3(0f, 0.86f, -5.84f), new Vector2(0.70f, 0.18f), 1.3f, Palette.EnaStripeBlack, Quaternion.identity, noWrap: true);
            CreateWorldSignText("RearSignTxt", body, "ENA COACH", new Vector3(0f, 3.08f, -5.80f), new Vector2(2.2f, 0.44f), 2.5f, Palette.EnaSkirtRed, Quaternion.identity, noWrap: true);
            var tailL = CreateBox("BrakeLightL", body, new Vector3(-1.02f, 1.15f, -5.80f), new Vector3(0.28f, 0.62f, 0.08f), matTail, false);
            var tailR = CreateBox("BrakeLightR", body, new Vector3(1.02f, 1.15f, -5.80f), new Vector3(0.28f, 0.62f, 0.08f), matTail, false);
            ctrl.brakeLightRenderers = new[] { tailL.GetComponent<Renderer>(), tailR.GetComponent<Renderer>() };

            // Right-Hand Drive (RHD) Cockpit Dash + Rotating Steering Wheel + Camera Anchors
            CreateBox("RhdDashboard", body, new Vector3(0f, 1.52f, 5.15f), new Vector3(2.26f, 0.45f, 0.65f), matInterior, false);
            var steerPivot = new GameObject("SteeringWheelPivot").transform;
            steerPivot.SetParent(body, false);
            steerPivot.localPosition = new Vector3(0.68f, 1.82f, 4.84f); // RHD right side
            steerPivot.localRotation = Quaternion.Euler(22f, 0f, 0f);
            CreateCylinder("SteerHub", steerPivot, Vector3.zero, new Vector3(0.46f, 0.04f, 0.46f), matBlack);
            CreateBox("SteerSpoke", steerPivot, Vector3.zero, new Vector3(0.42f, 0.05f, 0.06f), matChrome, false);
            ctrl.steeringWheelVisual = steerPivot;

            var cockpitCam = new GameObject("CockpitCameraAnchor").transform;
            cockpitCam.SetParent(root.transform, false);
            cockpitCam.localPosition = new Vector3(0.68f, 2.34f, 4.42f);
            ctrl.cockpitCameraAnchor = cockpitCam;

            var axleCam = new GameObject("AxleCameraAnchor").transform;
            axleCam.SetParent(root.transform, false);
            axleCam.localPosition = new Vector3(-2.1f, 0.95f, 1.8f);
            axleCam.localRotation = Quaternion.Euler(4f, 14f, 0f);
            ctrl.axleCameraAnchor = axleCam;

            // 8 Tuned WheelColliders (4 axles: front twin-steer + rear drive & tag!)
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
            ctrl.motorTorque = 2650f;
            ctrl.brakeTorque = 5400f;
            ctrl.maxSpeedKmh = 78f;
            ctrl.centerOfMassOffset = new Vector3(0f, -0.55f, 0.1f);

            var chassisCol = root.AddComponent<BoxCollider>();
            chassisCol.center = new Vector3(0f, 1.85f, 0f);
            chassisCol.size = new Vector3(2.42f, 2.55f, 8.65f);

            var matNavy = GetMat("IsuzuCabNavy", Palette.IsuzuCabNavy, 0.62f, 0.2f);
            var matCargo = GetMat("IsuzuCargoSilver", Palette.IsuzuCargoSilver, 0.42f, 0.28f);
            var matGold = GetMat("IsuzuStripeGold", Palette.IsuzuStripeGold, 0.5f, 0.2f);
            var matBullbar = GetMat("BullbarSteel", Palette.BullbarSteel, 0.55f, 0.72f);
            var matWhite = GetMat("EnaBodyWhite", Palette.EnaBodyWhite, 0.65f, 0.1f);
            var matChrome = GetMat("EnaChrome", Palette.EnaChrome, 0.9f, 0.85f);
            var matGlass = GetMat("EnaGlassDark", Palette.EnaGlassDark, 0.92f, 0.22f);
            var matHead = GetMat("HeadlightWarm", Palette.HeadlightWarm, 0.8f, 0f, Palette.HeadlightWarm * 2.4f);
            var matTail = GetMat("TaillightRed", Palette.TaillightRed, 0.6f, 0f, Palette.TaillightRed * 1.4f);
            var matPlate = GetMat("PlateYellow", Palette.PlateYellow, 0.4f);
            var matInterior = GetMat("CabInterior", Palette.CabInterior, 0.25f);
            var matTire = GetMat("TireRubber", Palette.TireRubber, 0.2f);
            var matRim = GetMat("WheelRimAlloy", Palette.WheelRimAlloy, 0.78f, 0.82f);

            var body = new GameObject("VisualBodyRoot").transform;
            body.SetParent(root.transform, false);

            // Chassis frame rails
            CreateBox("ChassisFrame", body, new Vector3(0f, 0.82f, -0.2f), new Vector3(1.2f, 0.28f, 8.1f), matBullbar, false);

            // Deep navy Isuzu Forward Cab
            CreateBox("ForwardCab", body, new Vector3(0f, 1.82f, 3.05f), new Vector3(2.26f, 2.02f, 2.25f), matNavy, false);
            CreateBox("RoofFairing", body, new Vector3(0f, 3.04f, 2.85f), new Vector3(2.14f, 0.48f, 1.55f), matWhite, false);
            CreateBox("CabWindshield", body, new Vector3(0f, 2.18f, 4.18f), new Vector3(2.06f, 0.92f, 0.10f), matGlass, false);
            CreateBox("CabSideGlass", body, new Vector3(0f, 2.15f, 3.18f), new Vector3(2.30f, 0.80f, 1.38f), matGlass, false);
            CreateBox("CabGrille", body, new Vector3(0f, 1.38f, 4.19f), new Vector3(1.62f, 0.48f, 0.08f), matChrome, false);

            // Heavy welded Kenyan Kikapu steel bull-bar + vertical air snorkel
            CreateBox("BullbarBase", body, new Vector3(0f, 0.92f, 4.42f), new Vector3(2.34f, 0.28f, 0.24f), matBullbar, false);
            CreateBox("BullbarTop", body, new Vector3(0f, 1.55f, 4.44f), new Vector3(2.10f, 0.10f, 0.10f), matBullbar, false);
            CreateBox("BullbarPostL", body, new Vector3(-0.72f, 1.24f, 4.44f), new Vector3(0.10f, 0.68f, 0.10f), matBullbar, false);
            CreateBox("BullbarPostR", body, new Vector3(0.72f, 1.24f, 4.44f), new Vector3(0.10f, 0.68f, 0.10f), matBullbar, false);
            CreateCylinder("AirSnorkel", body, new Vector3(-1.20f, 2.25f, 2.15f), new Vector3(0.16f, 1.15f, 0.16f), matBullbar);

            // Corrugated EPH Logistics Cargo Box
            CreateBox("CargoBoxMain", body, new Vector3(0f, 2.18f, -1.25f), new Vector3(2.42f, 2.48f, 5.95f), matCargo, false);
            CreateBox("CargoGoldStripe", body, new Vector3(0f, 1.35f, -1.25f), new Vector3(2.46f, 0.28f, 5.98f), matGold, false);
            CreateWorldSignText("CargoSignL", body, "EPH LOGISTICS  •  ISUZU FRR 90", new Vector3(-1.24f, 2.28f, -1.25f), new Vector2(5.4f, 0.72f), 3.4f, Palette.IsuzuCabNavy, Quaternion.Euler(0f, 90f, 0f));
            CreateWorldSignText("CargoSignR", body, "EPH LOGISTICS  •  ISUZU FRR 90", new Vector3(1.24f, 2.28f, -1.25f), new Vector2(5.4f, 0.72f), 3.4f, Palette.IsuzuCabNavy, Quaternion.Euler(0f, -90f, 0f));

            // Headlights, plate, taillights
            CreateBox("FrontPlate", body, new Vector3(0f, 0.74f, 4.55f), new Vector3(0.66f, 0.18f, 0.05f), matPlate, false);
            CreateBox("HeadL", body, new Vector3(-0.88f, 1.06f, 4.22f), new Vector3(0.36f, 0.22f, 0.08f), matHead, false);
            CreateBox("HeadR", body, new Vector3(0.88f, 1.06f, 4.22f), new Vector3(0.36f, 0.22f, 0.08f), matHead, false);
            var tailL = CreateBox("TailL", body, new Vector3(-0.96f, 0.96f, -4.25f), new Vector3(0.38f, 0.20f, 0.08f), matTail, false);
            var tailR = CreateBox("TailR", body, new Vector3(0.96f, 0.96f, -4.25f), new Vector3(0.38f, 0.20f, 0.08f), matTail, false);
            ctrl.brakeLightRenderers = new[] { tailL.GetComponent<Renderer>(), tailR.GetComponent<Renderer>() };

            // RHD Cockpit + Steering Wheel
            CreateBox("RhdDash", body, new Vector3(0f, 1.65f, 3.62f), new Vector3(2.0f, 0.40f, 0.55f), matInterior, false);
            var steerPivot = new GameObject("SteeringWheelPivot").transform;
            steerPivot.SetParent(body, false);
            steerPivot.localPosition = new Vector3(0.58f, 1.92f, 3.35f);
            steerPivot.localRotation = Quaternion.Euler(22f, 0f, 0f);
            CreateCylinder("SteerHub", steerPivot, Vector3.zero, new Vector3(0.44f, 0.04f, 0.44f), matBullbar);
            ctrl.steeringWheelVisual = steerPivot;

            var cockpitCam = new GameObject("CockpitCameraAnchor").transform;
            cockpitCam.SetParent(root.transform, false);
            cockpitCam.localPosition = new Vector3(0.58f, 2.38f, 2.95f);
            ctrl.cockpitCameraAnchor = cockpitCam;

            var axleCam = new GameObject("AxleCameraAnchor").transform;
            axleCam.SetParent(root.transform, false);
            axleCam.localPosition = new Vector3(-2.0f, 0.95f, 1.2f);
            axleCam.localRotation = Quaternion.Euler(4f, 14f, 0f);
            ctrl.axleCameraAnchor = axleCam;

            // 6 Wheels (2 front steer + dual rear drive wheels)
            float[] axleZs = { 2.85f, -1.85f, -2.95f };
            bool[] steerFlags = { true, false, false };
            bool[] driveFlags = { false, true, true };
            ctrl.wheels = BuildWheelColliderSet(root.transform, axleZs, steerFlags, driveFlags, halfTrack: 1.12f, radius: 0.48f, suspension: 0.24f, matTire, matRim);

            return ctrl;
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

                    // Visual wheel mesh
                    var vis = new GameObject($"WV_{tag}").transform;
                    vis.SetParent(visParent, false);
                    vis.localPosition = wcGo.transform.localPosition;

                    var tire = CreateCylinder("Tire", vis, Vector3.zero, new Vector3(radius * 2f, 0.18f, radius * 2f), matTire);
                    tire.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                    var rim = CreateCylinder("Rim", vis, Vector3.zero, new Vector3(radius * 1.18f, 0.195f, radius * 1.18f), matRim);
                    rim.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);

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
            var rt = tmp.rectTransform;
            rt.sizeDelta = rectSize;
        }

        private static void SetBuildSettingsScene(string path)
        {
            var list = new List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(path, true)
            };
            EditorBuildSettings.scenes = list.ToArray();
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
