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
    /// Full Connected ETS2-Style Kenya ENA Coach Highway & 10-Town Map Builder:
    /// Builds all 6 connected Catmull-Rom PBR highways + 2 multi-exit roundabouts (ZERO speed bumps!),
    /// 2600m x 1500m multi-biome Kenyan terrain (Rift Valley, Tea Highlands, Tsavo, Lake Victoria, Indian Ocean),
    /// 10 living Kenyan ENA Coach hubs (Nairobi CBD + KICC, Narok, Bomet, Kisii, Migori/Sirare Border,
    /// Nakuru, Kericho, Kisumu Lakeside, Voi, Mombasa + Moi Ave Tusks Arch), 3 Rubis Petrol Stations,
    /// KeNHA directional overhead gantries, KPLC power lines, branching Acacia & Coastal Palm trees,
    /// 42 spline-following LHT passing vehicles, 50+ articulated pedestrians, Day/Sunset/Night cycle,
    /// live GPS minimap + interactive [M] Full Kenya Route Map, and switchable ENA Coach KDK 057M & EPH Isuzu FRR 90.
    /// </summary>
    public static class KenyaStreetTownBuilder
    {
        private const string ScenePath = "Assets/KenyaStreetSim/Scenes/KenyaTownStreets.unity";
        private const string MatFolder = "Assets/KenyaStreetSim/Materials";
        private const string SettingsFolder = "Assets/KenyaStreetSim/Settings";
        private const string VolumeProfilePath = "Assets/KenyaStreetSim/Settings/KenyaStreetVolumeProfile.asset";
        private const string SkyMatPath = "Assets/KenyaStreetSim/Materials/KenyaSky.mat";

        private static readonly string[] ShopNames =
        {
            "NAIROBI CHEMIST", "M-PESA AGENT", "JAVA CAFE", "KISUMU HARDWARE",
            "NAIVAS MART", "SAFARI HOTEL", "CYBER & PRINT", "QUICK BITES",
            "EQUITY AGENT", "SIMBA ELECTRONICS", "ELDORET SPARES", "KENYA PHARMACY",
            "UHURU BAKERY", "RIFT VALLEY OPTICS", "KCB BANK", "SAVANNAH GRILL"
        };

        private static Dictionary<string, Material> matCache;
        private static Shader urpLitShader;
        private static Shader urpParticleShader;

        private static Texture2D texTarmacAlb, texTarmacNrm;
        private static Texture2D texMurramAlb, texMurramNrm;
        private static Texture2D texCabroAlb, texCabroNrm;
        private static Texture2D texPlasterAlb, texPlasterNrm;
        private static Texture2D texMabatiAlb, texMabatiNrm;
        private static Texture2D texBarkAlb, texBarkNrm;
        private static Texture2D texLeafCluster;
        private static Texture2D texTireAlb, texTireNrm;
        private static Texture2D texGlassAlb, texGlassNrm;

        [MenuItem("Kenya Street Sim/Build Full Kenya ENA Coach Map")]
        public static string BuildKenyaTownScene()
        {
            EnsureFolders();
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

            var worldRoot = new GameObject("FullKenyaMapWorld");
            var streetLights = new List<Light>();
            var lampRenderers = new List<Renderer>();

            // 1. Full Multi-Biome Terrain + Lake Victoria + Indian Ocean + All 6 Smooth Spline Highways + 2 Roundabouts
            BuildKenyaTerrainWaterAndHighways(worldRoot.transform);

            // 2. Nairobi CBD Traffic Light Intersection + KICC Skyline + Sidewalks + KPLC Power Lines
            var nairobiLight = BuildNairobiCbdCore(worldRoot.transform, streetLights, lampRenderers);

            // 3. Build All 10 Living Kenyan ENA Coach Towns + Regional Landmarks + 10 Bus Stage Terminals
            var stages = BuildAllTenKenyaTownsAndStages(worldRoot.transform, streetLights, lampRenderers);

            // 4. KeNHA Directional Highway Gantries, Roundabout Monuments, Sirare Border Arch & Mombasa Tusks!
            BuildKenyaNationalLandmarksAndGantries(worldRoot.transform);

            // 5. Highway Acacia Savanna, Kericho Tea Hills & Coastal Palm Trees along corridors
            BuildHighwayBiomeFoliage(worldRoot.transform);

            // 6. 42 Sculpted Spline-Following LHT Passing Kenyan Vehicles across ALL 6 Highways!
            BuildFullKenyaSplineTraffic(worldRoot.transform);

            // 7. Sidewalk & Zebra Pedestrians in Nairobi CBD
            BuildNairobiPedestrians(worldRoot.transform, nairobiLight);

            // 8. Sculpted Player Fleet (ENA Coach KDK 057M & EPH Isuzu FRR 90)
            var fleetRoot = new GameObject("PlayerFleet");
            Vector3 startPos = new Vector3(-3.5f, 0.30f, -22f);
            var enaCoach = BuildEnaCoach(fleetRoot.transform, startPos);
            var isuzuTruck = BuildIsuzuFrr(fleetRoot.transform, startPos);
            isuzuTruck.gameObject.SetActive(false);

            // 9. Runtime Systems: Audio, Time of Day, ETS2 HUD, Interactive [M] Full Kenya Map, Game Loop
            var sysRoot = new GameObject("KenyaStreetSystems");
            sysRoot.AddComponent<KenyaStreetAudio>();

            var tod = sysRoot.AddComponent<KenyaTimeOfDay>();
            tod.sunLight = sunLight;
            tod.skyboxMaterial = skyMat;
            tod.streetPointLights = streetLights.ToArray();
            tod.lampGlowRenderers = lampRenderers.ToArray();

            var hud = sysRoot.AddComponent<KenyaStreetHUD>();
            var routeMap = sysRoot.AddComponent<KenyaRouteMapUI>();
            var loop = sysRoot.AddComponent<StreetGameLoop>();
            loop.fleet = new[] { enaCoach, isuzuTruck };
            loop.stages = stages;
            loop.streetCamera = camObj.GetComponent<KenyaStreetCamera>();
            loop.hud = hud;
            loop.routeMap = routeMap;
            loop.startPosition = startPos;

            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene, ScenePath);
            SetBuildSettingsScene(ScenePath);

            int meshCount = Object.FindObjectsByType<MeshRenderer>().Length;
            int defaultMatCount = CountDefaultMaterials();
            return $"Built Full Kenya ENA Coach Map ({ScenePath}) | Towns=10 | Highways=6 | Roundabouts=2 | Renderers={meshCount} | DefaultMaterials={defaultMatCount} | SpeedBumps=0";
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
                KenyaSculptedMeshFactory.MeshFolder,
                "Assets/KenyaStreetSim/Meshes/Roads"
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
            if (matCache.TryGetValue(key, out var cached) && cached != null) return cached;

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
                mat.SetTextureScale("_BaseMap", tiling ?? Vector2.one);
            }
            else mat.SetTexture("_BaseMap", null);

            if (normalTex != null)
            {
                mat.EnableKeyword("_NORMALMAP");
                mat.SetTexture("_BumpMap", normalTex);
                mat.SetFloat("_BumpScale", bumpScale);
                mat.SetTextureScale("_BumpMap", tiling ?? Vector2.one);
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
                mat.SetFloat("_Cull", 0f);
                mat.renderQueue = (int)RenderQueue.AlphaTest;
            }
            else
            {
                mat.DisableKeyword("_ALPHATEST_ON");
                mat.SetFloat("_AlphaClip", 0f);
                mat.SetFloat("_Cull", 2f);
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

        private static Material GetTransparentGlassMat(string key, Color color)
        {
            string path = $"{MatFolder}/M_{key}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(urpLitShader);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
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
            RenderSettings.fogDensity = 0.0011f;

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
            bl.intensity.value = 0.32f;
            bl.scatter.overrideState = true;
            bl.scatter.value = 0.66f;

            if (!profile.TryGet<Vignette>(out var vig)) vig = profile.Add<Vignette>();
            vig.intensity.overrideState = true;
            vig.intensity.value = 0.15f;
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
            camGo.transform.position = new Vector3(-9f, 4.8f, -34f);
            camGo.transform.LookAt(new Vector3(-2f, 2.4f, 25f));

            var cam = camGo.AddComponent<Camera>();
            cam.allowHDR = true;
            cam.fieldOfView = 54f;
            cam.nearClipPlane = 0.12f;
            cam.farClipPlane = 1300f;

            var camData = camGo.AddComponent<UniversalAdditionalCameraData>();
            camData.renderPostProcessing = true;
            camData.antialiasing = AntialiasingMode.FastApproximateAntialiasing;

            camGo.AddComponent<AudioListener>();
            camGo.AddComponent<KenyaStreetCamera>();
            return camGo;
        }

        private static void BuildKenyaTerrainWaterAndHighways(Transform parent)
        {
            var matTerrain = GetMat("RiftMurramTerrain", Color.white, 0.08f, 0f, null, texMurramAlb, texMurramNrm, Vector2.one, 1.35f);
            var matTarmac = GetMat("KenyaHighwayTarmac", Color.white, 0.30f, 0f, null, texTarmacAlb, texTarmacNrm, Vector2.one, 1.25f);
            var matMurramShoulder = GetMat("KenyaMurramShoulder", Color.white, 0.10f, 0f, null, texMurramAlb, texMurramNrm, Vector2.one, 1.3f);
            var matWhiteLine = GetMat("RoadLineWhite", Palette.RoadLineWhite, 0.15f);
            var matYellowLine = GetMat("RoadLineYellow", Palette.RoadLineYellow, 0.18f);
            var matCurb = GetMat("CurbStone", Palette.CurbStone, 0.22f, 0f, null, texPlasterAlb, texPlasterNrm, new Vector2(2f, 2f), 0.9f);
            var matGrass = GetMat("RoundaboutLawn", Palette.GrassVerge, 0.12f);
            var matTeaHills = GetMat("KerichoTeaGreen", Palette.Hex("#3E7C2F"), 0.14f, 0f, null, texMurramAlb, texMurramNrm, new Vector2(18f, 18f), 1.1f);
            var matLakeWater = GetMat("LakeVictoriaWater", Palette.Hex("#1E6EA1"), 0.92f, 0.12f);
            var matOceanWater = GetMat("IndianOceanWater", Palette.Hex("#1289A7"), 0.94f, 0.15f);

            // 1. Full 2600m x 1500m Multi-Biome Kenyan Terrain
            var terrainGo = CreateCustomMesh("FullKenyaTerrainHills", parent, Vector3.zero, Quaternion.identity, Vector3.one, KenyaSculptedMeshFactory.RiftValleyTerrainMesh, matTerrain, withMeshCollider: false);
            terrainGo.isStatic = true;

            // Safety flat physics floor well below highways so no vehicle can ever fall out of world
            CreateBox("KenyaPhysicsSubfloor", parent, new Vector3(-220f, -0.45f, 200f), new Vector3(2650f, 0.4f, 1550f), matTerrain, withCollider: true);

            // Lush Emerald Tea Plantation Belts around Kericho & Bomet highlands!
            CreateBox("KerichoTeaBeltNorth", parent, new Vector3(-680f, -0.22f, 675f), new Vector3(380f, 0.12f, 130f), matTeaHills, false);
            CreateBox("BometTeaBeltSouth", parent, new Vector3(-860f, -0.22f, 115f), new Vector3(260f, 0.12f, 110f), matTeaHills, false);

            // Shimmering Lake Victoria (west of Kisumu), Lake Nakuru glimpse, and Indian Ocean (Mombasa Coast)!
            CreateBox("LakeVictoriaWaterPlane", parent, new Vector3(-1350f, -0.28f, 560f), new Vector3(380f, 0.2f, 420f), matLakeWater, false);
            CreateBox("LakeNakuruWaterPlane", parent, new Vector3(-185f, -0.26f, 405f), new Vector3(135f, 0.2f, 95f), matLakeWater, false);
            CreateBox("IndianOceanWaterPlane", parent, new Vector3(920f, -0.28f, -270f), new Vector3(420f, 0.2f, 440f), matOceanWater, false);

            // 2. Smooth Catmull-Rom Extruded PBR Highways & Roundabouts (ZERO speed bumps!)
            KenyaRoadSplineBuilder.BuildAllHighwayAndRoundaboutGeometry(
                parent, matTarmac, matMurramShoulder, matWhiteLine, matYellowLine, matCurb, matGrass);
        }

        private static StreetTrafficLight BuildNairobiCbdCore(Transform parent, List<Light> pointLights, List<Renderer> glowRenderers)
        {
            var cbd = new GameObject("Nairobi_CBD_Core").transform;
            cbd.SetParent(parent, false);

            var matCabro = GetMat("CabroSidewalk", Color.white, 0.22f, 0f, null, texCabroAlb, texCabroNrm, new Vector2(2f, 24f), 1.2f);
            var matCurb = GetMat("CurbStone", Palette.CurbStone, 0.22f, 0f, null, texPlasterAlb, texPlasterNrm, new Vector2(1f, 20f), 0.9f);
            var matWhite = GetMat("RoadLineWhite", Palette.RoadLineWhite, 0.15f);
            var matPole = GetMat("LampPostMetal", Palette.LampPostMetal, 0.55f, 0.75f);
            var matLampGlow = GetMat("LampGlow", Palette.LampGlow, 0.6f, 0f, Palette.LampGlow * 0.5f);
            var matHousing = GetMat("SignalHousing", Palette.SignalHousing, 0.35f, 0.3f);
            var matLens = GetMat("SignalLensBase", Palette.SignalOff, 0.7f, 0.1f, Color.black);
            var matBark = GetMat("AcaciaBarkPBR", Color.white, 0.10f, 0f, null, texBarkAlb, texBarkNrm, new Vector2(1f, 3f), 1.35f);
            var matLeaf = GetMat("AcaciaLeafAlpha", Color.white, 0.12f, 0f, null, texLeafCluster, null, Vector2.one, 1f, alphaClipDoubleSided: true);

            // Interlocking Cabro sidewalks along Nairobi Haile Selassie Avenue (Z = -52..126)
            for (int side = -1; side <= 1; side += 2)
            {
                CreateBox($"NairobiCurb_{side}", cbd, new Vector3(side * 7.22f, 0.08f, 37f), new Vector3(0.44f, 0.16f, 178f), matCurb, true);
                CreateBox($"NairobiSidewalk_{side}", cbd, new Vector3(side * 9.85f, 0.08f, 37f), new Vector3(4.82f, 0.16f, 178f), matCabro, true);
            }

            // Zebra Crossing & Working 3-Color Traffic Light at Nairobi CBD Plaza (Z = 95)
            for (float x = -5.4f; x <= 5.5f; x += 1.2f)
            {
                CreateBox($"NairobiZebra_{x:0.0}", cbd, new Vector3(x, 0.018f, 95f), new Vector3(0.58f, 0.014f, 3.0f), matWhite, false);
            }

            var lightGo = new GameObject("NairobiTrafficLight");
            lightGo.transform.SetParent(cbd, false);
            lightGo.transform.position = new Vector3(0f, 0f, 95f);
            var ctrl = lightGo.AddComponent<StreetTrafficLight>();
            ctrl.intersectionZ = 95f;
            var mainRed = new List<Renderer>();
            var mainYel = new List<Renderer>();
            var mainGrn = new List<Renderer>();
            BuildTrafficGantry(lightGo.transform, new Vector3(-8.2f, 0.16f, 102f), 180f, matPole, matHousing, matLens, mainRed, mainYel, mainGrn);
            BuildTrafficGantry(lightGo.transform, new Vector3( 8.2f, 0.16f,  88f),   0f, matPole, matHousing, matLens, mainRed, mainYel, mainGrn);
            ctrl.mainRedLenses = mainRed.ToArray();
            ctrl.mainYellowLenses = mainYel.ToArray();
            ctrl.mainGreenLenses = mainGrn.ToArray();

            // Iconic KICC Tower (Cylindrical Kenyatta International Convention Centre + Helipad!) + Nairobi Glass Skyline
            BuildKiccAndNairobiSkyline(cbd);

            // Nairobi Street Lamps & Branching Acacia Trees + KPLC Power Lines along Haile Selassie Ave
            for (float z = -42f; z <= 118f; z += 26f)
            {
                if (Mathf.Abs(z - 95f) < 12f) continue;
                for (int side = -1; side <= 1; side += 2)
                {
                    if (side == -1 && Mathf.Abs(z - 55f) < 14f) continue;
                    var lamp = new GameObject($"NairobiLamp_{z:0}_{side}").transform;
                    lamp.SetParent(cbd, false);
                    lamp.position = new Vector3(side * 7.75f, 0.16f, z);
                    lamp.rotation = Quaternion.Euler(0f, side < 0 ? 0f : 180f, 0f);
                    CreateCylinder("Post", lamp, new Vector3(0f, 3.8f, 0f), new Vector3(0.18f, 3.8f, 0.18f), matPole);
                    CreateBox("Arm", lamp, new Vector3(1.1f, 7.45f, 0f), new Vector3(2.4f, 0.14f, 0.14f), matPole, false);
                    var head = CreateBox("Head", lamp, new Vector3(2.15f, 7.32f, 0f), new Vector3(0.68f, 0.16f, 0.36f), matLampGlow, false);
                    glowRenderers.Add(head.GetComponent<Renderer>());

                    if (side == -1 && Mathf.Abs(z) < 80f)
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

                    // Branching Acacia tree in stone planter
                    float tz = z + 13f;
                    if (tz < 118f && Mathf.Abs(tz - 95f) > 12f && !(side == -1 && Mathf.Abs(tz - 55f) < 14f))
                    {
                        var tree = new GameObject($"NairobiAcacia_{tz:0}_{side}").transform;
                        tree.SetParent(cbd, false);
                        tree.position = new Vector3(side * 9.8f, 0.16f, tz);
                        CreateBox("Planter", tree, new Vector3(0f, 0.22f, 0f), new Vector3(1.65f, 0.44f, 1.65f), matCurb, true);
                        CreateCustomMesh("Trunk", tree, new Vector3(0f, 0.40f, 0f), Quaternion.Euler(0f, tz * 13f, 0f), Vector3.one, KenyaSculptedMeshFactory.TreeTrunkMesh, matBark, false);
                        CreateCustomMesh("Crown", tree, new Vector3(0f, 0.40f, 0f), Quaternion.Euler(0f, tz * 13f, 0f), Vector3.one, KenyaSculptedMeshFactory.TreeFoliageMesh, matLeaf, false);
                    }
                }
            }

            // KPLC Utility Poles & Sagging Power Cables along East side of Nairobi CBD
            BuildKplcPowerStretch(cbd, new Vector3(11.3f, 0.16f, -45f), new Vector3(11.3f, 0.16f, 115f), 5);

            // Lit Rubis Petrol Station at Nairobi South Exit (East side, Z = 10), Bomet & Voi!
            BuildRubisPetrolStation(parent, new Vector3(26f, 0.16f, 12f), 0f, "RUBIS NAIROBI", pointLights, glowRenderers);
            BuildRubisPetrolStation(parent, new Vector3(-835f, 0.16f, 224f), -75f, "RUBIS BOMET", pointLights, glowRenderers);
            BuildRubisPetrolStation(parent, new Vector3(405f, 0.16f, -42f), 124f, "RUBIS VOI TSAVO", pointLights, glowRenderers);

            return ctrl;
        }

        private static void BuildKiccAndNairobiSkyline(Transform parent)
        {
            var sky = new GameObject("NairobiSkyline_KICC").transform;
            sky.SetParent(parent, false);

            var matKiccStone = GetMat("KiccTerracotta", Palette.Hex("#C98E62"), 0.22f, 0f, null, texPlasterAlb, texPlasterNrm, new Vector2(4f, 8f), 1.0f);
            var matGlassTower = GetMat("NairobiSkyscraperGlass", Color.white, 0.92f, 0.35f, null, texGlassAlb, texGlassNrm, new Vector2(5f, 14f), 1.0f);
            var matSteel = GetMat("SkyscraperTrim", Palette.TrimDark, 0.5f, 0.6f);

            // Iconic KICC Cylindrical Tower + Revolving Restaurant Crown + Helipad overlooking Nairobi CBD!
            var kicc = new GameObject("KICC_Tower_Nairobi").transform;
            kicc.SetParent(sky, false);
            kicc.position = new Vector3(-36f, 0f, 65f);
            CreateBox("KiccPodium", kicc, new Vector3(0f, 4.5f, 0f), new Vector3(24f, 9f, 24f), matKiccStone, false);
            CreateCylinder("KiccMainShaft", kicc, new Vector3(0f, 32f, 0f), new Vector3(14f, 26f, 14f), matKiccStone);
            CreateCylinder("KiccGlassBand", kicc, new Vector3(0f, 54f, 0f), new Vector3(16.5f, 2.2f, 16.5f), matGlassTower);
            CreateCylinder("KiccHelipadCrown", kicc, new Vector3(0f, 58.5f, 0f), new Vector3(18.5f, 1.4f, 18.5f), matKiccStone);
            CreateWorldSignText("KiccTitle", kicc, "KICC NAIROBI", new Vector3(8.5f, 58.6f, 0f), new Vector2(12f, 2.2f), 5.5f, Palette.UiAccentAmber, Quaternion.Euler(0f, -90f, 0f), noWrap: true);

            // Britam / UAP-inspired sleek Nairobi skyline glass towers behind the shops
            CreateBox("BritamStyleTower", sky, new Vector3(44f, 34f, 88f), new Vector3(15f, 68f, 15f), matGlassTower, false);
            CreateBox("BritamSpire", sky, new Vector3(44f, 72f, 88f), new Vector3(5f, 14f, 5f), matSteel, false);
            CreateBox("UapStyleTower", sky, new Vector3(-42f, 28f, 18f), new Vector3(16f, 56f, 14f), matGlassTower, false);
            CreateBox("ImStyleTower", sky, new Vector3(42f, 23f, -22f), new Vector3(14f, 46f, 14f), matGlassTower, false);
        }

        private static BusStageZone[] BuildAllTenKenyaTownsAndStages(Transform parent, List<Light> pointLights, List<Renderer> glowRenderers)
        {
            var townsRoot = new GameObject("Kenya_10_Towns_And_Stages").transform;
            townsRoot.SetParent(parent, false);

            var matTrim = GetMat("TrimDark", Palette.TrimDark, 0.25f, 0.1f, null, texPlasterAlb, texPlasterNrm, new Vector2(2f, 1f), 0.7f);
            var matGlass = GetMat("GlassWindowPBR", Color.white, 0.90f, 0.30f, null, texGlassAlb, texGlassNrm, Vector2.one, 1.0f);
            var matShopGlass = GetMat("ShopGlassPBR", Color.white, 0.88f, 0.25f, null, texGlassAlb, texGlassNrm, new Vector2(4f, 1f), 1.0f);
            var matFrame = GetMat("WindowFrame", Palette.WindowFrame, 0.4f, 0.2f);
            var matRoofMabati = GetMat("RoofMabati", Palette.RoofIron, 0.38f, 0.45f, null, texMabatiAlb, texMabatiNrm, new Vector2(4f, 3f), 1.35f);
            var matSign = GetMat("ShopSignBoard", Palette.ShopSignBoard, 0.35f);
            var matTankBlk = GetMat("WaterTankBlack", Palette.WaterTankBlack, 0.35f);
            var matTankBlu = GetMat("WaterTankBlue", Palette.WaterTankBlue, 0.38f);
            var matRail = GetMat("BalconyRailMetal", Palette.LampPostMetal, 0.5f, 0.7f);
            var matShelterFrame = GetMat("BusShelterFrame", Palette.BusShelterFrame, 0.45f, 0.5f);
            var matShelterRoof = GetMat("BusShelterRoofMabati", Palette.BusShelterRoof, 0.42f, 0.25f, null, texMabatiAlb, texMabatiNrm, new Vector2(4f, 2f), 1.2f);
            var matBench = GetMat("BenchWood", Palette.BenchWood, 0.25f, 0f, null, texBarkAlb, texBarkNrm, Vector2.one, 0.8f);
            var matZone = GetMat("StageZonePad", Palette.StageZoneAccent, 0.3f, 0f, Palette.StageZoneAccent * 0.7f);
            var matBeacon = GetMat("StageBeacon", Palette.StageZoneAccent, 0.5f, 0.2f, Palette.StageZoneAccent * 1.6f);
            var matParticle = GetParticleMat("StageConfetti", Palette.UiAccentAmber);
            var matCabro = GetMat("TownCabroWalk", Color.white, 0.22f, 0f, null, texCabroAlb, texCabroNrm, new Vector2(2f, 16f), 1.15f);

            var rng = new System.Random(2542026);
            int shopCounter = 0;
            var stages = new BusStageZone[KenyaNetworkData.Hubs.Length];

            for (int h = 0; h < KenyaNetworkData.Hubs.Length; h++)
            {
                var hub = KenyaNetworkData.Hubs[h];
                var townGo = new GameObject($"Town_{h:00}_{hub.name}").transform;
                townGo.SetParent(townsRoot, false);
                townGo.position = new Vector3(hub.xz.x, 0f, hub.xz.y);
                townGo.rotation = Quaternion.Euler(0f, hub.roadYawDeg, 0f);

                // Sidewalk strips flanking each regional Kenyan town main street (Nairobi CBD already built longer ones)
                if (h > 0)
                {
                    CreateBox("TownWalkL", townGo, new Vector3(-9.6f, 0.08f, 0f), new Vector3(4.6f, 0.16f, 110f), matCabro, true);
                    CreateBox("TownWalkR", townGo, new Vector3( 9.6f, 0.08f, 0f), new Vector3(4.6f, 0.16f, 110f), matCabro, true);
                }

                // Place authentic Kenyan multi-story commercial shops lining BOTH sides of the town avenue!
                int pairsPerTown = (h == 0) ? 7 : 5;
                float zStart = (h == 0) ? -92f : -46f;
                float spacing = (h == 0) ? 21f : 22f;

                for (int k = 0; k < pairsPerTown; k++)
                {
                    float localZ = zStart + k * spacing;
                    for (int side = -1; side <= 1; side += 2)
                    {
                        // Leave open space for Nairobi Rubis station, Nairobi traffic light, or the left-side Bus Stage bay
                        if (h == 0 && side == 1 && Mathf.Abs((hub.xz.y + localZ) - 12f) < 24f) continue;
                        if (h == 0 && Mathf.Abs((hub.xz.y + localZ) - 95f) < 12f) continue;
                        if (side == -1 && Mathf.Abs(localZ) < 12f) continue; // clear bay for ENA Coach Terminal shelter!

                        float wFront = 14.5f + (float)rng.NextDouble() * 4.5f;
                        float depth = 11.5f;
                        int stories = (h == 0) ? rng.Next(3, 5) : (hub.biome == KenyaNetworkData.KenyaBiome.CoastalMombasa ? rng.Next(2, 4) : rng.Next(2, 4));

                        Color facadeCol = hub.biome == KenyaNetworkData.KenyaBiome.CoastalMombasa
                            ? (k % 2 == 0 ? Palette.FacadeCream : Palette.Hex("#E9E4D8")) // White/cream Swahili coastal aesthetic!
                            : Palette.BuildingFacades[rng.Next(Palette.BuildingFacades.Length)];
                        Color awningCol = Palette.ShopAwnings[rng.Next(Palette.ShopAwnings.Length)];
                        string shopTitle = (k == 0 && side == 1)
                            ? $"{hub.name.ToUpperInvariant()} PLAZA"
                            : ShopNames[shopCounter++ % ShopNames.Length];

                        var facadeMat = GetMat($"FacadePBR_{ColorUtility.ToHtmlStringRGB(facadeCol)}", facadeCol, 0.16f, 0f, null, texPlasterAlb, texPlasterNrm, new Vector2(3f, 2.5f), 1.1f);
                        var awningMat = GetMat($"AwningMabati_{ColorUtility.ToHtmlStringRGB(awningCol)}", awningCol, 0.35f, 0.25f, null, texMabatiAlb, texMabatiNrm, new Vector2(5f, 1f), 1.25f);

                        BuildKenyanShopBuilding(
                            townGo,
                            new Vector3(side * (12.6f + depth * 0.5f), 0.16f, localZ),
                            yawDeg: side < 0 ? 90f : -90f,
                            widthFront: wFront,
                            depth: depth,
                            stories: stories,
                            facadeMat, awningMat, matTrim, matGlass, matShopGlass, matFrame, matRoofMabati, matSign,
                            rng.NextDouble() > 0.45 ? matTankBlk : matTankBlu, matRail,
                            shopTitle,
                            hasWaterTank: rng.NextDouble() > 0.35);
                    }
                }

                // Shaded ENA Coach Bus Stage Terminal on the left-hand curb of EVERY Kenyan town!
                var stageGo = new GameObject($"BusStage_{h + 1:00}_{hub.name}");
                stageGo.transform.SetParent(townGo, false);
                stageGo.transform.localPosition = new Vector3(-4.2f, 0f, 0f);

                var zone = stageGo.AddComponent<BusStageZone>();
                zone.stageIndex = h;
                zone.stageName = $"{hub.name} ENA Terminal";
                zone.rewardKes = hub.fareKes;
                zone.passengerCount = hub.passengers;

                var padGo = CreateBox("StageFloorPad", stageGo.transform, new Vector3(0.4f, 0.022f, 0f), new Vector3(5.4f, 0.02f, 16.5f), matZone, false);
                zone.floorPadRenderer = padGo.GetComponent<Renderer>();

                var beaconGo = CreateBox("StageBeaconDiamond", stageGo.transform, new Vector3(0.4f, 6.2f, 0f), new Vector3(1.25f, 1.25f, 1.25f), matBeacon, false);
                beaconGo.transform.localRotation = Quaternion.Euler(45f, 45f, 45f);
                zone.beaconVisual = beaconGo.transform;

                var shelter = new GameObject("EnaCoachShelter").transform;
                shelter.SetParent(stageGo.transform, false);
                shelter.localPosition = new Vector3(-5.6f, 0.16f, 0f);
                CreateBox("BackWall", shelter, new Vector3(-1.1f, 1.45f, 0f), new Vector3(0.14f, 2.7f, 8.6f), matShelterFrame, true);
                CreateBox("PostFL", shelter, new Vector3(0.95f, 1.45f, -3.9f), new Vector3(0.16f, 2.9f, 0.16f), matShelterFrame, true);
                CreateBox("PostFR", shelter, new Vector3(0.95f, 1.45f,  3.9f), new Vector3(0.16f, 2.9f, 0.16f), matShelterFrame, true);
                var roof = CreateBox("ShelterRoof", shelter, new Vector3(0f, 3.02f, 0f), new Vector3(2.95f, 0.20f, 9.2f), matShelterRoof, false);
                roof.transform.localRotation = Quaternion.Euler(0f, 0f, -6f);
                CreateBox("BenchSeat", shelter, new Vector3(-0.45f, 0.55f, 0f), new Vector3(0.65f, 0.14f, 6.0f), matBench, true);
                CreateWorldSignText("StageSignText", shelter, $"ENA COACH TERMINAL\n{hub.name.ToUpperInvariant()}", new Vector3(1.10f, 2.74f, 0f), new Vector2(7.6f, 0.90f), 2.7f, Palette.EnaBodyWhite, Quaternion.Euler(0f, -90f, 0f));

                // Confetti celebration burst
                var psGo = new GameObject("ConfettiBurst");
                psGo.transform.SetParent(stageGo.transform, false);
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

                // 3 waiting commuters at every town's ENA Coach Terminal!
                var commuters = new KenyaPedestrianAI[3];
                for (int c = 0; c < 3; c++)
                {
                    Vector3 localPos = new Vector3(-4.2f, 0.16f, -2.4f + c * 2.3f);
                    var ped = CreateArticulatedPedestrianLocal(stageGo.transform, $"Commuter_{h}_{c}", localPos, 90f, h * 5 + c);
                    ped.mode = KenyaPedestrianAI.BehaviorMode.BusStageWaiter;
                    ped.stageIndex = h;
                    commuters[c] = ped;
                }
                zone.waitingCommuters = commuters;

                // Add a night glow lamp above each regional ENA Coach terminal!
                var plGo = new GameObject($"TerminalNightLight_{h}");
                plGo.transform.SetParent(shelter, false);
                plGo.transform.localPosition = new Vector3(1.2f, 4.2f, 0f);
                var pl = plGo.AddComponent<Light>();
                pl.type = LightType.Point;
                pl.color = Palette.LampGlow;
                pl.range = 24f;
                pl.intensity = 0f;
                pl.enabled = false;
                pointLights.Add(pl);

                stages[h] = zone;
            }

            return stages;
        }

        private static void BuildKenyaNationalLandmarksAndGantries(Transform parent)
        {
            var root = new GameObject("Kenya_Landmarks_And_KeNHA_Gantries").transform;
            root.SetParent(parent, false);

            var matSteel = GetMat("GantrySteel", Palette.LampPostMetal, 0.55f, 0.78f);
            var matKenhaGreen = GetMat("KenhaGreen", Palette.Hex("#0E6B3A"), 0.42f);
            var matSafeGreen = GetMat("SafaricomGreen", Palette.Hex("#28A745"), 0.45f);
            var matEnaRed = GetMat("BillboardEnaRed", Palette.EnaSkirtRed, 0.45f);
            var matWhite = GetMat("SignBorderWhite", Palette.EnaBodyWhite, 0.35f);
            var matIvory = GetMat("MombasaTusksIvory", Palette.Hex("#F5F1E6"), 0.45f, 0.08f);
            var matBlack = GetMat("KenyaFlagBlack", Palette.EnaStripeBlack, 0.35f);

            // 1. Nairobi Interchange Roundabout Monument Spire (0, 160) & Kisii Junction Spire (-1160, 260)
            foreach (var rb in KenyaNetworkData.Roundabouts)
            {
                var mon = new GameObject($"Monument_{rb.name}").transform;
                mon.SetParent(root, false);
                mon.position = new Vector3(rb.center.x, 0.35f, rb.center.y);
                CreateBox("Pedestal", mon, new Vector3(0f, 1.0f, 0f), new Vector3(3.8f, 2.0f, 3.8f), matBlack, true);
                CreateBox("RedBand", mon, new Vector3(0f, 3.4f, 0f), new Vector3(2.1f, 2.8f, 2.1f), matEnaRed, false);
                CreateBox("GreenBand", mon, new Vector3(0f, 6.2f, 0f), new Vector3(1.5f, 2.8f, 1.5f), matKenhaGreen, false);
                CreateSphere("TopGlobe", mon, new Vector3(0f, 8.3f, 0f), Vector3.one * 1.9f, matWhite);
            }

            // 2. KeNHA Green Overhead Directional Highway Gantries across Kenya!
            BuildKenhaGantry(root, new Vector3(0f, 0f, 122f), 0f,
                "NAIROBI INTERCHANGE  (KEEP LEFT)\n◄ NAROK / KISII (B3)   |   ▲ NAKURU / KISUMU (A104)   |   ► VOI / MOMBASA (A109)", matSteel, matKenhaGreen, matWhite);

            BuildKenhaGantry(root, new Vector3(-280f, 0f, 168f), -90f,
                "MAI MAHIU  •  GREAT RIFT VALLEY VIEWPOINT\nNAROK 30 KM   •   BOMET 60 KM   •   KISII 90 KM", matSteel, matKenhaGreen, matWhite);

            BuildKenhaGantry(root, new Vector3(-1045f, 0f, 245f), -78f,
                "KISII JUNCTION ROUNDABOUT\n▲ KISUMU / OYUGIS (C19)   |   ◄ MIGORI / SIRARE BORDER (A1)", matSteel, matKenhaGreen, matWhite);

            BuildKenhaGantry(root, new Vector3(-1160f, 0f, -172f), 180f,
                "KENYA — TANZANIA BORDER  (SIRARE / ISEBANIA)\nKARIBU TANZANIA   •   ENA COACH EXPRESS TERMINUS", matSteel, matEnaRed, matWhite);

            BuildKenhaGantry(root, new Vector3(-565f, 0f, 574f), -90f,
                "KERICHO  •  TEA CAPITAL OF KENYA\nKISUMU LAKESIDE  •  LAKE VICTORIA AHEAD", matSteel, matKenhaGreen, matWhite);

            BuildKenhaGantry(root, new Vector3(345f, 0f, -20f), 124f,
                "A109 MOMBASA ROAD  •  TSAVO CORRIDOR\nVOI  •  MARIAKANI  •  MOMBASA COAST", matSteel, matKenhaGreen, matWhite);

            // 3. ICONIC MOMBASA TUSKS ARCH (Moi Avenue Twin Smoothly-Curved Crossed Ivory Tusks + Coastal Palms!)
            var tusks = new GameObject("Mombasa_Iconic_Tusks_Arch").transform;
            tusks.SetParent(root, false);
            tusks.position = new Vector3(662f, 0f, -210f);
            tusks.rotation = Quaternion.Euler(0f, 118f, 0f);

            const int tuskSegs = 8;
            for (int pair = -1; pair <= 1; pair += 2)
            {
                float zOff = pair * 1.15f;
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector2 p0 = new Vector2(side * 8.4f, 0.0f);
                    Vector2 p1 = new Vector2(side * 7.0f, 7.2f);
                    Vector2 p2 = new Vector2(-side * 1.3f, 12.0f);

                    for (int s = 0; s < tuskSegs; s++)
                    {
                        float t0 = (float)s / tuskSegs;
                        float t1 = (float)(s + 1) / tuskSegs;
                        Vector2 a = (1f - t0) * (1f - t0) * p0 + 2f * (1f - t0) * t0 * p1 + t0 * t0 * p2;
                        Vector2 b = (1f - t1) * (1f - t1) * p0 + 2f * (1f - t1) * t1 * p1 + t1 * t1 * p2;
                        Vector2 mid = (a + b) * 0.5f;
                        Vector2 d = b - a;
                        float segLen = d.magnitude + 0.24f;
                        float angleDeg = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg - 90f;
                        float w = Mathf.Lerp(1.02f, 0.24f, (t0 + t1) * 0.5f);

                        var seg = CreateBox($"Tusk_{pair}_{side}_{s}", tusks, new Vector3(mid.x, mid.y, zOff), new Vector3(w, segLen, w * 0.72f), matIvory, false);
                        seg.transform.localRotation = Quaternion.Euler(0f, 0f, angleDeg);
                    }
                }
            }
            // Crisp dark-slate Mombasa welcome plaque right below the crossed ivory tips
            CreateBox("MombasaSignBacking", tusks, new Vector3(0f, 8.7f, -1.55f), new Vector3(9.4f, 1.25f, 0.14f), matBlack, false);
            CreateWorldSignText("MombasaWelcomeText", tusks, "KARIBU MOMBASA  •  MOI AVENUE", new Vector3(0f, 8.7f, -1.66f), new Vector2(9.1f, 1.1f), 3.6f, Palette.UiAccentAmber, Quaternion.identity, noWrap: true);

            // Lush Coastal Palm Boulevard flanking Mombasa Moi Avenue right beside the Iconic Tusks!
            var matPalmBark = GetMat("AcaciaBarkPBR", Color.white, 0.10f, 0f, null, texBarkAlb, texBarkNrm, new Vector2(1f, 3f), 1.35f);
            var matPalmLeaf = GetMat("AcaciaLeafAlpha", Color.white, 0.12f, 0f, null, texLeafCluster, null, Vector2.one, 1f, alphaClipDoubleSided: true);
            float[] palmZs = { -20f, -9f, 11f, 24f, 52f };
            for (int pz = 0; pz < palmZs.Length; pz++)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    var pTr = new GameObject($"MombasaAvePalm_{pz}_{side}").transform;
                    pTr.SetParent(tusks, false);
                    pTr.localPosition = new Vector3(side * 10.6f, -0.05f, palmZs[pz]);
                    pTr.localRotation = Quaternion.Euler(0f, (pz * 73 + side * 40) % 360, 0f);
                    float scl = 1.05f + (pz % 2) * 0.18f;
                    CreateCustomMesh("Trunk", pTr, Vector3.zero, Quaternion.identity, Vector3.one * scl, KenyaSculptedMeshFactory.PalmTrunkMesh, matPalmBark, false);
                    CreateCustomMesh("Crown", pTr, Vector3.zero, Quaternion.identity, Vector3.one * scl, KenyaSculptedMeshFactory.PalmCrownMesh, matPalmLeaf, false);
                }
            }

            // 4. Kenyan Highway Billboards
            BuildBillboard(root, new Vector3(13.5f, 0.16f, 26f), -16f, "SAFARICOM 5G  •  M-PESA\nTUWAWEZESHE KENYA", matSafeGreen, matSteel, matWhite);
            BuildBillboard(root, new Vector3(-420f, 0.16f, 160f), -90f, "ENA COACH  EXPRESS\nNAIROBI • KISII • KISUMU • MOMBASA", matEnaRed, matSteel, matWhite);
        }

        private static void BuildKenhaGantry(Transform parent, Vector3 pos, float yawDeg, string text, Material matSteel, Material matBoard, Material matWhite)
        {
            var tr = new GameObject($"KenhaGantry_{pos.x:0}_{pos.z:0}").transform;
            tr.SetParent(parent, false);
            tr.position = pos;
            tr.rotation = Quaternion.Euler(0f, yawDeg, 0f);

            CreateCylinder("LegL", tr, new Vector3(-8.5f, 3.95f, 0f), new Vector3(0.36f, 3.95f, 0.36f), matSteel);
            CreateCylinder("LegR", tr, new Vector3( 8.5f, 3.95f, 0f), new Vector3(0.36f, 3.95f, 0.36f), matSteel);
            CreateBox("CrossTrussTop", tr, new Vector3(0f, 7.80f, 0f), new Vector3(17.6f, 0.36f, 0.42f), matSteel, false);
            CreateBox("CrossTrussBot", tr, new Vector3(0f, 6.80f, 0f), new Vector3(17.6f, 0.28f, 0.42f), matSteel, false);

            CreateBox("SignFrame", tr, new Vector3(0f, 7.35f, -0.26f), new Vector3(13.4f, 2.35f, 0.14f), matWhite, false);
            CreateBox("SignBoard", tr, new Vector3(0f, 7.35f, -0.30f), new Vector3(13.1f, 2.08f, 0.14f), matBoard, false);
            CreateWorldSignText("SignText", tr, text, new Vector3(0f, 7.35f, -0.40f), new Vector2(12.6f, 1.95f), 3.6f, Palette.EnaBodyWhite, Quaternion.identity, noWrap: false);
        }

        private static void BuildHighwayBiomeFoliage(Transform parent)
        {
            var root = new GameObject("Kenya_Biome_Acacia_And_Palm_Trees").transform;
            root.SetParent(parent, false);

            var matBark = GetMat("AcaciaBarkPBR", Color.white, 0.10f, 0f, null, texBarkAlb, texBarkNrm, new Vector2(1f, 3f), 1.35f);
            var matLeaf = GetMat("AcaciaLeafAlpha", Color.white, 0.12f, 0f, null, texLeafCluster, null, Vector2.one, 1f, alphaClipDoubleSided: true);
            var rng = new System.Random(254777);

            // Populate branching Kenyan Acacia & Coastal/Lakeside Palm trees smoothly along every highway corridor!
            for (int h = 1; h < KenyaNetworkData.Highways.Length; h++)
            {
                var pts = KenyaNetworkData.SampleHighway(KenyaNetworkData.Highways[h], 32f);
                for (int i = 1; i < pts.Count - 1; i++)
                {
                    Vector3 p = pts[i];
                    if (IsNearAnyHubOrRoundabout(new Vector2(p.x, p.z), 48f)) continue;

                    Vector3 fwd = (pts[i + 1] - pts[i - 1]).normalized;
                    Vector3 right = Vector3.Cross(Vector3.up, fwd).normalized;

                    bool isPalmRegion = (p.x > 540f && p.z < -120f) || (p.x < -880f && p.z > 500f); // Mombasa Coast or Lake Victoria Kisumu!

                    for (int side = -1; side <= 1; side += 2)
                    {
                        if (rng.NextDouble() < 0.25) continue;
                        float offset = 13.5f + (float)rng.NextDouble() * 16f;
                        Vector3 treePos = p + right * (side * offset);
                        treePos.y = -0.05f;
                        float yaw = (float)(rng.NextDouble() * 360.0);
                        float scl = 0.95f + (float)rng.NextDouble() * 0.45f;

                        var tr = new GameObject(isPalmRegion ? $"KenyaPalm_{h}_{i}_{side}" : $"KenyaAcacia_{h}_{i}_{side}").transform;
                        tr.SetParent(root, false);
                        tr.position = treePos;

                        if (isPalmRegion)
                        {
                            CreateCustomMesh("PalmTrunk", tr, Vector3.zero, Quaternion.Euler(0f, yaw, 0f), Vector3.one * scl, KenyaSculptedMeshFactory.PalmTrunkMesh, matBark, false);
                            CreateCustomMesh("PalmCrown", tr, Vector3.zero, Quaternion.Euler(0f, yaw, 0f), Vector3.one * scl, KenyaSculptedMeshFactory.PalmCrownMesh, matLeaf, false);
                        }
                        else
                        {
                            CreateCustomMesh("AcaciaTrunk", tr, Vector3.zero, Quaternion.Euler(0f, yaw, 0f), Vector3.one * scl, KenyaSculptedMeshFactory.TreeTrunkMesh, matBark, false);
                            CreateCustomMesh("AcaciaCrown", tr, Vector3.zero, Quaternion.Euler(0f, yaw, 0f), Vector3.one * scl, KenyaSculptedMeshFactory.TreeFoliageMesh, matLeaf, false);
                        }
                    }
                }
            }
        }

        private static bool IsNearAnyHubOrRoundabout(Vector2 xz, float radius)
        {
            for (int i = 0; i < KenyaNetworkData.Hubs.Length; i++)
            {
                if (Vector2.Distance(xz, KenyaNetworkData.Hubs[i].xz) < radius) return true;
            }
            for (int r = 0; r < KenyaNetworkData.Roundabouts.Length; r++)
            {
                if (Vector2.Distance(xz, KenyaNetworkData.Roundabouts[r].center) < radius) return true;
            }
            return false;
        }

        private static void BuildFullKenyaSplineTraffic(Transform parent)
        {
            var root = new GameObject("Kenya_Highway_LHT_Traffic").transform;
            root.SetParent(parent, false);

            var matTire = GetMat("TireRubberPBR", Color.white, 0.22f, 0f, null, texTireAlb, texTireNrm, new Vector2(1f, 3f), 1.2f);
            var matRim = GetMat("WheelRimAlloy", Palette.WheelRimAlloy, 0.85f, 0.85f);
            var matGlass = GetMat("EnaGlassDark", Palette.EnaGlassDark, 0.94f, 0.30f);
            var matHeadlight = GetMat("HeadlightWarm", Palette.HeadlightWarm, 0.85f, 0f, Palette.HeadlightWarm * 2.6f);
            var matTail = GetMat("TaillightRed", Palette.TaillightRed, 0.65f, 0f, Palette.TaillightRed * 1.6f);

            // Spawn sculpted LHT passing Kenyan Matatus, Sedans/Probox & Freight Lorries across ALL 6 highways!
            int[] perDirectionPerHighway = { 2, 5, 3, 5, 2, 4 }; // 42 vehicles total across Kenya!
            int seed = 1;

            for (int h = 0; h < KenyaNetworkData.Highways.Length; h++)
            {
                int count = perDirectionPerHighway[h];
                for (int dir = 0; dir < 2; dir++)
                {
                    bool fwd = (dir == 0);
                    for (int k = 0; k < count; k++)
                    {
                        float progress = (k + 0.35f) / count;
                        if (h == 0 && fwd && progress < 0.32f) progress = 0.45f; // keep clear of player start in Nairobi

                        int kind = (seed++) % 3;
                        var ai = CreateSplineTrafficVehicle(root, $"KenyaTraffic_H{h}_{(fwd ? "F" : "R")}_{k}", kind, seed, matTire, matRim, matGlass, matHeadlight, matTail);
                        ai.highwayIndex = h;
                        ai.forwardDirection = fwd;
                        ai.startNormalizedProgress = progress;
                        ai.laneOffsetMeters = 3.4f;
                        ai.cruiseSpeed = (h == 0 ? 10.5f : 14.5f) + (seed % 4) * 1.4f;
                    }
                }
            }
        }

        private static KenyaSplineTraffic CreateSplineTrafficVehicle(
            Transform parent, string name, int kind, int colorSeed,
            Material matTire, Material matRim, Material matGlass, Material matHeadlight, Material matTail)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var ai = go.AddComponent<KenyaSplineTraffic>();
            var col = go.AddComponent<BoxCollider>();

            Transform[] wheels;
            if (kind == 0)
            {
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
                CreateBox("HeadR", go.transform, new Vector3( 0.66f, 0.80f, 2.52f), new Vector3(0.32f, 0.18f, 0.08f), matHeadlight, false);
                CreateBox("TailL", go.transform, new Vector3(-0.72f, 0.90f, -2.54f), new Vector3(0.24f, 0.42f, 0.08f), matTail, false);
                CreateBox("TailR", go.transform, new Vector3( 0.72f, 0.90f, -2.54f), new Vector3(0.24f, 0.42f, 0.08f), matTail, false);

                wheels = CreateTrafficWheelSet(go.transform, 0.96f, 1.52f, -1.48f, 0.36f, matTire, matRim);
                col.center = new Vector3(0f, 1.15f, 0f);
                col.size = new Vector3(1.98f, 2.0f, 5.1f);
                ai.vehicleHalfLength = 2.68f;
                ai.wheelRadius = 0.36f;
            }
            else if (kind == 1)
            {
                Color[] carColors = { Palette.CarSilver, Palette.CarMaroon, Palette.CarBlue, Palette.CarGreen };
                Color c = carColors[colorSeed % carColors.Length];
                var matCar = GetMat($"CarMetal_{ColorUtility.ToHtmlStringRGB(c)}", c, 0.82f, 0.38f);

                CreateCustomMesh("SculptedSedanShell", go.transform, Vector3.zero, Quaternion.identity, Vector3.one, KenyaSculptedMeshFactory.SedanBodyMesh, matCar, false);
                CreateBox("CabinGlass", go.transform, new Vector3(0f, 1.24f, -0.35f), new Vector3(1.76f, 0.44f, 1.95f), matGlass, false);
                CreateBox("HeadL", go.transform, new Vector3(-0.62f, 0.66f, 2.22f), new Vector3(0.32f, 0.16f, 0.08f), matHeadlight, false);
                CreateBox("HeadR", go.transform, new Vector3( 0.62f, 0.66f, 2.22f), new Vector3(0.32f, 0.16f, 0.08f), matHeadlight, false);
                CreateBox("TailL", go.transform, new Vector3(-0.62f, 0.74f, -2.22f), new Vector3(0.30f, 0.18f, 0.08f), matTail, false);
                CreateBox("TailR", go.transform, new Vector3( 0.62f, 0.74f, -2.22f), new Vector3(0.30f, 0.18f, 0.08f), matTail, false);

                wheels = CreateTrafficWheelSet(go.transform, 0.90f, 1.32f, -1.32f, 0.34f, matTire, matRim);
                col.center = new Vector3(0f, 0.88f, 0f);
                col.size = new Vector3(1.86f, 1.48f, 4.45f);
                ai.vehicleHalfLength = 2.32f;
                ai.wheelRadius = 0.34f;
            }
            else
            {
                var matCab = GetMat("TruckCabGloss", Palette.TruckCabWhite, 0.75f, 0.2f);
                var matBox = GetMat("TruckCargoMabati", Palette.TruckCargoBlue, 0.42f, 0.32f, null, texMabatiAlb, texMabatiNrm, new Vector2(4f, 2f), 1.15f);

                CreateBox("Cab", go.transform, new Vector3(0f, 1.45f, 2.15f), new Vector3(2.12f, 1.85f, 1.95f), matCab, false);
                CreateBox("CabGlass", go.transform, new Vector3(0f, 1.78f, 3.12f), new Vector3(1.92f, 0.78f, 0.10f), matGlass, false);
                CreateBox("CargoBox", go.transform, new Vector3(0f, 1.88f, -0.95f), new Vector3(2.24f, 2.35f, 4.15f), matBox, false);
                CreateBox("HeadL", go.transform, new Vector3(-0.78f, 0.85f, 3.14f), new Vector3(0.32f, 0.20f, 0.08f), matHeadlight, false);
                CreateBox("HeadR", go.transform, new Vector3( 0.78f, 0.85f, 3.14f), new Vector3(0.32f, 0.20f, 0.08f), matHeadlight, false);
                CreateBox("TailL", go.transform, new Vector3(-0.85f, 0.95f, -3.04f), new Vector3(0.32f, 0.20f, 0.08f), matTail, false);
                CreateBox("TailR", go.transform, new Vector3( 0.85f, 0.95f, -3.04f), new Vector3(0.32f, 0.20f, 0.08f), matTail, false);

                wheels = CreateTrafficWheelSet(go.transform, 1.05f, 2.15f, -1.55f, 0.44f, matTire, matRim);
                col.center = new Vector3(0f, 1.55f, 0.05f);
                col.size = new Vector3(2.26f, 2.85f, 6.25f);
                ai.vehicleHalfLength = 3.25f;
                ai.wheelRadius = 0.44f;
            }

            ai.wheelVisuals = wheels;
            return ai;
        }

        private static void BuildNairobiPedestrians(Transform parent, StreetTrafficLight nairobiLight)
        {
            var root = new GameObject("Nairobi_Walking_Pedestrians").transform;
            root.SetParent(parent, false);
            int seed = 300;
            float[] startZs = { -40f, -10f, 20f, 68f, 102f };
            for (int i = 0; i < startZs.Length; i++)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    float xWalk = side * (8.7f + (i % 2) * 1.1f);
                    var ped = CreateArticulatedPedestrianLocal(root, $"NairobiWalker_{i}_{side}", new Vector3(xWalk, 0.16f, startZs[i]), 0f, seed++);
                    ped.mode = KenyaPedestrianAI.BehaviorMode.SidewalkPatrol;
                    ped.pointA = new Vector3(xWalk, 0.16f, startZs[i]);
                    ped.pointB = new Vector3(xWalk, 0.16f, startZs[i] + 28f);
                    ped.walkSpeed = 1.35f + (seed % 3) * 0.18f;
                }
            }

            // 2 Zebra Crossers at Nairobi CBD Plaza light
            var z1 = CreateArticulatedPedestrianLocal(root, "NairobiZebraCrosser_1", new Vector3(-8.2f, 0.16f, 95f), 90f, seed++);
            z1.mode = KenyaPedestrianAI.BehaviorMode.ZebraCrossing;
            z1.pointA = new Vector3(-8.2f, 0.16f, 95f);
            z1.pointB = new Vector3( 8.2f, 0.16f, 95f);
            z1.crossingLight = nairobiLight;

            var z2 = CreateArticulatedPedestrianLocal(root, "NairobiZebraCrosser_2", new Vector3(8.2f, 0.16f, 96.4f), -90f, seed++);
            z2.mode = KenyaPedestrianAI.BehaviorMode.ZebraCrossing;
            z2.pointA = new Vector3( 8.2f, 0.16f, 96.4f);
            z2.pointB = new Vector3(-8.2f, 0.16f, 96.4f);
            z2.crossingLight = nairobiLight;
        }

        private static void BuildRubisPetrolStation(Transform parent, Vector3 pos, float yawDeg, string label, List<Light> pointLights, List<Renderer> glowRenderers)
        {
            var root = new GameObject($"RubisStation_{label}").transform;
            root.SetParent(parent, false);
            root.position = pos;
            root.rotation = Quaternion.Euler(0f, yawDeg, 0f);

            var matConcrete = GetMat("StationConcrete", Palette.Sidewalk, 0.25f, 0f, null, texCabroAlb, texCabroNrm, new Vector2(6f, 8f), 1.0f);
            var matGreen = GetMat("RubisGreen", Palette.Hex("#0F8A4B"), 0.55f, 0.15f);
            var matRed = GetMat("RubisRed", Palette.Hex("#D4262C"), 0.55f, 0.15f);
            var matWhite = GetMat("RubisWhite", Palette.EnaBodyWhite, 0.55f);
            var matDark = GetMat("StationDark", Palette.SignalHousing, 0.4f, 0.4f);
            var matGlow = GetMat("CanopyLightGlow", Palette.LampGlow, 0.7f, 0f, Palette.LampGlow * 2.8f);
            var matGlass = GetMat("StationGlass", Color.white, 0.9f, 0.2f, null, texGlassAlb, texGlassNrm, new Vector2(4f, 1f), 0.9f);

            CreateBox("ForecourtPad", root, new Vector3(-2f, 0.02f, 0f), new Vector3(20f, 0.08f, 30f), matConcrete, true);
            CreateBox("CanopyTopWhite", root, new Vector3(-1.5f, 5.45f, 0f), new Vector3(11.5f, 0.45f, 19f), matWhite, false);
            CreateBox("CanopyFasciaGreen", root, new Vector3(-1.5f, 5.80f, 0f), new Vector3(11.7f, 0.52f, 19.2f), matGreen, false);
            CreateBox("CanopyStripeRed", root, new Vector3(-1.5f, 5.38f, 0f), new Vector3(11.75f, 0.20f, 19.25f), matRed, false);
            CreateWorldSignText("CanopyBrandSign", root, $"{label}  •  RUBIS ENERGY", new Vector3(-7.42f, 5.82f, 0f), new Vector2(17.5f, 0.85f), 4.4f, Palette.EnaBodyWhite, Quaternion.Euler(0f, 90f, 0f), noWrap: true);

            float[] colZ = { -6.0f, 6.0f };
            float[] colX = { -4.5f, 1.5f };
            foreach (float cx in colX)
                foreach (float cz in colZ)
                    CreateBox($"Pillar_{cx}_{cz}", root, new Vector3(cx, 2.7f, cz), new Vector3(0.55f, 5.4f, 0.55f), matWhite, true);

            for (int i = -1; i <= 1; i += 2)
            {
                float iz = i * 4.2f;
                CreateBox($"PumpIsland_{i}", root, new Vector3(-1.5f, 0.18f, iz), new Vector3(1.6f, 0.24f, 4.0f), matDark, true);
                CreateBox($"PumpA_{i}", root, new Vector3(-1.5f, 1.15f, iz - 1.0f), new Vector3(0.85f, 1.75f, 0.65f), matGreen, true);
                CreateBox($"PumpB_{i}", root, new Vector3(-1.5f, 1.15f, iz + 1.0f), new Vector3(0.85f, 1.75f, 0.65f), matRed, true);

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

            CreateBox("ShopBuilding", root, new Vector3(7.2f, 2.2f, 0f), new Vector3(6.5f, 4.4f, 19f), matWhite, true);
            CreateBox("ShopBandGreen", root, new Vector3(7.2f, 4.15f, 0f), new Vector3(6.6f, 0.65f, 19.1f), matGreen, false);
            CreateBox("ShopGlassFront", root, new Vector3(3.92f, 1.75f, 0f), new Vector3(0.14f, 2.7f, 14.5f), matGlass, false);
            CreateBox("PriceTotemPole", root, new Vector3(-9.5f, 3.8f, -12.5f), new Vector3(1.55f, 7.6f, 0.45f), matDark, true);
            CreateBox("PriceTotemTop", root, new Vector3(-9.5f, 6.85f, -12.5f), new Vector3(1.75f, 1.5f, 0.52f), matGreen, false);
            CreateWorldSignText("TotemPriceText", root, "RUBIS\nSUPER 180.50\nDIESEL 168.20", new Vector3(-9.5f, 5.1f, -12.78f), new Vector2(1.65f, 3.6f), 2.1f, Palette.UiAccentAmber, Quaternion.identity, noWrap: false);
        }

        private static void BuildKplcPowerStretch(Transform parent, Vector3 start, Vector3 end, int poleCount)
        {
            var matWoodPole = GetMat("KplcWoodPole", Palette.Hex("#5B4330"), 0.18f, 0f, null, texBarkAlb, texBarkNrm, new Vector2(1f, 4f), 1.1f);
            var matCrossarm = GetMat("KplcSteel", Palette.LampPostMetal, 0.5f, 0.65f);
            var matWire = GetMat("PowerCableBlack", Palette.SignalHousing, 0.2f);

            var positions = new Vector3[poleCount];
            for (int i = 0; i < poleCount; i++)
            {
                Vector3 p = Vector3.Lerp(start, end, (float)i / Mathf.Max(1, poleCount - 1));
                positions[i] = p;
                var pole = new GameObject($"KPLC_Pole_{i}").transform;
                pole.SetParent(parent, false);
                pole.position = p;
                CreateCylinder("PoleShaft", pole, new Vector3(0f, 4.9f, 0f), new Vector3(0.24f, 4.9f, 0.24f), matWoodPole);
                CreateBox("CrossarmTop", pole, new Vector3(0f, 9.2f, 0f), new Vector3(2.2f, 0.14f, 0.14f), matCrossarm, false);
            }

            for (int i = 0; i < poleCount - 1; i++)
            {
                float[] oxs = { -0.85f, 0f, 0.85f };
                foreach (float ox in oxs)
                {
                    var go = new GameObject($"KPLC_Wire_{i}_{ox:0.0}");
                    go.transform.SetParent(parent, false);
                    var lr = go.AddComponent<LineRenderer>();
                    lr.sharedMaterial = matWire;
                    lr.startWidth = 0.045f;
                    lr.endWidth = 0.045f;
                    lr.useWorldSpace = true;
                    lr.shadowCastingMode = ShadowCastingMode.Off;
                    lr.receiveShadows = false;
                    const int pts = 9;
                    lr.positionCount = pts;
                    Vector3 a = positions[i] + new Vector3(ox, 9.2f, 0f);
                    Vector3 b = positions[i + 1] + new Vector3(ox, 9.2f, 0f);
                    for (int k = 0; k < pts; k++)
                    {
                        float t = (float)k / (pts - 1);
                        Vector3 wp = Vector3.Lerp(a, b, t);
                        wp.y -= Mathf.Sin(t * Mathf.PI) * 0.72f;
                        lr.SetPosition(k, wp);
                    }
                }
            }
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

        private static void BuildTrafficGantry(
            Transform parent, Vector3 worldPos, float yawDeg,
            Material matPole, Material matHousing, Material matLens,
            List<Renderer> redList, List<Renderer> yelList, List<Renderer> grnList)
        {
            var g = new GameObject("TrafficGantry").transform;
            g.SetParent(parent, true);
            g.position = worldPos;
            g.rotation = Quaternion.Euler(0f, yawDeg, 0f);
            CreateBox("Pole", g, new Vector3(0f, 3.1f, 0f), new Vector3(0.24f, 6.2f, 0.24f), matPole, true);
            CreateBox("Arm", g, new Vector3(-2.4f, 5.9f, 0f), new Vector3(5.0f, 0.20f, 0.20f), matPole, false);
            BuildSignalHead(g, new Vector3(-4.2f, 5.35f, -0.18f), matHousing, matLens, redList, yelList, grnList);
            BuildSignalHead(g, new Vector3(-0.3f, 3.4f, -0.18f), matHousing, matLens, redList, yelList, grnList);
        }

        private static void BuildSignalHead(
            Transform parent, Vector3 localPos, Material matHousing, Material matLens,
            List<Renderer> redList, List<Renderer> yelList, List<Renderer> grnList)
        {
            var head = new GameObject("SignalHead").transform;
            head.SetParent(parent, false);
            head.localPosition = localPos;
            CreateBox("Box", head, Vector3.zero, new Vector3(0.46f, 1.24f, 0.34f), matHousing, false);
            redList.Add(CreateSphere("LensRed", head, new Vector3(0f, 0.36f, -0.16f), Vector3.one * 0.28f, matLens));
            yelList.Add(CreateSphere("LensYel", head, new Vector3(0f, 0.0f, -0.16f), Vector3.one * 0.28f, matLens));
            grnList.Add(CreateSphere("LensGrn", head, new Vector3(0f, -0.36f, -0.16f), Vector3.one * 0.28f, matLens));
        }

        private static void BuildKenyanShopBuilding(
            Transform parent, Vector3 localPos, float yawDeg,
            float widthFront, float depth, int stories,
            Material facadeMat, Material awningMat,
            Material matTrim, Material matGlass, Material matShopGlass, Material matFrame,
            Material matRoofMabati, Material matSign, Material matTank, Material matRail,
            string shopName, bool hasWaterTank)
        {
            var b = new GameObject($"Shop_{shopName}").transform;
            b.SetParent(parent, false);
            b.localPosition = localPos;
            b.localRotation = Quaternion.Euler(0f, yawDeg, 0f);

            float groundH = 3.9f;
            float floorH = 3.2f;
            float totalH = groundH + (stories - 1) * floorH;
            float frontZ = depth * 0.5f;

            CreateBox("MainBlock", b, new Vector3(0f, totalH * 0.5f, 0f), new Vector3(widthFront, totalH, depth), facadeMat, true);
            CreateBox("GroundPlinth", b, new Vector3(0f, 0.35f, frontZ + 0.04f), new Vector3(widthFront + 0.08f, 0.7f, 0.16f), matTrim, false);
            CreateBox("ShopGlass", b, new Vector3(0f, 1.80f, frontZ + 0.05f), new Vector3(widthFront * 0.84f, 2.25f, 0.12f), matShopGlass, false);
            CreateBox("ShopDoorFrame", b, new Vector3(0f, 1.38f, frontZ + 0.09f), new Vector3(1.85f, 2.55f, 0.12f), matFrame, false);

            var awning = CreateBox("ShopAwningMabati", b, new Vector3(0f, 3.08f, frontZ + 1.02f), new Vector3(widthFront * 0.94f, 0.16f, 2.08f), awningMat, false);
            awning.transform.localRotation = Quaternion.Euler(14f, 0f, 0f);

            CreateBox("ShopSignBoard", b, new Vector3(0f, 3.74f, frontZ + 0.16f), new Vector3(widthFront * 0.88f, 0.80f, 0.18f), matSign, false);
            CreateWorldSignText("ShopSignText", b, shopName, new Vector3(0f, 3.74f, frontZ + 0.27f), new Vector2(widthFront * 0.84f, 0.68f), 3.2f, Palette.UiAccentAmber);

            for (int f = 1; f < stories; f++)
            {
                float fy = groundH + (f - 1) * floorH;
                CreateBox($"Cornice_{f}", b, new Vector3(0f, fy + 0.12f, frontZ + 0.28f), new Vector3(widthFront + 0.35f, 0.24f, 0.65f), matTrim, false);
                if (f == 1) CreateBox($"BalconyRail_{f}", b, new Vector3(0f, fy + 0.92f, frontZ + 0.56f), new Vector3(widthFront * 0.92f, 0.07f, 0.07f), matRail, false);

                int winCount = Mathf.Max(2, Mathf.FloorToInt(widthFront / 3.4f));
                float step = (widthFront - 2.0f) / winCount;
                for (int w = 0; w < winCount; w++)
                {
                    float wx = -(widthFront - 2.0f) * 0.5f + step * (w + 0.5f);
                    CreateBox($"WinFrame_{f}_{w}", b, new Vector3(wx, fy + 1.65f, frontZ + 0.05f), new Vector3(1.68f, 1.58f, 0.14f), matFrame, false);
                    CreateBox($"WinGlass_{f}_{w}", b, new Vector3(wx, fy + 1.65f, frontZ + 0.09f), new Vector3(1.42f, 1.32f, 0.10f), matGlass, false);
                }
            }

            CreateBox("RoofParapet", b, new Vector3(0f, totalH + 0.36f, 0f), new Vector3(widthFront + 0.26f, 0.72f, depth + 0.26f), matRoofMabati, false);
            if (hasWaterTank)
            {
                float tx = (widthFront * 0.22f) * (yawDeg > 0 ? 1f : -1f);
                CreateBox("TankStand", b, new Vector3(tx, totalH + 0.95f, -depth * 0.15f), new Vector3(1.8f, 0.7f, 1.8f), matTrim, false);
                CreateCylinder("WaterTank", b, new Vector3(tx, totalH + 2.25f, -depth * 0.15f), new Vector3(1.85f, 1.05f, 1.85f), matTank);
            }
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

        private static KenyaPedestrianAI CreateArticulatedPedestrianLocal(Transform parent, string name, Vector3 localPos, float localYawDeg, int seed)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(0f, localYawDeg, 0f);

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
            rb.linearDamping = 0.03f;
            rb.angularDamping = 0.35f;
#else
            rb.drag = 0.03f;
            rb.angularDrag = 0.35f;
#endif

            var ctrl = root.AddComponent<KenyaStreetVehicle>();
            ctrl.displayName = "ENA Coach KDK 057M";
            ctrl.plateNumber = "KDK 057M";
            ctrl.isCoach = true;
            ctrl.motorTorque = 3400f;
            ctrl.brakeTorque = 6200f;
            ctrl.maxSpeedKmh = 105f;
            ctrl.centerOfMassOffset = new Vector3(0f, -0.58f, 0.15f);

            var chassisCol = root.AddComponent<BoxCollider>();
            chassisCol.center = new Vector3(0f, 1.98f, 0f);
            chassisCol.size = new Vector3(2.48f, 2.68f, 11.55f);

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

            CreateCustomMesh("SculptedUpperBody", body, Vector3.zero, Quaternion.identity, Vector3.one, KenyaSculptedMeshFactory.EnaCoachUpperMesh, matWhite, false);
            CreateCustomMesh("SculptedLowerSkirt", body, Vector3.zero, Quaternion.identity, Vector3.one, KenyaSculptedMeshFactory.EnaCoachSkirtMesh, matRed, false);
            CreateCustomMesh("SculptedRoofBrow", body, Vector3.zero, Quaternion.identity, Vector3.one, KenyaSculptedMeshFactory.EnaCoachWindshieldMesh, matWhite, false);

            var matClearWindshield = GetTransparentGlassMat("EnaPanoramicWindshield", Palette.WithAlpha(Palette.EnaGlassDark, 0.24f));
            var frontGlass = CreateBox("PanoramicWindshieldGlass", body, new Vector3(0f, 2.26f, 5.76f), new Vector3(2.26f, 1.28f, 0.04f), matClearWindshield, false);
            frontGlass.transform.localRotation = Quaternion.Euler(-11f, 0f, 0f);
            CreateBox("CockpitGlassL", body, new Vector3(-1.25f, 2.26f, 5.18f), new Vector3(0.04f, 1.24f, 1.08f), matClearWindshield, false);
            CreateBox("CockpitGlassR", body, new Vector3( 1.25f, 2.26f, 5.18f), new Vector3(0.04f, 1.24f, 1.08f), matClearWindshield, false);

            CreateBox("AccentStripeBlack", body, new Vector3(0f, 1.57f, -0.04f), new Vector3(2.58f, 0.13f, 11.45f), matBlack, false);
            CreateBox("RoofACPod", body, new Vector3(0f, 3.56f, 0.35f), new Vector3(1.68f, 0.28f, 3.65f), matWhite, false);
            CreateBox("RoofACGrille", body, new Vector3(0f, 3.58f, 0.35f), new Vector3(1.42f, 0.30f, 2.85f), matBlack, false);

            var brow = CreateBox("FrontBrowBlack", body, new Vector3(0f, 3.04f, 5.56f), new Vector3(2.38f, 0.48f, 0.18f), matBlack, false);
            brow.transform.localRotation = Quaternion.Euler(-16f, 0f, 0f);
            CreateSphere("EnaOvalLogo", body, new Vector3(-0.82f, 3.06f, 5.69f), new Vector3(0.40f, 0.25f, 0.08f), matOrange);
            CreateWorldSignText("FrontEnaHeader", body, "ENA COACH", new Vector3(0.22f, 3.06f, 5.70f), new Vector2(1.85f, 0.40f), 2.0f, Palette.EnaBodyWhite, Quaternion.Euler(-16f, 180f, 0f), noWrap: true);

            CreateBox("SideWindowsBand", body, new Vector3(0f, 2.42f, -0.35f), new Vector3(2.57f, 1.08f, 9.6f), matGlass, false);
            for (float pz = -4.2f; pz <= 3.9f; pz += 1.62f)
                CreateBox($"WindowPillar_{pz:0.0}", body, new Vector3(0f, 2.42f, pz), new Vector3(2.58f, 1.10f, 0.14f), matBlack, false);

            CreateWorldSignText("SideSignL", body, "ENA COACH  •  KDK 057M", new Vector3(-1.30f, 1.15f, 0.1f), new Vector2(8.0f, 0.65f), 4.2f, Palette.EnaBodyWhite, Quaternion.Euler(0f, 90f, 0f));
            CreateWorldSignText("SideSignR", body, "ENA COACH  •  KDK 057M", new Vector3( 1.30f, 1.15f, 0.1f), new Vector2(8.0f, 0.65f), 4.2f, Palette.EnaBodyWhite, Quaternion.Euler(0f, -90f, 0f));

            CreateBox("FrontGrille", body, new Vector3(0f, 1.14f, 5.93f), new Vector3(1.48f, 0.54f, 0.08f), matBlack, false);
            CreateBox("ChromeVBar1", body, new Vector3(0f, 1.28f, 5.98f), new Vector3(1.42f, 0.08f, 0.06f), matChrome, false);
            CreateBox("ChromeVBar2", body, new Vector3(0f, 1.08f, 5.98f), new Vector3(1.18f, 0.07f, 0.06f), matChrome, false);
            CreateBox("FrontPlate", body, new Vector3(0f, 0.72f, 5.96f), new Vector3(0.68f, 0.19f, 0.05f), matPlate, false);
            CreateWorldSignText("FrontPlateTxt", body, "KDK 057M", new Vector3(0f, 0.72f, 6.00f), new Vector2(0.66f, 0.18f), 1.3f, Palette.EnaStripeBlack, Quaternion.Euler(0f, 180f, 0f), noWrap: true);

            var headL = CreateBox("HeadlightL", body, new Vector3(-0.92f, 0.96f, 5.92f), new Vector3(0.38f, 0.20f, 0.08f), matHead, false);
            var headR = CreateBox("HeadlightR", body, new Vector3( 0.92f, 0.96f, 5.92f), new Vector3(0.38f, 0.20f, 0.08f), matHead, false);
            var indFL = CreateBox("IndFrontL", body, new Vector3(-1.14f, 0.96f, 5.86f), new Vector3(0.16f, 0.20f, 0.10f), matIndicator, false);
            var indFR = CreateBox("IndFrontR", body, new Vector3( 1.14f, 0.96f, 5.86f), new Vector3(0.16f, 0.20f, 0.10f), matIndicator, false);

            var spotL = CreateHeadlightSpot(root.transform, "HeadlightBeamL", new Vector3(-0.92f, 0.96f, 6.05f));
            var spotR = CreateHeadlightSpot(root.transform, "HeadlightBeamR", new Vector3( 0.92f, 0.96f, 6.05f));
            ctrl.headlightSpots = new[] { spotL, spotR };
            ctrl.headlightRenderers = new[] { headL.GetComponent<Renderer>(), headR.GetComponent<Renderer>() };

            for (int side = -1; side <= 1; side += 2)
            {
                CreateBox($"MirrorArm_{side}", body, new Vector3(side * 1.36f, 2.54f, 5.92f), new Vector3(0.34f, 0.07f, 0.46f), matWhite, false);
                CreateBox($"MirrorHead_{side}", body, new Vector3(side * 1.52f, 2.26f, 6.12f), new Vector3(0.14f, 0.48f, 0.22f), matWhite, false);
                CreateBox($"MirrorGlass_{side}", body, new Vector3(side * 1.52f, 2.26f, 6.00f), new Vector3(0.12f, 0.42f, 0.03f), matChrome, false);
            }

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

            var cockpitCam = new GameObject("CockpitCameraAnchor").transform;
            cockpitCam.SetParent(root.transform, false);
            cockpitCam.localPosition = new Vector3(0.68f, 2.38f, 4.78f);
            cockpitCam.localRotation = Quaternion.Euler(4f, 0f, 0f);
            ctrl.cockpitCameraAnchor = cockpitCam;

            var dashCam = new GameObject("DashcamAnchor").transform;
            dashCam.SetParent(root.transform, false);
            dashCam.localPosition = new Vector3(0f, 1.98f, 6.08f);
            dashCam.localRotation = Quaternion.Euler(2f, 0f, 0f);
            ctrl.bumperCameraAnchor = dashCam;

            var axleCam = new GameObject("AxleCameraAnchor").transform;
            axleCam.SetParent(root.transform, false);
            axleCam.localPosition = new Vector3(-2.1f, 0.95f, 1.8f);
            axleCam.localRotation = Quaternion.Euler(4f, 14f, 0f);
            ctrl.axleCameraAnchor = axleCam;

            float[] axleZs = { 4.25f, 2.75f, -2.55f, -4.05f };
            bool[] steerFlags = { true, true, false, false };
            bool[] driveFlags = { false, false, true, true };
            ctrl.wheels = BuildWheelColliderSet(root.transform, axleZs, steerFlags, driveFlags, 1.18f, 0.50f, 0.24f, matTire, matRim);
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
            rb.linearDamping = 0.03f;
            rb.angularDamping = 0.35f;
#else
            rb.drag = 0.03f;
            rb.angularDrag = 0.35f;
#endif

            var ctrl = root.AddComponent<KenyaStreetVehicle>();
            ctrl.displayName = "EPH Isuzu FRR 90 (6W)";
            ctrl.plateNumber = "KDL 409G";
            ctrl.isCoach = false;
            ctrl.motorTorque = 3100f;
            ctrl.brakeTorque = 5800f;
            ctrl.maxSpeedKmh = 96f;
            ctrl.centerOfMassOffset = new Vector3(0f, -0.55f, 0.1f);

            var chassisCol = root.AddComponent<BoxCollider>();
            chassisCol.center = new Vector3(0f, 1.85f, 0f);
            chassisCol.size = new Vector3(2.42f, 2.55f, 8.65f);

            var matNavy = GetMat("IsuzuCabNavyGloss", Palette.IsuzuCabNavy, 0.76f, 0.22f);
            var matCargo = GetMat("IsuzuCargoMabati", Palette.IsuzuCargoSilver, 0.48f, 0.38f, null, texMabatiAlb, texMabatiNrm, new Vector2(5f, 2f), 1.2f);
            var matGold = GetMat("IsuzuStripeGold", Palette.IsuzuStripeGold, 0.65f, 0.35f);
            var matBullbar = GetMat("BullbarSteel", Palette.BullbarSteel, 0.6f, 0.8f);
            var matWhite = GetMat("EnaBodyPearlGloss", Palette.EnaBodyWhite, 0.72f, 0.08f);
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
            CreateCustomMesh("SculptedForwardCab", body, Vector3.zero, Quaternion.identity, Vector3.one, KenyaSculptedMeshFactory.IsuzuCabMesh, matNavy, false);
            CreateBox("RoofFairing", body, new Vector3(0f, 3.06f, 2.75f), new Vector3(2.10f, 0.46f, 1.55f), matWhite, false);
            var ws = CreateBox("CabWindshield", body, new Vector3(0f, 2.22f, 4.10f), new Vector3(1.98f, 0.88f, 0.10f), matGlass, false);
            ws.transform.localRotation = Quaternion.Euler(-14f, 0f, 0f);
            CreateBox("CabSideGlass", body, new Vector3(0f, 2.15f, 3.05f), new Vector3(2.30f, 0.80f, 1.38f), matGlass, false);
            CreateBox("CabGrille", body, new Vector3(0f, 1.42f, 4.24f), new Vector3(1.62f, 0.48f, 0.08f), matChrome, false);

            CreateBox("BullbarBase", body, new Vector3(0f, 0.92f, 4.44f), new Vector3(2.34f, 0.28f, 0.24f), matBullbar, false);
            CreateBox("BullbarTop", body, new Vector3(0f, 1.56f, 4.46f), new Vector3(2.12f, 0.10f, 0.10f), matBullbar, false);
            CreateBox("BullbarPostL", body, new Vector3(-0.72f, 1.24f, 4.46f), new Vector3(0.10f, 0.68f, 0.10f), matBullbar, false);
            CreateBox("BullbarPostR", body, new Vector3( 0.72f, 1.24f, 4.46f), new Vector3(0.10f, 0.68f, 0.10f), matBullbar, false);
            CreateCylinder("AirSnorkel", body, new Vector3(-1.20f, 2.25f, 2.05f), new Vector3(0.16f, 1.15f, 0.16f), matBullbar);

            CreateBox("CargoBoxMain", body, new Vector3(0f, 2.18f, -1.25f), new Vector3(2.42f, 2.48f, 5.95f), matCargo, false);
            CreateBox("CargoGoldStripe", body, new Vector3(0f, 1.35f, -1.25f), new Vector3(2.46f, 0.28f, 5.98f), matGold, false);
            CreateWorldSignText("CargoSignL", body, "EPH LOGISTICS  •  ISUZU FRR 90", new Vector3(-1.24f, 2.28f, -1.25f), new Vector2(5.4f, 0.72f), 3.4f, Palette.IsuzuCabNavy, Quaternion.Euler(0f, 90f, 0f));
            CreateWorldSignText("CargoSignR", body, "EPH LOGISTICS  •  ISUZU FRR 90", new Vector3( 1.24f, 2.28f, -1.25f), new Vector2(5.4f, 0.72f), 3.4f, Palette.IsuzuCabNavy, Quaternion.Euler(0f, -90f, 0f));

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

            var dashCam = new GameObject("DashcamAnchor").transform;
            dashCam.SetParent(root.transform, false);
            dashCam.localPosition = new Vector3(0f, 1.92f, 4.48f);
            ctrl.bumperCameraAnchor = dashCam;

            var axleCam = new GameObject("AxleCameraAnchor").transform;
            axleCam.SetParent(root.transform, false);
            axleCam.localPosition = new Vector3(-2.0f, 0.95f, 1.2f);
            axleCam.localRotation = Quaternion.Euler(4f, 14f, 0f);
            ctrl.axleCameraAnchor = axleCam;

            float[] axleZs = { 2.85f, -1.85f, -2.95f };
            bool[] steerFlags = { true, false, false };
            bool[] driveFlags = { false, true, true };
            ctrl.wheels = BuildWheelColliderSet(root.transform, axleZs, steerFlags, driveFlags, 1.12f, 0.48f, 0.24f, matTire, matRim);
            return ctrl;
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

        private static GameObject CreateCustomMesh(string name, Transform parent, Vector3 localPos, Quaternion localRot, Vector3 localScale, Mesh mesh, Material mat, bool withMeshCollider)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot;
            go.transform.localScale = localScale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            if (withMeshCollider) go.AddComponent<MeshCollider>().sharedMesh = mesh;
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
            if (!withCollider) Object.DestroyImmediate(go.GetComponent<Collider>());
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
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(path, true) };
        }

        private static int CountDefaultMaterials()
        {
            int count = 0;
            foreach (var r in Object.FindObjectsByType<Renderer>())
            {
                if (r is ParticleSystemRenderer) continue;
                var m = r.sharedMaterial;
                if (m == null || m.name.Contains("Default-Material") || m.name.Contains("Lit (Instance)")) count++;
            }
            return count;
        }
    }
}
