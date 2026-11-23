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
        private const string ScaniaBusAssetPath = "Assets/KenyaStreetSim/ImportedModels/ScaniaTouring2026/Scania_Touring_2026.glb";
        private const string IsuzuFrrAssetPath = "Assets/KenyaStreetSim/ImportedModels/Fleet/Isuzu_FRR_Kenyan.glb";
        private const string IsuzuTxAssetPath = "Assets/KenyaStreetSim/ImportedModels/Fleet/Isuzu_TX_Lorry.glb";
        private const string MarcopoloG7AssetPath = "Assets/KenyaStreetSim/ImportedModels/Fleet/Marcopolo_G7_Volvo.glb";
        private const string MarcopoloExpressAssetPath = "Assets/KenyaStreetSim/ImportedModels/Fleet/Marcopolo_G7_1200_Express.glb";
        private const string YutongEts2AssetPath = "Assets/KenyaStreetSim/ImportedModels/Fleet/Yutong_ZK6888H_ETS2.glb";
        private const string RangeRoverAssetPath = "Assets/KenyaStreetSim/ImportedModels/Fleet/RangeRover_Sport_2018.fbx";
        private const string RealCoachHornAssetPath = "Assets/KenyaStreetSim/Audio/CoachAirHorn_Real.ogg";
        private const string ImportedVehiclesFolder = "Assets/KenyaStreetSim/ImportedModels/Vehicles";
        private const string ImportedPeopleFolder = "Assets/KenyaStreetSim/ImportedModels/People";

        private static readonly string[] ShopNames =
        {
            "SAFARICOM SHOP", "M-PESA AGENT", "JAVA HOUSE", "NAIVAS SUPERMARKET",
            "KCB BANK", "EQUITY BANK", "ARTCAFFE", "QUICKMART",
            "KENYA PHARMACY", "SIMBA ELECTRONICS", "ELDORET SPARES", "CARREFOUR EXPRESS",
            "UHURU BAKERY", "CO-OP BANK", "RIFT VALLEY OPTICS", "SAVANNAH GRILL"
        };

        private static Dictionary<string, Material> matCache;
        private static Shader urpLitShader;
        private static Shader urpParticleShader;
        private static List<GameObject> importedVehiclePrefabs;
        private static List<GameObject> importedPeoplePrefabs;

        private static Texture2D texTarmacAlb, texTarmacNrm;
        private static Texture2D texMurramAlb, texMurramNrm;
        private static Texture2D texSavannaAlb, texSavannaNrm;
        private static Texture2D texCabroAlb, texCabroNrm;
        private static Texture2D texPlasterAlb, texPlasterNrm;
        private static Texture2D texMabatiAlb, texMabatiNrm;
        private static Texture2D texBarkAlb, texBarkNrm;
        private static Texture2D texLeafCluster;
        private static Texture2D texTireAlb, texTireNrm;
        private static Texture2D texGlassAlb, texGlassNrm;
        private static Texture2D texSkyDay, texSkySunset, texSkyNight;

        [MenuItem("Kenya Street Sim/Build Full Kenya ENA Coach Map")]
        public static string BuildKenyaTownScene()
        {
            EnsureFolders();
            KenyaPbrTextureGenerator.GenerateAllTextures();
            KenyaSculptedMeshFactory.BuildAllMeshes();
            LoadGeneratedTextures();
            DiscoverImportedModels();

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

            // 1. Full Multi-Biome Terrain + Lake Victoria + Lake Naivasha + Indian Ocean + All 8 Highways + 3 Roundabouts
            BuildKenyaTerrainWaterAndHighways(worldRoot.transform);

            // 2. Nairobi CBD Traffic Light Intersection + KICC Skyline + Sidewalks + KPLC Power Lines + 5 Rubis Stations
            var nairobiLight = BuildNairobiCbdCore(worldRoot.transform, streetLights, lampRenderers);

            // 3. Build All 16 Living Kenyan ENA Coach Towns (Curve-Aligned & Road-Clearance Verified!) + 16 Bus Stage Terminals
            var stages = BuildAllSixteenKenyaTownsAndStages(worldRoot.transform, streetLights, lampRenderers);

            // 4. KeNHA Directional Highway Gantries, 3 Roundabout Monuments, Sirare Border Arch & Mombasa Crossed Tusks!
            BuildKenyaNationalLandmarksAndGantries(worldRoot.transform);

            // 5. Highway Acacia Savanna, Kericho/Kakamega Highlands & Coastal/Lakeside Palm Trees along corridors
            BuildHighwayBiomeFoliage(worldRoot.transform);

            // 6. Town-Weighted Anti-Jam Passing Traffic (More Diverse Cars in Major Towns, Lighter Rural Highways!)
            int trafficCount = BuildFullKenyaSplineTraffic(worldRoot.transform);

            // 6b. 4 Authentic Kenyan Police Highway Roadblocks (Officer Walks Around Vehicle & Waves You Through!)
            int roadblockCount = KenyaPoliceRoadblockBuilder.BuildAllRoadblocks(worldRoot.transform);

            // 7. Real 3D Animated Sidewalk & Zebra Pedestrians in Nairobi CBD + Regional Towns
            BuildNairobiPedestrians(worldRoot.transform, nairobiLight);

            // 8. Full 5-Vehicle Playable Kenyan Fleet (All Selectable on Title Screen & via [V], All with [C] Cockpit Dashboard & Live Mirrors!)
            var fleetRoot = new GameObject("PlayerFleet");
            Vector3 startPos = new Vector3(-3.5f, 0.32f, -24f);
            var matSharedTire = GetMat("TireRubberPBR", Color.white, 0.22f, 0f, null, texTireAlb, texTireNrm, new Vector2(1f, 3f), 1.3f);
            var matSharedRim = GetMat("WheelRimAlcoa", Palette.WheelRimAlloy, 0.92f, 0.92f);
            var matSharedHead = GetMat("HeadlightWarm", Palette.HeadlightWarm, 0.85f, 0f, Palette.HeadlightWarm * 1.6f);
            var matSharedTail = GetMat("TaillightRed", Palette.TaillightRed, 0.7f, 0f, Palette.TaillightRed * 1.2f);
            var matSharedInd = GetMat("IndicatorAmber", Palette.UiAccentAmber, 0.7f, 0f, Color.black);
            var matSharedPlate = GetMat("PlateYellow", Palette.PlateYellow, 0.45f);
            var matSharedInt = GetMat("CabInterior", Palette.CabInterior, 0.28f);
            var matSharedBlk = GetMat("EnaStripeBlackGloss", Palette.EnaStripeBlack, 0.75f, 0.12f);
            var matSharedChr = GetMat("EnaChrome", Palette.EnaChrome, 0.96f, 0.94f);
            var matSharedGlass = GetTransparentGlassMat("EnaPanoramicWindshield", Palette.WithAlpha(Palette.EnaGlassDark, 0.14f));

            var enaScania = BuildEnaCoach(fleetRoot.transform, startPos);
            var policeLandCruiser = KenyaPoliceLandCruiserBuilder.BuildKenyaPoliceLandCruiser(
                fleetRoot.transform, startPos,
                matSharedTire, matSharedHead, matSharedTail, matSharedInd,
                matSharedPlate, matSharedInt, matSharedBlk, matSharedChr, matSharedGlass);
            var isuzuFrr = BuildImportedModVehicle(
                fleetRoot.transform, startPos, IsuzuFrrAssetPath,
                "Kenyan_Isuzu_FRR_KDL409G", "Kenyan Isuzu FRR (KDL 409G)", "KDL 409G",
                isCoach: false, targetLength: 8.3f, massKg: 7600f, motorTorque: 3250f, maxSpeedKmh: 98f,
                extraYawDeg: 0f, matSharedTire, matSharedRim, matSharedHead, matSharedTail, matSharedInd, matSharedPlate, matSharedInt, matSharedBlk, matSharedChr)
                ?? BuildIsuzuFrr(fleetRoot.transform, startPos);
            var isuzuTx = BuildImportedModVehicle(
                fleetRoot.transform, startPos, IsuzuTxAssetPath,
                "Kenyan_Isuzu_TX_Lorry_KCY812M", "Kenyan Isuzu TX Lorry (KCY 812M)", "KCY 812M",
                isCoach: false, targetLength: 8.0f, massKg: 8200f, motorTorque: 3350f, maxSpeedKmh: 94f,
                extraYawDeg: 0f, matSharedTire, matSharedRim, matSharedHead, matSharedTail, matSharedInd, matSharedPlate, matSharedInt, matSharedBlk, matSharedChr)
                ?? BuildIsuzuFrr(fleetRoot.transform, startPos);
            var marcopoloG7 = BuildImportedModVehicle(
                fleetRoot.transform, startPos, MarcopoloG7AssetPath,
                "Marcopolo_G7_Volvo_KDR100E", "Marcopolo G7 Volvo (KDR 100E)", "KDR 100E",
                isCoach: true, targetLength: 13.6f, massKg: 11400f, motorTorque: 4250f, maxSpeedKmh: 108f,
                extraYawDeg: 180f, matSharedTire, matSharedRim, matSharedHead, matSharedTail, matSharedInd, matSharedPlate, matSharedInt, matSharedBlk, matSharedChr)
                ?? BuildEnaCoach(fleetRoot.transform, startPos);
            var yutongEts2 = BuildImportedModVehicle(
                fleetRoot.transform, startPos, YutongEts2AssetPath,
                "Yutong_ZK6888H_ETS2_KDT888Y", "Yutong ZK6888H ETS2 (KDT 888Y)", "KDT 888Y",
                isCoach: true, targetLength: 9.85f, massKg: 10400f, motorTorque: 3950f, maxSpeedKmh: 112f,
                extraYawDeg: 0f, matSharedTire, matSharedRim, matSharedHead, matSharedTail, matSharedInd, matSharedPlate, matSharedInt, matSharedBlk, matSharedChr);

            var fleetList = new List<KenyaStreetVehicle> { enaScania, policeLandCruiser, isuzuFrr, isuzuTx, marcopoloG7 };
            if (yutongEts2 != null) fleetList.Add(yutongEts2);

            // Auto-discover any extra user-converted ETS2 mods (*_ETS2.glb) placed in ImportedModels/Fleet/ via the ETS2 Importer!
            string fleetDir = "Assets/KenyaStreetSim/ImportedModels/Fleet";
            if (Directory.Exists(fleetDir))
            {
                int extraIdx = 1;
                foreach (var f in Directory.GetFiles(fleetDir, "*_ETS2.glb"))
                {
                    string assetPath = f.Replace("\\", "/");
                    if (string.Equals(assetPath, YutongEts2AssetPath, System.StringComparison.OrdinalIgnoreCase)) continue;
                    string modBase = Path.GetFileNameWithoutExtension(f).Replace("_ETS2", "").Replace("_", " ");
                    string plate = $"KDU {100 + extraIdx:000}E";
                    var extraMod = BuildImportedModVehicle(
                        fleetRoot.transform, startPos, assetPath,
                        $"ETS2Mod_{extraIdx}_{modBase.Replace(" ", "_")}", $"{modBase} ETS2 ({plate})", plate,
                        isCoach: true, targetLength: 10.5f, massKg: 10500f, motorTorque: 4000f, maxSpeedKmh: 110f,
                        extraYawDeg: 0f, matSharedTire, matSharedRim, matSharedHead, matSharedTail, matSharedInd, matSharedPlate, matSharedInt, matSharedBlk, matSharedChr);
                    if (extraMod != null)
                    {
                        fleetList.Add(extraMod);
                        extraIdx++;
                    }
                }
            }

            for (int fi = 1; fi < fleetList.Count; fi++)
            {
                fleetList[fi].gameObject.SetActive(false);
            }

            // 9. Runtime Systems: Real Vehicle Audio, Dynamic Rain/Clouds Weather, 3D GPS Guide, Time of Day, HUD, Interactive Map, Game Loop
            var sysRoot = new GameObject("KenyaStreetSystems");
            var audioSys = sysRoot.AddComponent<KenyaStreetAudio>();
            var realHornClip = AssetDatabase.LoadAssetAtPath<AudioClip>(RealCoachHornAssetPath);
            if (realHornClip != null) audioSys.realCoachHornClip = realHornClip;

            var weatherSys = sysRoot.AddComponent<KenyaWeatherSystem>();
            if (matCache.TryGetValue("KenyaHighwayTarmac", out var tarmacMat))
            {
                weatherSys.highwayTarmacMaterial = tarmacMat;
            }
            weatherSys.sunLight = sunLight;

            sysRoot.AddComponent<KenyaGpsRouteGuide>();

            var tod = sysRoot.AddComponent<KenyaTimeOfDay>();
            tod.sunLight = sunLight;
            tod.skyboxMaterial = skyMat;
            tod.daySkyPanoramic = texSkyDay;
            tod.sunsetSkyPanoramic = texSkySunset;
            tod.nightSkyPanoramic = texSkyNight;
            tod.streetPointLights = streetLights.ToArray();
            tod.lampGlowRenderers = lampRenderers.ToArray();

            var hud = sysRoot.AddComponent<KenyaStreetHUD>();
            var routeMap = sysRoot.AddComponent<KenyaRouteMapUI>();
            var loop = sysRoot.AddComponent<StreetGameLoop>();
            loop.fleet = fleetList.ToArray();
            loop.stages = stages;
            loop.streetCamera = camObj.GetComponent<KenyaStreetCamera>();
            loop.hud = hud;
            loop.routeMap = routeMap;
            loop.startPosition = startPos;

            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene, ScenePath);
            SetBuildSettingsScene(ScenePath);
            KenyaHdGameViewConfigurator.ApplyFullHdCrispGameView(maximizeOnPlay: true, silent: true);

            int meshCount = Object.FindObjectsByType<MeshRenderer>().Length + Object.FindObjectsByType<SkinnedMeshRenderer>().Length;
            int defaultMatCount = CountDefaultMaterials();
            int housesOnRoad = AuditHousesOnRoad(worldRoot.transform);
            int pedCount = Object.FindObjectsByType<KenyaPedestrianAI>().Length;
            return $"Built 16-Town Kenya ENA Coach Map ({ScenePath}) | Towns={KenyaNetworkData.Hubs.Length} | Highways={KenyaNetworkData.Highways.Length} | Roundabouts={KenyaNetworkData.Roundabouts.Length} | Roadblocks={roadblockCount} | Vehicles={trafficCount} (3D={importedVehiclePrefabs.Count}) | People={pedCount} (3D={importedPeoplePrefabs.Count}) | Renderers={meshCount} | HousesOnRoad={housesOnRoad} | DefaultMaterials={defaultMatCount} | SpeedBumps=0";
        }

        private static void DiscoverImportedModels()
        {
            importedVehiclePrefabs = LoadModelsInFolder(ImportedVehiclesFolder);
            importedPeoplePrefabs = LoadModelsInFolder(ImportedPeopleFolder);
        }

        private static List<GameObject> LoadModelsInFolder(string folder)
        {
            var list = new List<GameObject>();
            if (!Directory.Exists(folder)) return list;
            string[] files = Directory.GetFiles(folder);
            System.Array.Sort(files);
            foreach (var f in files)
            {
                string ext = Path.GetExtension(f).ToLowerInvariant();
                if (ext != ".glb" && ext != ".gltf" && ext != ".fbx" && ext != ".prefab" && ext != ".obj") continue;
                string assetPath = f.Replace("\\", "/");
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                if (go != null) list.Add(go);
            }
            return list;
        }

        private static int AuditHousesOnRoad(Transform worldRoot)
        {
            Physics.SyncTransforms();
            int violations = 0;
            foreach (var t in worldRoot.GetComponentsInChildren<Transform>(true))
            {
                if (!t.name.StartsWith("Shop_") && !t.name.StartsWith("RubisStation_")) continue;
                var mainBlock = t.Find("MainBlock") ?? t.Find("ShopBuilding");
                if (mainBlock == null) continue;
                var ls = mainBlock.lossyScale;
                if (!KenyaNetworkData.IsFootprintClearOfAllRoads(mainBlock.position, ls.x * 0.5f, ls.z * 0.5f, mainBlock.eulerAngles.y, 0.5f))
                {
                    violations++;
                }
            }
            return violations;
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
            string cc0 = $"{tf}/CC0_PBR";

            // Prefer real photographed CC0 2K Poly Haven PBR maps when present, with clean procedural fallback!
            texTarmacAlb = LoadFirstTex($"{cc0}/CC0_Asphalt_Albedo.jpg", $"{tf}/Tarmac_Albedo.png");
            texTarmacNrm = LoadFirstTex($"{cc0}/CC0_Asphalt_Normal.jpg", $"{tf}/Tarmac_Normal.png");
            texMurramAlb = AssetDatabase.LoadAssetAtPath<Texture2D>($"{tf}/Murram_Albedo.png");
            texMurramNrm = AssetDatabase.LoadAssetAtPath<Texture2D>($"{tf}/Murram_Normal.png");
            texSavannaAlb = LoadFirstTex($"{cc0}/CC0_SavannaTerrain_Albedo.jpg", $"{tf}/Murram_Albedo.png");
            texSavannaNrm = LoadFirstTex($"{cc0}/CC0_SavannaTerrain_Normal.jpg", $"{tf}/Murram_Normal.png");
            texCabroAlb = LoadFirstTex($"{cc0}/CC0_Cabro_Albedo.jpg", $"{tf}/Cabro_Albedo.png");
            texCabroNrm = LoadFirstTex($"{cc0}/CC0_Cabro_Normal.jpg", $"{tf}/Cabro_Normal.png");
            texPlasterAlb = LoadFirstTex($"{cc0}/CC0_Plaster_Albedo.jpg", $"{tf}/Plaster_Albedo.png");
            texPlasterNrm = LoadFirstTex($"{cc0}/CC0_Plaster_Normal.jpg", $"{tf}/Plaster_Normal.png");
            texMabatiAlb = LoadFirstTex($"{cc0}/CC0_Mabati_Albedo.jpg", $"{tf}/Mabati_Albedo.png");
            texMabatiNrm = LoadFirstTex($"{cc0}/CC0_Mabati_Normal.jpg", $"{tf}/Mabati_Normal.png");
            texBarkAlb = LoadFirstTex($"{cc0}/CC0_Bark_Albedo.jpg", $"{tf}/Bark_Albedo.png");
            texBarkNrm = LoadFirstTex($"{cc0}/CC0_Bark_Normal.jpg", $"{tf}/Bark_Normal.png");
            texLeafCluster = AssetDatabase.LoadAssetAtPath<Texture2D>($"{tf}/LeafCluster_Albedo.png");
            texTireAlb = AssetDatabase.LoadAssetAtPath<Texture2D>($"{tf}/TireTread_Albedo.png");
            texTireNrm = AssetDatabase.LoadAssetAtPath<Texture2D>($"{tf}/TireTread_Normal.png");
            texGlassAlb = AssetDatabase.LoadAssetAtPath<Texture2D>($"{tf}/WindowFacade_Albedo.png");
            texGlassNrm = AssetDatabase.LoadAssetAtPath<Texture2D>($"{tf}/WindowFacade_Normal.png");

            texSkyDay = AssetDatabase.LoadAssetAtPath<Texture2D>($"{cc0}/Sky_KenyaDay_2k.hdr");
            texSkySunset = AssetDatabase.LoadAssetAtPath<Texture2D>($"{cc0}/Sky_KenyaSunset_2k.hdr");
            texSkyNight = AssetDatabase.LoadAssetAtPath<Texture2D>($"{cc0}/Sky_KenyaNight_2k.hdr");
        }

        private static Texture2D LoadFirstTex(params string[] paths)
        {
            for (int i = 0; i < paths.Length; i++)
            {
                var t = AssetDatabase.LoadAssetAtPath<Texture2D>(paths[i]);
                if (t != null) return t;
            }
            return null;
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
            sun.intensity = 1.55f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.85f;
            sun.shadowBias = 0.04f;
            sun.shadowNormalBias = 0.35f;
            sunGo.transform.rotation = Quaternion.Euler(42f, -34f, 0f);

            skyMat = AssetDatabase.LoadAssetAtPath<Material>(SkyMatPath);
            var panoShader = Shader.Find("Skybox/Panoramic");
            var procShader = Shader.Find("Skybox/Procedural");
            if (skyMat == null)
            {
                skyMat = new Material(texSkyDay != null && panoShader != null ? panoShader : procShader);
                AssetDatabase.CreateAsset(skyMat, SkyMatPath);
            }
            if (texSkyDay != null && panoShader != null)
            {
                skyMat.shader = panoShader;
                skyMat.SetTexture("_MainTex", texSkyDay);
                skyMat.SetColor("_Tint", Color.white);
                skyMat.SetFloat("_Exposure", 0.78f);
                skyMat.SetFloat("_Rotation", 28f);
                skyMat.SetFloat("_Mapping", 1f);   // Latitude Longitude Layout
                skyMat.SetFloat("_ImageType", 0f); // 360 Degrees
            }
            else
            {
                skyMat.shader = procShader;
                skyMat.SetColor("_SkyTint", Palette.SkyTint);
                skyMat.SetColor("_GroundColor", Palette.SkyGround);
                skyMat.SetFloat("_AtmosphereThickness", 0.72f);
                skyMat.SetFloat("_SunSize", 0.028f);
                skyMat.SetFloat("_Exposure", 1.24f);
            }
            EditorUtility.SetDirty(skyMat);

            RenderSettings.skybox = skyMat;
            RenderSettings.sun = sun;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
            RenderSettings.reflectionIntensity = 1.0f;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Palette.AmbientSky;
            RenderSettings.ambientEquatorColor = Palette.AmbientEquator;
            RenderSettings.ambientGroundColor = Palette.AmbientGround;
            // Crystal-clear high-altitude Kenyan sunlight visibility (minimal haze!)
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.72f, 0.82f, 0.93f, 1f);
            RenderSettings.fogDensity = 0.00024f;
            DynamicGI.UpdateEnvironment();

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
            bl.threshold.value = 1.25f;
            bl.intensity.overrideState = true;
            bl.intensity.value = 0.14f;
            bl.scatter.overrideState = true;
            bl.scatter.value = 0.55f;

            // Zero dark muddy vignette for crisp bright edge-to-edge clarity
            if (!profile.TryGet<Vignette>(out var vig)) vig = profile.Add<Vignette>();
            vig.intensity.overrideState = true;
            vig.intensity.value = 0.0f;
            vig.smoothness.overrideState = true;
            vig.smoothness.value = 0.25f;

            if (!profile.TryGet<ColorAdjustments>(out var ca)) ca = profile.Add<ColorAdjustments>();
            ca.saturation.overrideState = true;
            ca.saturation.value = 12f;
            ca.contrast.overrideState = true;
            ca.contrast.value = 14f;
            ca.postExposure.overrideState = true;
            ca.postExposure.value = 0.16f;
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
            cam.allowMSAA = true;
            cam.fieldOfView = 54f;
            cam.nearClipPlane = 0.12f;
            cam.farClipPlane = 1600f;

            var camData = camGo.AddComponent<UniversalAdditionalCameraData>();
            camData.renderPostProcessing = true;
            // Crisp SMAA High instead of blurry FXAA so roads, text, vehicles & mirrors stay razor-sharp!
            camData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            camData.antialiasingQuality = AntialiasingQuality.High;

            camGo.AddComponent<AudioListener>();
            camGo.AddComponent<KenyaStreetCamera>();
            return camGo;
        }

        private static void BuildKenyaTerrainWaterAndHighways(Transform parent)
        {
            var matTerrain = GetMat("RiftMurramTerrain", Palette.Hex("#E4E6DC"), 0.08f, 0f, null, texSavannaAlb, texSavannaNrm, new Vector2(0.85f, 0.85f), 1.45f);
            var matTarmac = GetMat("KenyaHighwayTarmac", Palette.Hex("#888C94"), 0.24f, 0f, null, texTarmacAlb, texTarmacNrm, new Vector2(1.4f, 1.4f), 1.35f);
            var matMurramShoulder = GetMat("KenyaMurramShoulder", Color.white, 0.10f, 0f, null, texMurramAlb, texMurramNrm, Vector2.one, 1.3f);
            var matWhiteLine = GetMat("RoadLineWhite", Palette.RoadLineWhite, 0.15f);
            var matYellowLine = GetMat("RoadLineYellow", Palette.RoadLineYellow, 0.18f);
            var matCurb = GetMat("CurbStone", Palette.CurbStone, 0.22f, 0f, null, texPlasterAlb, texPlasterNrm, new Vector2(2f, 2f), 0.9f);
            var matGrass = GetMat("RoundaboutLawn", Palette.GrassVerge, 0.12f);
            var matTeaHills = GetMat("KerichoTeaGreen", Palette.Hex("#3E7C2F"), 0.14f, 0f, null, texMurramAlb, texMurramNrm, new Vector2(18f, 18f), 1.1f);
            var matLakeWater = GetMat("LakeVictoriaWater", Palette.Hex("#1E6EA1"), 0.92f, 0.12f);
            var matOceanWater = GetMat("IndianOceanWater", Palette.Hex("#1289A7"), 0.94f, 0.15f);

            // 1. Full 2780m x 1600m Multi-Biome Kenyan Terrain (Eldoret N -> Sirare S, Lake Victoria W -> Malindi E)
            var terrainGo = CreateCustomMesh("FullKenyaTerrainHills", parent, Vector3.zero, Quaternion.identity, Vector3.one, KenyaSculptedMeshFactory.RiftValleyTerrainMesh, matTerrain, withMeshCollider: false);
            terrainGo.isStatic = true;

            // Safety flat physics floor well below highways so no vehicle can ever fall out of world
            CreateBox("KenyaPhysicsSubfloor", parent, new Vector3(-170f, -0.45f, 280f), new Vector3(2900f, 0.4f, 1720f), matTerrain, withCollider: true);

            // Lush Emerald Tea & Rainforest Belts around Kericho, Bomet & Kakamega highlands!
            CreateBox("KerichoTeaBeltNorth", parent, new Vector3(-680f, -0.22f, 675f), new Vector3(380f, 0.12f, 130f), matTeaHills, false);
            CreateBox("BometTeaBeltSouth", parent, new Vector3(-860f, -0.22f, 115f), new Vector3(260f, 0.12f, 110f), matTeaHills, false);
            CreateBox("KakamegaRainforestBelt", parent, new Vector3(-780f, -0.22f, 895f), new Vector3(310f, 0.12f, 130f), matTeaHills, false);

            // Shimmering Lake Victoria (Kisumu + Homa Bay), Lake Naivasha, Lake Nakuru, and Indian Ocean (Mombasa -> Malindi)!
            CreateBox("LakeVictoriaWaterPlane", parent, new Vector3(-1400f, -0.28f, 520f), new Vector3(360f, 0.2f, 480f), matLakeWater, false);
            CreateBox("LakeNaivashaWaterPlane", parent, new Vector3(-165f, -0.26f, 345f), new Vector3(92f, 0.2f, 74f), matLakeWater, false);
            CreateBox("LakeNakuruWaterPlane", parent, new Vector3(-235f, -0.26f, 415f), new Vector3(110f, 0.2f, 80f), matLakeWater, false);
            CreateBox("IndianOceanMombasaPlane", parent, new Vector3(950f, -0.28f, -320f), new Vector3(440f, 0.2f, 340f), matOceanWater, false);
            CreateBox("IndianOceanMalindiPlane", parent, new Vector3(1140f, -0.28f, 10f), new Vector3(320f, 0.2f, 540f), matOceanWater, false);

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
                    CreateCylinder("PostBase", lamp, new Vector3(0f, 0.55f, 0f), new Vector3(0.24f, 0.55f, 0.24f), matPole);
                    CreateCylinder("Post", lamp, new Vector3(0f, 3.8f, 0f), new Vector3(0.15f, 3.8f, 0.15f), matPole);
                    var arm = CreateCylinder("GooseneckArm", lamp, new Vector3(1.05f, 7.62f, 0f), new Vector3(0.085f, 1.20f, 0.085f), matPole);
                    arm.transform.localRotation = Quaternion.Euler(0f, 0f, -76f);
                    CreateBox("CobraCap", lamp, new Vector3(2.22f, 7.90f, 0f), new Vector3(0.76f, 0.10f, 0.38f), matPole, false);
                    var head = CreateBox("Head", lamp, new Vector3(2.22f, 7.80f, 0f), new Vector3(0.64f, 0.08f, 0.32f), matLampGlow, false);
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

            // Lit Rubis Petrol Stations placed cleanly off the tarmac along major corridors (verified road-clear!)
            TryPlaceRubisStation(parent, new Vector3(26f, 0.16f, 12f), 0f, "RUBIS NAIROBI", pointLights, glowRenderers);
            TryPlaceRubisStation(parent, new Vector3(-820f, 0.16f, 224f), -75f, "RUBIS BOMET", pointLights, glowRenderers);
            TryPlaceRubisStation(parent, new Vector3(-430f, 0.16f, 845f), -84f, "RUBIS ELDORET", pointLights, glowRenderers);
            TryPlaceRubisStation(parent, new Vector3(295f, 0.16f, 88f), 130f, "RUBIS MTITO ANDEI", pointLights, glowRenderers);
            TryPlaceRubisStation(parent, new Vector3(470f, 0.16f, -68f), 122f, "RUBIS VOI TSAVO", pointLights, glowRenderers);

            return ctrl;
        }

        private static void TryPlaceRubisStation(Transform parent, Vector3 pos, float yawDeg, string label, List<Light> pointLights, List<Renderer> glowRenderers)
        {
            if (KenyaNetworkData.IsFootprintClearOfAllRoads(pos, 11f, 15f, yawDeg, minBufferFromTarmacEdge: 3.5f))
            {
                BuildRubisPetrolStation(parent, pos, yawDeg, label, pointLights, glowRenderers);
                return;
            }
            // Push perpendicular away from nearest road so petrol station forecourt NEVER touches tarmac!
            if (KenyaNetworkData.FindNearestRoadPoint(pos, out Vector3 roadPt, out _, out _))
            {
                Vector3 away = pos - roadPt;
                away.y = 0f;
                if (away.sqrMagnitude < 0.01f) away = Vector3.right;
                away.Normalize();
                for (int step = 1; step <= 6; step++)
                {
                    Vector3 cand = pos + away * (step * 4.5f);
                    if (KenyaNetworkData.IsFootprintClearOfAllRoads(cand, 11f, 15f, yawDeg, minBufferFromTarmacEdge: 3.5f))
                    {
                        BuildRubisPetrolStation(parent, cand, yawDeg, label, pointLights, glowRenderers);
                        return;
                    }
                }
            }
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

        private static BusStageZone[] BuildAllSixteenKenyaTownsAndStages(Transform parent, List<Light> pointLights, List<Renderer> glowRenderers)
        {
            var townsRoot = new GameObject("Kenya_16_Towns_And_Stages").transform;
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
            var matCabro = GetMat("TownCabroWalk", Color.white, 0.22f, 0f, null, texCabroAlb, texCabroNrm, new Vector2(2f, 4f), 1.15f);

            var rng = new System.Random(2542026);
            int shopCounter = 0;
            var stages = new BusStageZone[KenyaNetworkData.Hubs.Length];

            for (int h = 0; h < KenyaNetworkData.Hubs.Length; h++)
            {
                var hub = KenyaNetworkData.Hubs[h];
                var townGo = new GameObject($"Town_{h:00}_{hub.name}").transform;
                townGo.SetParent(townsRoot, false);

                // Curve-aligned commercial shops lining BOTH sides of every Kenyan town — mathematically guaranteed OFF the road!
                int pairsPerTown = (h == 0) ? 7 : 5;
                float arcStart = (h == 0) ? -92f : -44f;
                float spacing = (h == 0) ? 21f : 22f;

                for (int k = 0; k < pairsPerTown; k++)
                {
                    float arcOffset = arcStart + k * spacing;
                    Vector3 roadPt, roadFwd;
                    if (h == 0)
                    {
                        roadPt = new Vector3(0f, 0f, hub.xz.y + arcOffset);
                        roadFwd = Vector3.forward;
                    }
                    else
                    {
                        KenyaNetworkData.SampleRoadAlongTown(hub.xz, hub.roadYawDeg, arcOffset, out roadPt, out roadFwd);
                    }
                    Vector3 roadRight = Vector3.Cross(Vector3.up, roadFwd).normalized;
                    float curveYaw = Mathf.Atan2(roadFwd.x, roadFwd.z) * Mathf.Rad2Deg;

                    for (int side = -1; side <= 1; side += 2)
                    {
                        if (h == 0 && side == 1 && Mathf.Abs(roadPt.z - 12f) < 26f) continue;
                        if (h == 0 && Mathf.Abs(roadPt.z - 95f) < 12f) continue;
                        if (side == -1 && Mathf.Abs(arcOffset) < 14f) continue; // clear bay for ENA Coach Terminal shelter!

                        float wFront = 14.0f + (float)rng.NextDouble() * 4.2f;
                        float depth = 11.2f;
                        int stories = (h == 0) ? rng.Next(3, 5) : rng.Next(2, 4);

                        float shopYaw = curveYaw + (side < 0 ? 90f : -90f);
                        float lateralOffset = 12.8f + depth * 0.5f;
                        Vector3 lotPos = roadPt + roadRight * (side * lateralOffset);
                        lotPos.y = 0.16f;

                        // GUARANTEE ZERO HOUSES ON ROAD: test all 9 rotated footprint corners against ALL 8 highways & ALL 3 roundabouts!
                        bool clear = false;
                        for (int push = 0; push < 4; push++)
                        {
                            Vector3 cand = roadPt + roadRight * (side * (lateralOffset + push * 2.8f));
                            cand.y = 0.16f;
                            if (KenyaNetworkData.IsFootprintClearOfAllRoads(cand, wFront * 0.5f + 0.5f, depth * 0.5f + 0.5f, shopYaw, minBufferFromTarmacEdge: 3.4f))
                            {
                                lotPos = cand;
                                clear = true;
                                break;
                            }
                        }
                        if (!clear) continue;

                        // Curve-aligned individual cabro sidewalk apron right in front of this verified shop (never crosses tarmac!)
                        if (h > 0)
                        {
                            Vector3 apronPos = lotPos - roadRight * (side * (depth * 0.5f + 2.1f));
                            apronPos.y = 0.08f;
                            if (KenyaNetworkData.IsFootprintClearOfAllRoads(apronPos, wFront * 0.48f, 1.8f, shopYaw, minBufferFromTarmacEdge: 0.35f))
                            {
                                var apron = CreateBox($"Apron_{h}_{k}_{side}", townGo, apronPos, new Vector3(wFront + 1.2f, 0.16f, 3.8f), matCabro, true);
                                apron.transform.rotation = Quaternion.Euler(0f, shopYaw, 0f);
                            }
                        }

                        Color facadeCol = hub.biome == KenyaNetworkData.KenyaBiome.CoastalMombasa
                            ? (k % 2 == 0 ? Palette.FacadeCream : Palette.Hex("#E9E4D8"))
                            : Palette.BuildingFacades[rng.Next(Palette.BuildingFacades.Length)];
                        Color awningCol = Palette.ShopAwnings[rng.Next(Palette.ShopAwnings.Length)];
                        string shopTitle = (k == 0 && side == 1)
                            ? $"{hub.name.ToUpperInvariant()} PLAZA"
                            : ShopNames[shopCounter++ % ShopNames.Length];

                        var facadeMat = GetMat($"FacadePBR_{ColorUtility.ToHtmlStringRGB(facadeCol)}", facadeCol, 0.16f, 0f, null, texPlasterAlb, texPlasterNrm, new Vector2(3f, 2.5f), 1.1f);
                        var awningMat = GetMat($"AwningMabati_{ColorUtility.ToHtmlStringRGB(awningCol)}", awningCol, 0.35f, 0.25f, null, texMabatiAlb, texMabatiNrm, new Vector2(5f, 1f), 1.25f);

                        BuildKenyanShopBuilding(
                            townGo,
                            lotPos,
                            yawDeg: shopYaw,
                            widthFront: wFront,
                            depth: depth,
                            stories: stories,
                            facadeMat, awningMat, matTrim, matGlass, matShopGlass, matFrame, matRoofMabati, matSign,
                            rng.NextDouble() > 0.45 ? matTankBlk : matTankBlu, matRail,
                            shopTitle,
                            hasWaterTank: rng.NextDouble() > 0.35);
                    }
                }

                // Shaded ENA Coach Bus Stage Terminal aligned cleanly to the actual road curve at EVERY Kenyan town!
                KenyaNetworkData.SampleRoadAlongTown(hub.xz, hub.roadYawDeg, 0f, out Vector3 stageRoadPt, out Vector3 stageRoadFwd);
                if (h == 0)
                {
                    stageRoadPt = new Vector3(0f, 0f, hub.xz.y);
                    stageRoadFwd = Vector3.forward;
                }
                Vector3 stageRight = Vector3.Cross(Vector3.up, stageRoadFwd).normalized;
                float stageYaw = Mathf.Atan2(stageRoadFwd.x, stageRoadFwd.z) * Mathf.Rad2Deg;

                var stageGo = new GameObject($"BusStage_{h + 1:00}_{hub.name}");
                stageGo.transform.SetParent(townGo, false);
                stageGo.transform.position = stageRoadPt - stageRight * 4.2f;
                stageGo.transform.rotation = Quaternion.Euler(0f, stageYaw, 0f);

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
                shelter.localPosition = new Vector3(-5.8f, 0.16f, 0f);
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

                // 3 REAL 3D waiting commuters at EVERY Kenyan town's ENA Coach Terminal!
                var commuters = new KenyaPedestrianAI[3];
                for (int c = 0; c < 3; c++)
                {
                    Vector3 localPos = new Vector3(-4.4f, 0.16f, -2.4f + c * 2.3f);
                    var ped = CreateArticulatedPedestrianLocal(stageGo.transform, $"Commuter_{h}_{c}", localPos, 90f, h * 5 + c);
                    ped.mode = KenyaPedestrianAI.BehaviorMode.BusStageWaiter;
                    ped.stageIndex = h;
                    commuters[c] = ped;
                }
                zone.waitingCommuters = commuters;

                // Night glow lamp above each regional ENA Coach terminal
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

            // 2. KeNHA Green Overhead Directional Highway Gantries across ALL 16-Town Kenyan Corridors!
            BuildKenhaGantry(root, new Vector3(0f, 0f, 122f), 0f,
                "NAIROBI INTERCHANGE  (KEEP LEFT)\n◄ NAROK / KISII (B3)   |   ▲ NAIVASHA / NAKURU / ELDORET (A104)   |   ► VOI / MOMBASA (A109)", matSteel, matKenhaGreen, matWhite);

            BuildKenhaGantry(root, new Vector3(-280f, 0f, 168f), -90f,
                "MAI MAHIU  •  GREAT RIFT VALLEY VIEWPOINT\nNAROK 30 KM   •   BOMET 60 KM   •   KISII 90 KM", matSteel, matKenhaGreen, matWhite);

            BuildKenhaGantry(root, new Vector3(-218f, 0f, 492f), -50f,
                "NAKURU RIFT ROUNDABOUT  (KEEP LEFT)\n▲ ELDORET / KAKAMEGA (A104/C39)   |   ◄ KERICHO / KISUMU (B1)", matSteel, matKenhaGreen, matWhite);

            BuildKenhaGantry(root, new Vector3(-470f, 0f, 804f), -84f,
                "ELDORET CITY  •  HOME OF CHAMPIONS\nKAKAMEGA RAINFOREST   •   KISUMU LAKESIDE (C39)", matSteel, matKenhaGreen, matWhite);

            BuildKenhaGantry(root, new Vector3(-1098f, 0f, 253f), -78f,
                "KISII JUNCTION ROUNDABOUT\n▲ HOMA BAY / KISUMU (C19)   |   ◄ MIGORI / SIRARE BORDER (A1)", matSteel, matKenhaGreen, matWhite);

            BuildKenhaGantry(root, new Vector3(-1160f, 0f, -182f), 180f,
                "KENYA — TANZANIA BORDER  (SIRARE / ISEBANIA)\nKARIBU TANZANIA   •   ENA COACH EXPRESS TERMINUS", matSteel, matEnaRed, matWhite);

            BuildKenhaGantry(root, new Vector3(-565f, 0f, 574f), -90f,
                "KERICHO  •  TEA CAPITAL OF KENYA\nKISUMU LAKESIDE  •  LAKE VICTORIA AHEAD", matSteel, matKenhaGreen, matWhite);

            BuildKenhaGantry(root, new Vector3(360f, 0f, -28f), 124f,
                "A109 MOMBASA ROAD  •  TSAVO CORRIDOR\nMTITO ANDEI  •  VOI  •  MOMBASA  •  MALINDI", matSteel, matKenhaGreen, matWhite);

            BuildKenhaGantry(root, new Vector3(925f, 0f, -68f), 20f,
                "B8 NORTH COAST HIGHWAY  •  INDIAN OCEAN\nKILIFI CREEK  •  MALINDI BEACH RESORT AHEAD", matSteel, matKenhaGreen, matWhite);

            // 3. ICONIC MOMBASA TUSKS ARCH (Moi Avenue Twin Smoothly-Curved Crossed Ivory Tusks + Coastal Palms!)
            var tusks = new GameObject("Mombasa_Iconic_Tusks_Arch").transform;
            tusks.SetParent(root, false);
            tusks.position = new Vector3(676f, 0f, -214f);
            tusks.rotation = Quaternion.Euler(0f, 116f, 0f);

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

                    bool isPalmRegion = (p.x > 560f && p.z < 180f) || (p.x < -880f && p.z > 330f); // Mombasa/Malindi Coast or Lake Victoria Kisumu/Homa Bay!

                    for (int side = -1; side <= 1; side += 2)
                    {
                        if (rng.NextDouble() < 0.25) continue;
                        float offset = 14.5f + (float)rng.NextDouble() * 16f;
                        Vector3 treePos = p + right * (side * offset);
                        if (KenyaNetworkData.DistanceToAnyTarmacEdge(new Vector2(treePos.x, treePos.z)) < 4.5f) continue;
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

        private static int BuildFullKenyaSplineTraffic(Transform parent)
        {
            var root = new GameObject("Kenya_Highway_LHT_Traffic").transform;
            root.SetParent(parent, false);

            var matTire = GetMat("TireRubberPBR", Color.white, 0.22f, 0f, null, texTireAlb, texTireNrm, new Vector2(1f, 3f), 1.2f);
            var matRim = GetMat("WheelRimAlloy", Palette.WheelRimAlloy, 0.85f, 0.85f);
            var matGlass = GetMat("EnaGlassDark", Palette.EnaGlassDark, 0.94f, 0.30f);
            var matHeadlight = GetMat("HeadlightWarm", Palette.HeadlightWarm, 0.85f, 0f, Palette.HeadlightWarm * 2.6f);
            var matTail = GetMat("TaillightRed", Palette.TaillightRed, 0.65f, 0f, Palette.TaillightRed * 1.6f);

            // Load the user's REAL HIGH-DETAIL 3D KENYAN VEHICLES for open rural inter-town highways!
            var realTrafficCatalog = new List<(GameObject prefab, float targetLen, float extraYaw, bool remapFbxUrp)>();
            var rrPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RangeRoverAssetPath);
            var frrPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(IsuzuFrrAssetPath);
            var txPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(IsuzuTxAssetPath);
            var g7Prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MarcopoloG7AssetPath);
            var g7ExpPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MarcopoloExpressAssetPath);
            var yutongPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(YutongEts2AssetPath);

            if (rrPrefab != null) realTrafficCatalog.Add((rrPrefab, 4.88f, 90f, true));
            if (frrPrefab != null) realTrafficCatalog.Add((frrPrefab, 7.90f, 0f, false));
            if (yutongPrefab != null) realTrafficCatalog.Add((yutongPrefab, 9.85f, 0f, false));
            if (txPrefab != null) realTrafficCatalog.Add((txPrefab, 7.50f, 0f, false));
            if (g7ExpPrefab != null) realTrafficCatalog.Add((g7ExpPrefab, 13.4f, 180f, false));
            if (g7Prefab != null) realTrafficCatalog.Add((g7Prefab, 13.4f, 180f, false));

            // A) SPARSE SMOOTH RURAL INTER-TOWN HIGHWAY TRAFFIC (Reduced density outside major towns -> ZERO traffic jams!)
            int[] ruralPerDir = { 1, 2, 1, 1, 1, 2, 1, 2 };
            int seed = 0;
            int totalSpawned = 0;

            for (int h = 0; h < KenyaNetworkData.Highways.Length; h++)
            {
                int count = h < ruralPerDir.Length ? ruralPerDir[h] : 1;
                for (int dir = 0; dir < 2; dir++)
                {
                    bool fwd = (dir == 0);
                    for (int k = 0; k < count; k++)
                    {
                        float progress = (k + 0.42f) / count;
                        if (h == 0 && fwd && progress < 0.48f) progress = 0.62f; // clear player start in Nairobi CBD

                        KenyaSplineTraffic ai;
                        if (realTrafficCatalog.Count > 0 && (seed % 4 != 3))
                        {
                            var entry = realTrafficCatalog[seed % realTrafficCatalog.Count];
                            ai = CreateHighDetailKenyanTrafficVehicle(
                                root, $"RuralHighway_H{h}_{(fwd ? "F" : "R")}_{k}_{entry.prefab.name}",
                                entry.prefab, entry.targetLen, entry.extraYaw, entry.remapFbxUrp, seed, matTire, matRim, matGlass, matHeadlight, matTail);
                            seed++;
                        }
                        else
                        {
                            // Authentic Kenyan 14-Seater PSV Matatu mixed onto rural corridors!
                            ai = CreateSplineTrafficVehicle(root, $"KenyaMatatuPSV_H{h}_{(fwd ? "F" : "R")}_{k}", 0, seed++, matTire, matRim, matGlass, matHeadlight, matTail);
                        }

                        ai.highwayIndex = h;
                        ai.forwardDirection = fwd;
                        ai.startNormalizedProgress = progress;
                        ai.laneOffsetMeters = 3.45f;
                        ai.cruiseSpeed = (h == 0 ? 11.5f : 15.8f) + (seed % 4) * 1.4f;
                        totalSpawned++;
                    }
                }
            }

            // B) LIVELY DIVERSE MAJOR-TOWN CITY CAR CLUSTERS (More varied free 3D cars INSIDE Nairobi, Nakuru, Kisumu, Eldoret & Mombasa!)
            // Mixes ALL 10 textured CC0 Kenney 3D Car Kit vehicles (sedan, suv, suv-luxury, hatchback-sports, taxi, van, delivery, truck, ambulance, police)
            // + Range Rovers + Kenyan Matatus circulating smoothly inside major Kenyan town sub-segments!
            (string townTag, int highwayIdx, float minN, float maxN, int perDir)[] majorTownClusters =
            {
                ("NairobiCBD", 0, 0.08f, 0.94f, 3),
                ("NakuruCity", 3, 0.62f, 0.96f, 2),
                ("KisumuCity", 4, 0.60f, 0.95f, 2),
                ("EldoretCity", 5, 0.28f, 0.62f, 2),
                ("MombasaCity", 7, 0.56f, 0.84f, 3)
            };

            for (int c = 0; c < majorTownClusters.Length; c++)
            {
                var cl = majorTownClusters[c];
                for (int dir = 0; dir < 2; dir++)
                {
                    bool fwd = (dir == 0);
                    for (int k = 0; k < cl.perDir; k++)
                    {
                        float localProg = (k + 0.30f) / cl.perDir;
                        // Keep immediate player spawn zone in Nairobi CBD forward lane completely clear
                        if (cl.highwayIdx == 0 && fwd && localProg < 0.45f) localProg = 0.52f + k * 0.16f;

                        KenyaSplineTraffic ai;
                        if (importedVehiclePrefabs != null && importedVehiclePrefabs.Count > 0 && (seed % 6 != 5))
                        {
                            var prefab = importedVehiclePrefabs[(seed++) % importedVehiclePrefabs.Count];
                            ai = CreateReal3DTrafficVehicle(root, $"TownCar_{cl.townTag}_{(fwd ? "F" : "R")}_{k}_{prefab.name}", prefab);
                        }
                        else if (rrPrefab != null && (seed % 2 == 0))
                        {
                            ai = CreateHighDetailKenyanTrafficVehicle(
                                root, $"TownSUV_{cl.townTag}_{(fwd ? "F" : "R")}_{k}_RangeRover",
                                rrPrefab, 4.88f, 90f, true, seed++, matTire, matRim, matGlass, matHeadlight, matTail);
                        }
                        else
                        {
                            ai = CreateSplineTrafficVehicle(root, $"TownMatatu_{cl.townTag}_{(fwd ? "F" : "R")}_{k}", 0, seed++, matTire, matRim, matGlass, matHeadlight, matTail);
                        }

                        ai.highwayIndex = cl.highwayIdx;
                        ai.forwardDirection = fwd;
                        ai.limitToSubRange = true;
                        ai.rangeMinNorm = fwd ? cl.minN : (1f - cl.maxN);
                        ai.rangeMaxNorm = fwd ? cl.maxN : (1f - cl.minN);
                        ai.startNormalizedProgress = Mathf.Repeat(localProg, 1f);
                        ai.laneOffsetMeters = 3.45f;
                        ai.cruiseSpeed = 11.2f + (seed % 4) * 1.2f;
                        totalSpawned++;
                    }
                }
            }
            return totalSpawned;
        }

        private static KenyaSplineTraffic CreateHighDetailKenyanTrafficVehicle(
            Transform parent, string name, GameObject modelPrefab, float targetLen, float extraYaw, bool remapFbxUrp, int colorSeed,
            Material matTire, Material matRim, Material matGlass, Material matHeadlight, Material matTail)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var ai = go.AddComponent<KenyaSplineTraffic>();

            // Wrap inside a clean alignment pivot so prefab root rotations (e.g. 270° X-up GLBs) are preserved intact!
            var modelPivot = new GameObject("ModelAlignPivot").transform;
            modelPivot.SetParent(go.transform, false);
            modelPivot.localPosition = Vector3.zero;
            modelPivot.localRotation = Quaternion.Euler(0f, extraYaw, 0f);
            modelPivot.localScale = Vector3.one;

            var inst = (GameObject)PrefabUtility.InstantiatePrefab(modelPrefab);
            inst.transform.SetParent(modelPivot, false);

            bool isMarcopoloG7 = modelPrefab.name.IndexOf("Marcopolo", System.StringComparison.OrdinalIgnoreCase) >= 0;
            foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
            {
                string rn = r.name.ToLowerInvariant();
                if (rn.Contains("shadow") || rn == "plane" || (isMarcopoloG7 && (r.name == "Object_22" || r.name == "Object_83")))
                {
                    r.gameObject.SetActive(false);
                }
            }

            Bounds rawB = MeasureRenderers(modelPivot);
            float scale = targetLen / Mathf.Max(0.5f, rawB.size.z);
            modelPivot.localScale = Vector3.one * scale;
            Bounds scaledB = MeasureRenderers(modelPivot);
            Vector3 relCenter = scaledB.center - go.transform.position;
            float relMinY = scaledB.min.y - go.transform.position.y;

            // Detect converted ETS2 wheel locators (ETS2_LOC_wheel_*): ground bottom of tires at Y=0 and mount real spinning wheels!
            var ets2WheelLocs = new List<Transform>();
            foreach (var t in inst.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.StartsWith("ETS2_LOC_wheel_")) ets2WheelLocs.Add(t);
            }
            float yShift = -relMinY;
            float ets2WheelRadius = 0.50f;
            if (ets2WheelLocs.Count >= 2)
            {
                float avgHubRelY = 0f;
                for (int w = 0; w < ets2WheelLocs.Count; w++) avgHubRelY += (ets2WheelLocs[w].position.y - go.transform.position.y);
                avgHubRelY /= ets2WheelLocs.Count;
                ets2WheelRadius = Mathf.Clamp(avgHubRelY, 0.38f, 0.56f);
                yShift = ets2WheelRadius - avgHubRelY;
            }
            modelPivot.localPosition = new Vector3(-relCenter.x, yShift, -relCenter.z);
            Bounds aligned = MeasureRenderers(modelPivot);

            Color[] suvPaints = { Palette.Hex("#ECEFF4"), Palette.Hex("#181C24"), Palette.Hex("#8E9AA8"), Palette.Hex("#1A3564"), Palette.Hex("#7A1E24") };
            Color chosenPaint = suvPaints[colorSeed % suvPaints.Length];
            var matPaint = GetMat($"RealSuvPaint_{ColorUtility.ToHtmlStringRGB(chosenPaint)}", chosenPaint, 0.88f, 0.42f);
            var matChrome = GetMat("EnaChrome", Palette.EnaChrome, 0.96f, 0.94f);
            var matBlackTrim = GetMat("EnaStripeBlackGloss", Palette.EnaStripeBlack, 0.42f, 0.15f);

            var wheelList = new List<Transform>();
            if (ets2WheelLocs.Count >= 2)
            {
                for (int w = 0; w < ets2WheelLocs.Count; w++)
                {
                    Vector3 locLocal = go.transform.InverseTransformPoint(ets2WheelLocs[w].position);
                    var pivot = new GameObject($"ETS2_TrafficWheel_{w}").transform;
                    pivot.SetParent(go.transform, false);
                    pivot.localPosition = new Vector3(locLocal.x, ets2WheelRadius, locLocal.z);
                    var tire = CreateCylinder("Tire", pivot, Vector3.zero, new Vector3(ets2WheelRadius * 2f, 0.18f, ets2WheelRadius * 2f), matTire);
                    tire.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                    var rim = CreateCylinder("Rim", pivot, Vector3.zero, new Vector3(ets2WheelRadius * 1.18f, 0.195f, ets2WheelRadius * 1.18f), matRim);
                    rim.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                    wheelList.Add(pivot);
                }
            }
            foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
            {
                if (!r.gameObject.activeInHierarchy) continue;
                string rn = r.name.ToLowerInvariant();
                if ((rn.Contains("wheel") || rn.Contains("tyre") || rn.Contains("tire")) &&
                    Vector3.Distance(r.transform.position, r.bounds.center) < 0.14f)
                {
                    wheelList.Add(r.transform);
                }

                if (remapFbxUrp)
                {
                    var matAmber = GetMat("IndicatorAmber", Palette.UiAccentAmber, 0.7f, 0f, Palette.UiAccentAmber * 0.8f);
                    var mats = r.sharedMaterials;
                    bool changed = false;
                    for (int i = 0; i < mats.Length; i++)
                    {
                        var m = mats[i];
                        string mn = m != null ? m.name.ToLowerInvariant() : "";
                        if (mn.Contains("tyre") || mn.Contains("tire") || mn.Contains("rubber")) mats[i] = matTire;
                        else if (mn.Contains("yellow")) mats[i] = matAmber;
                        else if (mn.Contains("glass") || mn.Contains("window") || mn.Contains("wind") || mn.Contains("trasnsparent")) mats[i] = matGlass;
                        else if (mn.Contains("chrome") || mn.Contains("silver") || mn.Contains("rim") || mn.Contains("alloy") || mn.Contains("metalic") || mn.Contains("mirror")) mats[i] = matChrome;
                        else if (mn.Contains("tail") || mn.Contains("brake") || mn.Contains("red") || mn.Contains("reflector")) mats[i] = matTail;
                        else if (mn.Contains("head") || mn.Contains("light") || mn.Contains("lamp")) mats[i] = matHeadlight;
                        else if (mn.Contains("black") || mn.Contains("plastic") || mn.Contains("grille") || mn.Contains("interior") || mn.Contains("carbon")) mats[i] = matBlackTrim;
                        else mats[i] = matPaint;
                        changed = true;
                    }
                    if (changed) r.sharedMaterials = mats;
                }
            }

            var col = go.AddComponent<BoxCollider>();
            Vector3 sz = aligned.size;
            col.center = new Vector3(0f, sz.y * 0.5f, 0f);
            col.size = new Vector3(Mathf.Clamp(sz.x, 1.85f, 2.75f), Mathf.Clamp(sz.y, 1.5f, 3.8f), Mathf.Clamp(sz.z, 4.4f, 13.8f));

            ai.vehicleHalfLength = col.size.z * 0.52f;
            ai.wheelRadius = targetLen > 6.5f ? 0.48f : 0.42f;
            ai.wheelVisuals = wheelList.ToArray();
            return ai;
        }

        private static KenyaSplineTraffic CreateReal3DTrafficVehicle(Transform parent, string name, GameObject modelPrefab)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var ai = go.AddComponent<KenyaSplineTraffic>();

            var inst = (GameObject)PrefabUtility.InstantiatePrefab(modelPrefab);
            inst.transform.SetParent(go.transform, false);
            inst.transform.localPosition = Vector3.zero;
            inst.transform.localRotation = Quaternion.identity;

            string lower = modelPrefab.name.ToLowerInvariant();
            bool isHeavy = lower.Contains("truck") || lower.Contains("delivery") || lower.Contains("ambulance") || lower.Contains("garbage");
            float scale = isHeavy ? 1.72f : 1.50f;
            inst.transform.localScale = Vector3.one * scale;

            // Bind Kenney 3D Car Kit colormap.png onto URP Lit so all 62 real 3D Kenyan vehicles display full colors, windows & black tires!
            var carAtlasTex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{ImportedVehiclesFolder}/Textures/colormap.png");
            Material matKenneyAtlas = carAtlasTex != null
                ? GetMat("KenneyReal3DCarAtlas", Color.white, 0.72f, 0.16f, null, carAtlasTex, null, Vector2.one, 1f)
                : null;

            // Measure actual scaled bounds to fit BoxCollider cleanly and align wheels to Y = 0
            Bounds b = new Bounds(Vector3.zero, new Vector3(2.0f, 1.6f, 4.2f));
            bool hasB = false;
            var wheelList = new List<Transform>();
            foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
            {
                if (matKenneyAtlas != null)
                {
                    var mats = r.sharedMaterials;
                    bool changed = false;
                    for (int mi = 0; mi < mats.Length; mi++)
                    {
                        if (mats[mi] != null && mats[mi].name.ToLowerInvariant().Contains("colormap"))
                        {
                            mats[mi] = matKenneyAtlas;
                            changed = true;
                        }
                    }
                    if (changed) r.sharedMaterials = mats;
                }
                if (!hasB) { b = r.bounds; hasB = true; }
                else b.Encapsulate(r.bounds);
            }
            if (hasB)
            {
                inst.transform.localPosition = new Vector3(-b.center.x, -b.min.y, -b.center.z);
            }

            foreach (var t in inst.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.IndexOf("wheel", System.StringComparison.OrdinalIgnoreCase) >= 0 && t.GetComponent<Renderer>() != null)
                {
                    wheelList.Add(t);
                }
            }

            var col = go.AddComponent<BoxCollider>();
            Vector3 sz = hasB ? b.size : new Vector3(2.1f, 1.8f, 4.4f);
            col.center = new Vector3(0f, sz.y * 0.5f, 0f);
            col.size = new Vector3(Mathf.Clamp(sz.x, 1.7f, 2.55f), Mathf.Clamp(sz.y, 1.3f, 2.9f), Mathf.Clamp(sz.z, 3.6f, 6.4f));

            ai.vehicleHalfLength = col.size.z * 0.52f;
            ai.wheelRadius = isHeavy ? 0.48f : 0.42f;
            ai.wheelVisuals = wheelList.ToArray();
            return ai;
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
                if (f == 1)
                {
                    CreateBox($"BalconyRail_{f}", b, new Vector3(0f, fy + 0.94f, frontZ + 0.56f), new Vector3(widthFront * 0.92f, 0.07f, 0.07f), matRail, false);
                    int postCnt = Mathf.Max(4, Mathf.FloorToInt(widthFront / 1.8f));
                    for (int bp = 0; bp <= postCnt; bp++)
                    {
                        float bx = Mathf.Lerp(-widthFront * 0.44f, widthFront * 0.44f, (float)bp / postCnt);
                        CreateBox($"BalconyPost_{f}_{bp}", b, new Vector3(bx, fy + 0.56f, frontZ + 0.56f), new Vector3(0.055f, 0.74f, 0.055f), matRail, false);
                    }
                }

                int winCount = Mathf.Max(2, Mathf.FloorToInt(widthFront / 3.4f));
                float step = (widthFront - 2.0f) / winCount;
                for (int w = 0; w < winCount; w++)
                {
                    float wx = -(widthFront - 2.0f) * 0.5f + step * (w + 0.5f);
                    CreateBox($"WinLintel_{f}_{w}", b, new Vector3(wx, fy + 2.48f, frontZ + 0.10f), new Vector3(1.88f, 0.14f, 0.22f), matTrim, false);
                    CreateBox($"WinSill_{f}_{w}",   b, new Vector3(wx, fy + 0.84f, frontZ + 0.12f), new Vector3(1.84f, 0.12f, 0.24f), matTrim, false);
                    CreateBox($"WinFrame_{f}_{w}",  b, new Vector3(wx, fy + 1.65f, frontZ + 0.05f), new Vector3(1.68f, 1.58f, 0.14f), matFrame, false);
                    CreateBox($"WinGlass_{f}_{w}",  b, new Vector3(wx, fy + 1.65f, frontZ + 0.09f), new Vector3(1.42f, 1.32f, 0.10f), matGlass, false);
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
            col.radius = 0.30f;
            col.height = 1.76f;

            var ai = go.AddComponent<KenyaPedestrianAI>();

            // Use REAL 3D rigged human models (Quaternius Men & Women or user-dropped GLB/FBX!) when available
            if (importedPeoplePrefabs != null && importedPeoplePrefabs.Count > 0)
            {
                var prefab = importedPeoplePrefabs[Mathf.Abs(seed) % importedPeoplePrefabs.Count];
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                inst.transform.SetParent(go.transform, false);
                inst.transform.localPosition = Vector3.zero;
                inst.transform.localRotation = Quaternion.identity;
                inst.transform.localScale = Vector3.one * (0.94f + (Mathf.Abs(seed) % 3) * 0.04f);

                // Apply authentic Kenyan skin tones & dark hair onto the rigged character's Skin/Hair materials!
                Color skin = Palette.SkinTones[Mathf.Abs(seed) % Palette.SkinTones.Length];
                var matKenyaSkin = GetMat($"KenyaHumanSkin_{ColorUtility.ToHtmlStringRGB(skin)}", skin, 0.24f);
                var matKenyaHair = GetMat("KenyaHumanHair", Palette.Hex("#181512"), 0.18f);
                foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
                {
                    var mats = r.sharedMaterials;
                    bool changed = false;
                    for (int m = 0; m < mats.Length; m++)
                    {
                        if (mats[m] == null) continue;
                        string mn = mats[m].name.ToLowerInvariant();
                        if (mn.Contains("skin")) { mats[m] = matKenyaSkin; changed = true; }
                        else if (mn.Contains("hair") || mn.Contains("eyebrow")) { mats[m] = matKenyaHair; changed = true; }
                    }
                    if (changed) r.sharedMaterials = mats;
                }

                // Wire skeletal rig bones into KenyaPedestrianAI so arms hang naturally & stride smoothly!
                ai.useSkeletalRig = true;
                ai.bodyPivot = inst.transform;
                foreach (var t in inst.GetComponentsInChildren<Transform>(true))
                {
                    switch (t.name)
                    {
                        case "Torso": ai.torsoBone = t; break;
                        case "Head": ai.headBone = t; break;
                        case "UpperArm.L": ai.leftArm = t; break;
                        case "LowerArm.L": ai.leftForeArm = t; break;
                        case "UpperArm.R": ai.rightArm = t; break;
                        case "LowerArm.R": ai.rightForeArm = t; break;
                        case "UpperLeg.L": ai.leftLeg = t; break;
                        case "LowerLeg.L": ai.leftLowerLeg = t; break;
                        case "UpperLeg.R": ai.rightLeg = t; break;
                        case "LowerLeg.R": ai.rightLowerLeg = t; break;
                    }
                }
                return ai;
            }

            // Fallback articulated stylized human if ImportedModels/People is empty
            Color fbSkin = Palette.SkinTones[seed % Palette.SkinTones.Length];
            Color shirt = Palette.ShirtColors[(seed * 3) % Palette.ShirtColors.Length];
            Color trouser = Palette.TrouserColors[(seed * 5) % Palette.TrouserColors.Length];

            var matSkin = GetMat($"Skin_{ColorUtility.ToHtmlStringRGB(fbSkin)}", fbSkin, 0.25f);
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
            rb.mass = 11200f;
#if UNITY_6000_0_OR_NEWER
            rb.linearDamping = 0.03f;
            rb.angularDamping = 0.35f;
#else
            rb.drag = 0.03f;
            rb.angularDrag = 0.35f;
#endif

            var ctrl = root.AddComponent<KenyaStreetVehicle>();
            ctrl.displayName = "ENA Scania 2026 (KDK 057M)";
            ctrl.plateNumber = "KDK 057M";
            ctrl.isCoach = true;
            ctrl.motorTorque = 4200f;
            ctrl.brakeTorque = 7400f;
            ctrl.maxSpeedKmh = 108f;
            ctrl.centerOfMassOffset = new Vector3(0f, -0.58f, 0.10f);

            var matBlack = GetMat("EnaStripeBlackGloss", Palette.EnaStripeBlack, 0.75f, 0.12f);
            var matChrome = GetMat("EnaChrome", Palette.EnaChrome, 0.96f, 0.94f);
            var matHead = GetMat("HeadlightWarm", Palette.HeadlightWarm, 0.85f, 0f, Palette.HeadlightWarm * 1.6f);
            var matTail = GetMat("TaillightRed", Palette.TaillightRed, 0.7f, 0f, Palette.TaillightRed * 1.2f);
            var matIndicator = GetMat("IndicatorAmber", Palette.UiAccentAmber, 0.7f, 0f, Color.black);
            var matPlate = GetMat("PlateYellow", Palette.PlateYellow, 0.45f);
            var matTire = GetMat("TireRubberPBR", Color.white, 0.22f, 0f, null, texTireAlb, texTireNrm, new Vector2(1f, 3f), 1.3f);
            var matRim = GetMat("WheelRimAlcoa", Palette.WheelRimAlloy, 0.92f, 0.92f);

            var body = new GameObject("VisualBodyRoot").transform;
            body.SetParent(root.transform, false);

            var scaniaAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ScaniaBusAssetPath);
            if (scaniaAsset != null)
            {
                // USE USER'S REAL 3D SCANIA TOURING 2026 LUXURY COACH MODEL!
                var chassisCol = root.AddComponent<BoxCollider>();
                chassisCol.center = new Vector3(0f, 2.05f, 0f);
                chassisCol.size = new Vector3(2.54f, 2.85f, 14.55f);

                var scaniaInst = (GameObject)PrefabUtility.InstantiatePrefab(scaniaAsset);
                scaniaInst.name = "Scania_Touring_2026_Model";
                scaniaInst.transform.SetParent(body, false);
                // Exact millimeter alignment verified from GLB bounds: centers X, grounds tires at Y=0, centers length at Z=0
                scaniaInst.transform.localPosition = new Vector3(0.14f, 0.81f, 4.58f);
                scaniaInst.transform.localRotation = Quaternion.identity;

                // Disable baked shadow planes & static combined wheel meshes (replaced by real spinning/steering WheelCollider wheels!)
                // AND make the Scania windshield & lowered sunshade crystal clear from inside the driver cockpit!
                var matClearScaniaGlass = GetTransparentGlassMat("ScaniaCrystalWindshield", new Color(0.86f, 0.94f, 1f, 0.08f));
                Transform realSteeringWheel = null;
                var brakeRenderers = new List<Renderer>();
                foreach (var t in scaniaInst.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == "BusShadowPlane" || t.name == "Plane" ||
                        t.name.StartsWith("front_wheel") || t.name.StartsWith("back_wheel"))
                    {
                        t.gameObject.SetActive(false);
                        continue;
                    }
                    if (t.name == "SteeringWheel_lp.001")
                    {
                        realSteeringWheel = t;
                    }
                    var r = t.GetComponent<Renderer>();
                    if (r != null)
                    {
                        if (t.name.Contains("Brakelight")) brakeRenderers.Add(r);
                        var mats = r.sharedMaterials;
                        bool changed = false;
                        for (int mi = 0; mi < mats.Length; mi++)
                        {
                            if (mats[mi] == null) continue;
                            string mn = mats[mi].name.ToLowerInvariant();
                            if (mn.Contains("sun_net") || (t.name.Contains("Windshield") && mn.Contains("glass")))
                            {
                                mats[mi] = matClearScaniaGlass;
                                changed = true;
                            }
                        }
                        if (changed) r.sharedMaterials = mats;
                    }
                }

                // Wrap real 3D Scania steering wheel around its exact mesh center so A/D turns it smoothly!
                if (realSteeringWheel != null)
                {
                    if (PrefabUtility.IsPartOfPrefabInstance(scaniaInst))
                    {
                        PrefabUtility.UnpackPrefabInstance(scaniaInst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                    }
                    var swRenderer = realSteeringWheel.GetComponent<Renderer>();
                    Vector3 worldCenter = swRenderer != null ? swRenderer.bounds.center : realSteeringWheel.position;
                    var pivotGo = new GameObject("ScaniaSteeringPivot");
                    pivotGo.transform.SetParent(realSteeringWheel.parent, false);
                    pivotGo.transform.position = worldCenter;
                    pivotGo.transform.rotation = root.transform.rotation * Quaternion.Euler(32f, 0f, 0f);
                    realSteeringWheel.SetParent(pivotGo.transform, true);
                    ctrl.steeringWheelVisual = pivotGo.transform;
                    ctrl.steeringWheelSpinAxis = Vector3.up;
                }

                // Kenyan ENA Coach LED Destination Strip & Yellow License Plates (KDK 057M)
                CreateBox("EnaLedBoardBacking", body, new Vector3(0f, 3.24f, 7.12f), new Vector3(2.12f, 0.28f, 0.08f), matBlack, false);
                CreateWorldSignText("EnaLedRouteTxt", body, "ENA COACH  •  KDK 057M  •  EXPRESS", new Vector3(0f, 3.24f, 7.18f), new Vector2(2.05f, 0.26f), 1.45f, Palette.UiAccentAmber, Quaternion.Euler(0f, 180f, 0f), noWrap: true);
                CreateBox("FrontPlate", body, new Vector3(0f, 0.56f, 7.45f), new Vector3(0.68f, 0.19f, 0.05f), matPlate, false);
                CreateWorldSignText("FrontPlateTxt", body, "KDK 057M", new Vector3(0f, 0.56f, 7.49f), new Vector2(0.66f, 0.18f), 1.3f, Palette.EnaStripeBlack, Quaternion.Euler(0f, 180f, 0f), noWrap: true);
                CreateBox("RearPlate", body, new Vector3(0f, 0.72f, -7.45f), new Vector3(0.72f, 0.20f, 0.05f), matPlate, false);
                CreateWorldSignText("RearPlateTxt", body, "KDK 057M", new Vector3(0f, 0.72f, -7.49f), new Vector2(0.70f, 0.18f), 1.3f, Palette.EnaStripeBlack, Quaternion.identity, noWrap: true);

                // Headlights, Indicators & Rear Taillights wired cleanly to Q/E/Z & L keys
                var headL = CreateBox("HeadlightGlowL", body, new Vector3(-0.98f, 1.12f, 7.28f), new Vector3(0.38f, 0.18f, 0.06f), matHead, false);
                var headR = CreateBox("HeadlightGlowR", body, new Vector3( 0.98f, 1.12f, 7.28f), new Vector3(0.38f, 0.18f, 0.06f), matHead, false);
                var indFL = CreateBox("IndFrontL", body, new Vector3(-1.22f, 1.12f, 7.16f), new Vector3(0.14f, 0.18f, 0.08f), matIndicator, false);
                var indFR = CreateBox("IndFrontR", body, new Vector3( 1.22f, 1.12f, 7.16f), new Vector3(0.14f, 0.18f, 0.08f), matIndicator, false);
                var indRL = CreateBox("IndRearL", body, new Vector3(-1.12f, 1.05f, -7.40f), new Vector3(0.24f, 0.22f, 0.06f), matIndicator, false);
                var indRR = CreateBox("IndRearR", body, new Vector3( 1.12f, 1.05f, -7.40f), new Vector3(0.24f, 0.22f, 0.06f), matIndicator, false);
                var tailL = CreateBox("BrakeGlowL", body, new Vector3(-1.12f, 1.48f, -7.40f), new Vector3(0.24f, 0.45f, 0.06f), matTail, false);
                var tailR = CreateBox("BrakeGlowR", body, new Vector3( 1.12f, 1.48f, -7.40f), new Vector3(0.24f, 0.45f, 0.06f), matTail, false);
                brakeRenderers.Add(tailL.GetComponent<Renderer>());
                brakeRenderers.Add(tailR.GetComponent<Renderer>());

                var spotL = CreateHeadlightSpot(root.transform, "HeadlightBeamL", new Vector3(-0.98f, 1.12f, 7.42f));
                var spotR = CreateHeadlightSpot(root.transform, "HeadlightBeamR", new Vector3( 0.98f, 1.12f, 7.42f));
                ctrl.headlightSpots = new[] { spotL, spotR };
                ctrl.headlightRenderers = new[] { headL.GetComponent<Renderer>(), headR.GetComponent<Renderer>() };
                ctrl.brakeLightRenderers = brakeRenderers.ToArray();
                ctrl.leftIndicatorRenderers = new[] { indFL.GetComponent<Renderer>(), indRL.GetComponent<Renderer>() };
                ctrl.rightIndicatorRenderers = new[] { indFR.GetComponent<Renderer>(), indRR.GetComponent<Renderer>() };

                // REAL SCANIA DRIVER SEAT COCKPIT CAMERA (Press C -> See Real 3D Scania Dashboard, Steering Wheel & BOTH Live Side Mirrors!)
                var cockpitCam = new GameObject("CockpitCameraAnchor").transform;
                cockpitCam.SetParent(root.transform, false);
                cockpitCam.localPosition = new Vector3(-0.62f, 2.18f, 5.74f);
                cockpitCam.localRotation = Quaternion.Euler(9.0f, 0f, 0f);
                ctrl.cockpitCameraAnchor = cockpitCam;

                // Sleek compact Live 3D Dashboard Telemetry Strip tucked cleanly on center dash beside the analog cluster!
                var dashPod = new GameObject("ScaniaLiveDashTelemetry").transform;
                dashPod.SetParent(body, false);
                dashPod.localPosition = new Vector3(-0.08f, 1.79f, 6.72f);
                dashPod.localRotation = Quaternion.Euler(14f, -12f, 0f);
                CreateBox("DashPodBezel", dashPod, Vector3.zero, new Vector3(0.38f, 0.11f, 0.025f), matBlack, false);
                var dashSpeedTmp = CreateWorldSignText("DashSpeedTxt", dashPod, "00 km/h | G:N", new Vector3(0f, 0.024f, -0.018f), new Vector2(0.36f, 0.055f), 0.34f, Palette.UiAccentGreen, Quaternion.identity, noWrap: true);
                var dashStatusTmp = CreateWorldSignText("DashStatusTxt", dashPod, "ENA GPS OK\nNAIROBI CBD", new Vector3(0f, -0.025f, -0.018f), new Vector2(0.36f, 0.055f), 0.22f, Palette.UiAccentAmber, Quaternion.identity, noWrap: false);

                // Real 3D Analog Speedo/RPM Cluster (centered cleanly through upper steering wheel!) + Dual Animated 3D Windshield Wipers + Animated Left Pneumatic Passenger Door!
                var matEnaDoorAccent = GetMat("EnaSkirtRedGloss", Palette.EnaSkirtRed, 0.72f, 0.10f);
                var matEnaClearGlass = GetTransparentGlassMat("ScaniaCrystalWindshield", new Color(0.86f, 0.94f, 1f, 0.14f));
                KenyaVehicleFeaturesBuilder.AttachRealDashWipersAndDoor(
                    root, body, body, new Vector3(-0.60f, 1.75f, 6.64f),
                    body, new Vector3(0f, 1.72f, 7.20f), 0.54f, 0.58f,
                    halfW: 1.28f, halfL: 7.28f, isCoach: true, isPoliceLC79: true, // isPoliceLC79=true suppresses extra cowl box since Scania has its own real 3D nose!
                    matBlack, matChrome, matEnaDoorAccent, matEnaClearGlass);

                // BOTH WORKING LIVE REAR-VIEW SIDE MIRRORS (Exterior Mirrors + Scania 2026 Left & Right A-Pillar Digital Mirror Monitors!)
                var mirrorLGlass = BuildLiveSideMirrorHousing(body, "LiveMirrorLeft",  new Vector3(-1.58f, 2.06f, 6.90f), new Vector3(-1.26f, 2.16f, 6.72f), new Vector3(0f, 24f, 0f),  new Vector2(0.24f, 0.48f), matBlack, matChrome);
                var mirrorRGlass = BuildLiveSideMirrorHousing(body, "LiveMirrorRight", new Vector3( 1.50f, 2.06f, 7.50f), new Vector3( 1.24f, 2.16f, 7.05f), new Vector3(0f, -26f, 0f), new Vector2(0.26f, 0.50f), matBlack, matChrome);
                // Scania 2026 Interior A-Pillar Digital Mirror Monitors hugging Left & Right A-pillars cleanly inside driver's windshield FOV!
                var digiLGlass = BuildDigitalPillarMirror(body, "ScaniaDigitalMirrorLeft",  new Vector3(-1.14f, 2.04f, 6.62f), new Vector3(0f,  26f, 0f), new Vector2(0.20f, 0.40f), matBlack, matChrome);
                var digiRGlass = BuildDigitalPillarMirror(body, "ScaniaDigitalMirrorRight", new Vector3( 0.96f, 2.04f, 6.66f), new Vector3(0f, -34f, 0f), new Vector2(0.22f, 0.42f), matBlack, matChrome);

                var mirrorCamL = new GameObject("MirrorCamAnchorL").transform;
                mirrorCamL.SetParent(root.transform, false);
                mirrorCamL.localPosition = new Vector3(-1.54f, 2.08f, 6.70f);
                mirrorCamL.localRotation = Quaternion.Euler(4f, 173f, 0f);

                var mirrorCamR = new GameObject("MirrorCamAnchorR").transform;
                mirrorCamR.SetParent(root.transform, false);
                mirrorCamR.localPosition = new Vector3(1.50f, 2.08f, 7.12f);
                mirrorCamR.localRotation = Quaternion.Euler(4f, 187f, 0f);

                var mirrors = root.AddComponent<KenyaVehicleMirrors>();
                mirrors.leftMirrorSurfaces = new[] { mirrorLGlass, digiLGlass };
                mirrors.rightMirrorSurfaces = new[] { mirrorRGlass, digiRGlass };
                mirrors.leftMirrorCameraAnchor = mirrorCamL;
                mirrors.rightMirrorCameraAnchor = mirrorCamR;
                mirrors.dashSpeedText = dashSpeedTmp;
                mirrors.dashStatusText = dashStatusTmp;

                var dashCam = new GameObject("DashcamAnchor").transform;
                dashCam.SetParent(root.transform, false);
                dashCam.localPosition = new Vector3(0f, 2.05f, 7.52f);
                dashCam.localRotation = Quaternion.Euler(2f, 0f, 0f);
                ctrl.bumperCameraAnchor = dashCam;

                var axleCam = new GameObject("AxleCameraAnchor").transform;
                axleCam.SetParent(root.transform, false);
                axleCam.localPosition = new Vector3(-2.15f, 0.98f, 2.2f);
                axleCam.localRotation = Quaternion.Euler(4f, 14f, 0f);
                ctrl.axleCameraAnchor = axleCam;

                // Scania Touring 6x2 Tri-Axle WheelColliders matching exact GLB wheel arches!
                float[] axleZs = { 4.59f, -2.41f, -3.91f };
                bool[] steerFlags = { true, false, false };
                bool[] driveFlags = { false, true, true };
                ctrl.wheels = BuildWheelColliderSet(root.transform, axleZs, steerFlags, driveFlags, 1.06f, 0.52f, 0.24f, matTire, matRim);
                return ctrl;
            }

            // Fallback sculpted bus if Scania GLB is ever removed
            var fbChassis = root.AddComponent<BoxCollider>();
            fbChassis.center = new Vector3(0f, 1.98f, 0f);
            fbChassis.size = new Vector3(2.48f, 2.68f, 11.55f);
            var fbWhite = GetMat("EnaBodyPearlGloss", Palette.EnaBodyWhite, 0.72f, 0.08f);
            var fbRed = GetMat("EnaSkirtRedGloss", Palette.EnaSkirtRed, 0.72f, 0.10f);
            CreateCustomMesh("SculptedUpperBody", body, Vector3.zero, Quaternion.identity, Vector3.one, KenyaSculptedMeshFactory.EnaCoachUpperMesh, fbWhite, false);
            CreateCustomMesh("SculptedLowerSkirt", body, Vector3.zero, Quaternion.identity, Vector3.one, KenyaSculptedMeshFactory.EnaCoachSkirtMesh, fbRed, false);
            float[] fbAxles = { 4.25f, 2.75f, -2.55f, -4.05f };
            ctrl.wheels = BuildWheelColliderSet(root.transform, fbAxles, new[] { true, true, false, false }, new[] { false, false, true, true }, 1.18f, 0.50f, 0.24f, matTire, matRim);
            return ctrl;
        }

        private static Renderer BuildLiveSideMirrorHousing(Transform parent, string name, Vector3 headPos, Vector3 armRootPos, Vector3 headEuler, Vector2 glassSize, Material matHousing, Material matChrome)
        {
            var holder = new GameObject(name).transform;
            holder.SetParent(parent, false);
            holder.localPosition = headPos;
            holder.localRotation = Quaternion.Euler(headEuler);

            // Sleek mounting arm linking BACK of mirror housing cleanly to coach/truck A-pillar (never crosses front glass!)
            Vector3 backOfHousing = headPos + Quaternion.Euler(headEuler) * new Vector3(0f, 0f, 0.08f);
            Vector3 toRoot = armRootPos - backOfHousing;
            var arm = CreateBox("MirrorArm", parent, (backOfHousing + armRootPos) * 0.5f, new Vector3(0.055f, 0.055f, toRoot.magnitude), matHousing, false);
            if (toRoot.sqrMagnitude > 0.001f) arm.transform.localRotation = Quaternion.LookRotation(toRoot.normalized, Vector3.up);

            // Outer mirror housing + bezel
            CreateBox("HousingShell", holder, new Vector3(0f, 0f, 0.045f), new Vector3(glassSize.x + 0.05f, glassSize.y + 0.06f, 0.07f), matHousing, false);
            // Front-facing (driver-facing -Z) Quad for crisp live RenderTexture reflection!
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "MirrorLiveGlass";
            quad.transform.SetParent(holder, false);
            quad.transform.localPosition = new Vector3(0f, 0f, 0.005f);
            quad.transform.localRotation = Quaternion.identity; // Quad front normal points along -Z (facing driver!)
            quad.transform.localScale = new Vector3(glassSize.x, glassSize.y, 1f);
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            var r = quad.GetComponent<Renderer>();
            r.sharedMaterial = matChrome;
            return r;
        }

        private static Renderer BuildDigitalPillarMirror(Transform parent, string name, Vector3 pos, Vector3 euler, Vector2 screenSize, Material matBezel, Material matChrome)
        {
            var holder = new GameObject(name).transform;
            holder.SetParent(parent, false);
            holder.localPosition = pos;
            holder.localRotation = Quaternion.Euler(euler);
            CreateBox("ScreenBezel", holder, new Vector3(0f, 0f, 0.025f), new Vector3(screenSize.x + 0.04f, screenSize.y + 0.05f, 0.035f), matBezel, false);
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "DigitalMirrorGlass";
            quad.transform.SetParent(holder, false);
            quad.transform.localPosition = new Vector3(0f, 0f, 0.002f);
            quad.transform.localRotation = Quaternion.identity;
            quad.transform.localScale = new Vector3(screenSize.x, screenSize.y, 1f);
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            var r = quad.GetComponent<Renderer>();
            r.sharedMaterial = matChrome;
            return r;
        }

        /// <summary>
        /// Universal auto-aligning wrapper for any imported 3D Kenyan bus/truck GLB/FBX mod
        /// (Isuzu FRR Kenyan, Isuzu TX Lorry, Marcopolo G7 Volvo, etc.).
        /// Automatically aligns longest axis along +Z, grounds bottom tires to Y=0, scales to exact
        /// real-world Kenyan length, attaches WheelColliders, Kenyan plates, working headlights/blinkers,
        /// AND a live [C] Cockpit Dashboard + BOTH working live Left & Right Side Mirrors!
        /// </summary>
        private static KenyaStreetVehicle BuildImportedModVehicle(
            Transform parent, Vector3 spawnPos, string assetPath,
            string goName, string displayName, string plateNum,
            bool isCoach, float targetLength, float massKg, float motorTorque, float maxSpeedKmh,
            float extraYawDeg,
            Material matTire, Material matRim, Material matHead, Material matTail, Material matInd,
            Material matPlate, Material matInterior, Material matBlack, Material matChrome)
        {
            var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (modelAsset == null) return null;

            var root = new GameObject(goName);
            root.transform.SetParent(parent, false);
            root.transform.position = spawnPos;

            var rb = root.AddComponent<Rigidbody>();
            rb.mass = massKg;
#if UNITY_6000_0_OR_NEWER
            rb.linearDamping = 0.03f;
            rb.angularDamping = 0.35f;
#else
            rb.drag = 0.03f;
            rb.angularDrag = 0.35f;
#endif

            var ctrl = root.AddComponent<KenyaStreetVehicle>();
            ctrl.displayName = displayName;
            ctrl.plateNumber = plateNum;
            ctrl.isCoach = isCoach;
            ctrl.motorTorque = motorTorque;
            ctrl.brakeTorque = motorTorque * 1.9f;
            ctrl.maxSpeedKmh = maxSpeedKmh;
            ctrl.centerOfMassOffset = new Vector3(0f, -0.55f, 0.10f);

            var body = new GameObject("VisualBodyRoot").transform;
            body.SetParent(root.transform, false);

            // Wrap inside a clean alignment pivot so prefab root rotations (e.g. 270° X-up GLBs) are preserved intact!
            var modelPivot = new GameObject($"{goName}_AlignPivot").transform;
            modelPivot.SetParent(body, false);
            modelPivot.localPosition = Vector3.zero;
            modelPivot.localRotation = Quaternion.identity;
            modelPivot.localScale = Vector3.one;

            var inst = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset);
            inst.name = $"{goName}_Model";
            inst.transform.SetParent(modelPivot, false);

            // Hide shadow planes & Marcopolo side plinth outliers + make front glass transparent BEFORE measuring bounds
            bool isMarcopoloG7 = assetPath.IndexOf("Marcopolo", System.StringComparison.OrdinalIgnoreCase) >= 0;
            var matClearGlass = GetTransparentGlassMat("ScaniaCrystalWindshield", new Color(0.86f, 0.94f, 1f, 0.08f));
            foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
            {
                string rn = r.name.ToLowerInvariant();
                if (rn.Contains("shadow") || rn == "plane" || (isMarcopoloG7 && (r.name == "Object_22" || r.name == "Object_83")))
                {
                    r.gameObject.SetActive(false);
                    continue;
                }
                var mats = r.sharedMaterials;
                bool changed = false;
                for (int mi = 0; mi < mats.Length; mi++)
                {
                    if (mats[mi] == null) continue;
                    string mn = mats[mi].name.ToLowerInvariant();
                    if (mn.Contains("vidro") || mn.Contains("windshield") || mn.Contains("parabrisa"))
                    {
                        mats[mi] = matClearGlass;
                        changed = true;
                    }
                }
                if (changed) r.sharedMaterials = mats;
            }

            // Measure natural imported bounds, align forward axis along +Z, scale to exact Kenyan length, center X/Z and ground Y=0!
            Bounds rawB = MeasureRenderers(modelPivot);
            float autoYaw = (rawB.size.x > rawB.size.z * 1.15f) ? 90f : 0f;
            modelPivot.localRotation = Quaternion.Euler(0f, autoYaw + extraYawDeg, 0f);
            Bounds rotB = MeasureRenderers(modelPivot);
            float curLen = Mathf.Max(0.5f, rotB.size.z);
            float scale = targetLength / curLen;
            modelPivot.localScale = Vector3.one * scale;
            Bounds finalB = MeasureRenderers(modelPivot);
            // Convert world-space Bounds center/min into body-local space!
            // If converted from an ETS2 .scs mod (contains ETS2_LOC_wheel_* locators), ground tire bottoms at Y=0!
            Vector3 relCenter = finalB.center - body.position;
            float relMinY = finalB.min.y - body.position.y;
            var ets2WheelLocs = new List<Transform>();
            foreach (var t in inst.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.StartsWith("ETS2_LOC_wheel_")) ets2WheelLocs.Add(t);
            }
            bool hasEts2WheelLocs = ets2WheelLocs.Count >= 2;
            float ets2WheelRadius = isCoach ? 0.50f : 0.46f;
            float yShift = -relMinY;
            if (hasEts2WheelLocs)
            {
                float avgHubRelY = 0f;
                for (int w = 0; w < ets2WheelLocs.Count; w++) avgHubRelY += (ets2WheelLocs[w].position.y - body.position.y);
                avgHubRelY /= ets2WheelLocs.Count;
                ets2WheelRadius = Mathf.Clamp(avgHubRelY, 0.38f, 0.56f);
                yShift = ets2WheelRadius - avgHubRelY;
            }
            modelPivot.localPosition = new Vector3(-relCenter.x, yShift, -relCenter.z);
            Bounds aligned = MeasureRenderers(modelPivot);

            float halfW = Mathf.Clamp(aligned.size.x * 0.5f, 1.0f, 1.38f);
            float fullH = Mathf.Clamp(aligned.size.y, 2.5f, 4.0f);
            float halfL = Mathf.Clamp(aligned.size.z * 0.5f, 3.4f, 7.2f);

            var chassisCol = root.AddComponent<BoxCollider>();
            chassisCol.center = new Vector3(0f, fullH * 0.52f, 0f);
            chassisCol.size = new Vector3(halfW * 1.92f, fullH * 0.78f, halfL * 1.94f);

            // Kenyan Yellow Plates + Headlights + Blinkers + Taillights
            CreateBox("FrontPlate", body, new Vector3(0f, 0.65f, halfL + 0.03f), new Vector3(0.64f, 0.18f, 0.04f), matPlate, false);
            CreateWorldSignText("FrontPlateTxt", body, plateNum, new Vector3(0f, 0.65f, halfL + 0.06f), new Vector2(0.62f, 0.17f), 1.2f, Palette.EnaStripeBlack, Quaternion.Euler(0f, 180f, 0f), noWrap: true);
            CreateBox("RearPlate", body, new Vector3(0f, 0.76f, -halfL - 0.03f), new Vector3(0.68f, 0.19f, 0.04f), matPlate, false);
            CreateWorldSignText("RearPlateTxt", body, plateNum, new Vector3(0f, 0.76f, -halfL - 0.06f), new Vector2(0.66f, 0.18f), 1.2f, Palette.EnaStripeBlack, Quaternion.identity, noWrap: true);

            var headL = CreateBox("HeadL", body, new Vector3(-halfW * 0.74f, 0.98f, halfL + 0.01f), new Vector3(0.32f, 0.18f, 0.05f), matHead, false);
            var headR = CreateBox("HeadR", body, new Vector3( halfW * 0.74f, 0.98f, halfL + 0.01f), new Vector3(0.32f, 0.18f, 0.05f), matHead, false);
            var indFL = CreateBox("IndFL", body, new Vector3(-halfW * 0.92f, 0.98f, halfL - 0.04f), new Vector3(0.14f, 0.18f, 0.06f), matInd, false);
            var indFR = CreateBox("IndFR", body, new Vector3( halfW * 0.92f, 0.98f, halfL - 0.04f), new Vector3(0.14f, 0.18f, 0.06f), matInd, false);
            var tailL = CreateBox("TailL", body, new Vector3(-halfW * 0.80f, 1.05f, -halfL - 0.01f), new Vector3(0.28f, 0.28f, 0.05f), matTail, false);
            var tailR = CreateBox("TailR", body, new Vector3( halfW * 0.80f, 1.05f, -halfL - 0.01f), new Vector3(0.28f, 0.28f, 0.05f), matTail, false);
            var indRL = CreateBox("IndRL", body, new Vector3(-halfW * 0.80f, 0.80f, -halfL - 0.01f), new Vector3(0.24f, 0.14f, 0.05f), matInd, false);
            var indRR = CreateBox("IndRR", body, new Vector3( halfW * 0.80f, 0.80f, -halfL - 0.01f), new Vector3(0.24f, 0.14f, 0.05f), matInd, false);

            var spotL = CreateHeadlightSpot(root.transform, "HeadlightBeamL", new Vector3(-halfW * 0.74f, 0.98f, halfL + 0.12f));
            var spotR = CreateHeadlightSpot(root.transform, "HeadlightBeamR", new Vector3( halfW * 0.74f, 0.98f, halfL + 0.12f));
            ctrl.headlightSpots = new[] { spotL, spotR };
            ctrl.headlightRenderers = new[] { headL.GetComponent<Renderer>(), headR.GetComponent<Renderer>() };
            ctrl.brakeLightRenderers = new[] { tailL.GetComponent<Renderer>(), tailR.GetComponent<Renderer>() };
            ctrl.leftIndicatorRenderers = new[] { indFL.GetComponent<Renderer>(), indRL.GetComponent<Renderer>() };
            ctrl.rightIndicatorRenderers = new[] { indFR.GetComponent<Renderer>(), indRR.GetComponent<Renderer>() };

            // Clean ETS2-style [C] RHD Cockpit Overlay Rig (ONLY visible in [C] Cockpit Mode so real 3D exteriors stay 100% pristine!)
            // Placed cleanly just ahead of opaque imported windshields so pressing [C] ALWAYS delivers a crystal-clear unobstructed
            // highway view + RHD Dashboard + live Telemetry + spinning Steering Wheel + BOTH working live Left & Right Side Mirrors!
            float cabEyeY = isCoach ? 2.42f : 2.22f;
            float camZ = halfL + 0.16f;
            float cabFrontZ = camZ + 0.64f;
            var cockpitRig = new GameObject("ModCockpitDashboardRig").transform;
            cockpitRig.SetParent(body, false);
            cockpitRig.localPosition = new Vector3(0f, cabEyeY - 0.54f, cabFrontZ);

            CreateBox("ModDashDeck", cockpitRig, new Vector3(0f, 0f, 0.12f), new Vector3(halfW * 1.72f, 0.34f, 0.46f), matInterior, false);
            CreateBox("ModDashHood", cockpitRig, new Vector3(0.42f, 0.28f, 0.08f), new Vector3(0.56f, 0.22f, 0.28f), matBlack, false);
            var modSpeedTmp = CreateWorldSignText("ModDashSpeed", cockpitRig, "00 km/h | G:N", new Vector3(0.42f, 0.36f, -0.07f), new Vector2(0.48f, 0.06f), 0.34f, Palette.UiAccentGreen, Quaternion.identity, noWrap: true);
            var modStatusTmp = CreateWorldSignText("ModDashStatus", cockpitRig, $"{plateNum} • KENYA GPS", new Vector3(0.42f, 0.31f, -0.07f), new Vector2(0.48f, 0.05f), 0.22f, Palette.UiAccentAmber, Quaternion.identity, noWrap: true);

            // Real Analog Speedo/RPM Needles + Visible Front Nose Cowl + Sweeping 3D Wipers + Animated Left Curb Passenger Door!
            var matModAccent = isCoach
                ? GetMat("EnaSkirtRedGloss", Palette.EnaSkirtRed, 0.72f, 0.10f)
                : GetMat("IsuzuDoorBlue", Palette.Hex("#1A5296"), 0.65f, 0.15f);
            KenyaVehicleFeaturesBuilder.AttachRealDashWipersAndDoor(
                root, body, cockpitRig, new Vector3(0.42f, 0.20f, -0.075f),
                cockpitRig, new Vector3(0.10f, 0.22f, 0.38f), 0.44f, 0.58f,
                halfW: halfW, halfL: halfL, isCoach: isCoach, isPoliceLC79: false,
                matBlack, matChrome, matModAccent, matClearGlass);

            var steerPivot = new GameObject("SteeringWheelPivot").transform;
            steerPivot.SetParent(cockpitRig, false);
            steerPivot.localPosition = new Vector3(0.42f, 0.11f, -0.22f);
            steerPivot.localRotation = Quaternion.Euler(64f, 0f, 0f);
            for (int seg = 0; seg < 12; seg++)
            {
                float a = seg * 30f * Mathf.Deg2Rad;
                var rimSeg = CreateBox($"SteerRim_{seg}", steerPivot, new Vector3(Mathf.Cos(a) * 0.20f, 0f, Mathf.Sin(a) * 0.20f), new Vector3(0.034f, 0.034f, 0.11f), matBlack, false);
                rimSeg.transform.localRotation = Quaternion.Euler(0f, -seg * 30f, 0f);
            }
            CreateCylinder("SteerHub", steerPivot, Vector3.zero, new Vector3(0.13f, 0.032f, 0.13f), matBlack);
            CreateBox("SteerSpokeH", steerPivot, Vector3.zero, new Vector3(0.38f, 0.026f, 0.038f), matChrome, false);
            CreateBox("SteerSpokeV", steerPivot, new Vector3(0f, 0f, -0.09f), new Vector3(0.038f, 0.026f, 0.18f), matChrome, false);
            ctrl.steeringWheelVisual = steerPivot;
            ctrl.steeringWheelSpinAxis = Vector3.up;

            // BOTH Working Live Left & Right Side Mirrors angled cleanly toward the RHD driver's eyes!
            var modMirL = BuildLiveSideMirrorHousing(cockpitRig, "ModMirrorL",
                new Vector3(-halfW * 0.56f, 0.54f, 0.34f),
                new Vector3(-halfW * 0.78f, 0.54f, 0.16f),
                new Vector3(0f, -32f, 0f), new Vector2(0.24f, 0.42f), matBlack, matChrome);
            var modMirR = BuildLiveSideMirrorHousing(cockpitRig, "ModMirrorR",
                new Vector3( halfW * 0.94f, 0.54f, 0.24f),
                new Vector3( halfW * 0.80f, 0.54f, 0.12f),
                new Vector3(0f,  26f, 0f), new Vector2(0.24f, 0.42f), matBlack, matChrome);

            var camL = new GameObject("ModMirrorCamL").transform;
            camL.SetParent(root.transform, false);
            camL.localPosition = new Vector3(-halfW - 0.26f, cabEyeY - 0.04f, halfL - 0.20f);
            camL.localRotation = Quaternion.Euler(4f, 173f, 0f);
            var camR = new GameObject("ModMirrorCamR").transform;
            camR.SetParent(root.transform, false);
            camR.localPosition = new Vector3( halfW + 0.26f, cabEyeY - 0.04f, halfL - 0.20f);
            camR.localRotation = Quaternion.Euler(4f, 187f, 0f);

            var mirrors = root.AddComponent<KenyaVehicleMirrors>();
            mirrors.leftMirrorSurfaces = new[] { modMirL };
            mirrors.rightMirrorSurfaces = new[] { modMirR };
            mirrors.leftMirrorCameraAnchor = camL;
            mirrors.rightMirrorCameraAnchor = camR;
            mirrors.dashSpeedText = modSpeedTmp;
            mirrors.dashStatusText = modStatusTmp;
            mirrors.cockpitOnlyRig = cockpitRig.gameObject;
            cockpitRig.gameObject.SetActive(false);

            var cockpitCam = new GameObject("CockpitCameraAnchor").transform;
            cockpitCam.SetParent(root.transform, false);
            cockpitCam.localPosition = new Vector3(0.42f, cabEyeY, camZ);
            cockpitCam.localRotation = Quaternion.Euler(7.5f, 0f, 0f);
            ctrl.cockpitCameraAnchor = cockpitCam;

            var dashCam = new GameObject("DashcamAnchor").transform;
            dashCam.SetParent(root.transform, false);
            dashCam.localPosition = new Vector3(0f, cabEyeY - 0.18f, halfL + 0.20f);
            ctrl.bumperCameraAnchor = dashCam;

            var axleCam = new GameObject("AxleCameraAnchor").transform;
            axleCam.SetParent(root.transform, false);
            axleCam.localPosition = new Vector3(-halfW - 0.85f, 0.95f, halfL * 0.25f);
            axleCam.localRotation = Quaternion.Euler(4f, 14f, 0f);
            ctrl.axleCameraAnchor = axleCam;

            // Tuned WheelColliders: for converted ETS2 .scs mods with ETS2_LOC_wheel_* locators, snap real spinning PBR
            // bus/truck wheels to the EXACT ETS2 axle coordinates & keep WheelVisuals visible!
            float[] axleZs;
            bool[] steerFlags;
            bool[] driveFlags;
            float wheelHalfTrack = halfW * 0.88f;
            float wheelRad = isCoach ? 0.50f : 0.46f;
            if (hasEts2WheelLocs)
            {
                var frontZs = new List<float>();
                var rearZs = new List<float>();
                float sumAbsX = 0f;
                for (int w = 0; w < ets2WheelLocs.Count; w++)
                {
                    Vector3 lp = root.transform.InverseTransformPoint(ets2WheelLocs[w].position);
                    sumAbsX += Mathf.Abs(lp.x);
                    if (ets2WheelLocs[w].name.Contains("wheel_f") || lp.z > 0f) frontZs.Add(lp.z);
                    else rearZs.Add(lp.z);
                }
                wheelHalfTrack = Mathf.Clamp(sumAbsX / ets2WheelLocs.Count, 0.78f, halfW * 0.96f);
                wheelRad = ets2WheelRadius;
                float fz = frontZs.Count > 0 ? AverageList(frontZs) : halfL * 0.55f;
                float rz = rearZs.Count > 0 ? AverageList(rearZs) : -halfL * 0.45f;
                axleZs = new[] { fz, rz };
                steerFlags = new[] { true, false };
                driveFlags = new[] { false, true };
            }
            else
            {
                axleZs = isCoach
                    ? new[] { halfL * 0.62f, -halfL * 0.36f, -halfL * 0.56f }
                    : new[] { halfL * 0.64f, -halfL * 0.42f, -halfL * 0.66f };
                steerFlags = new[] { true, false, false };
                driveFlags = new[] { false, true, true };
            }
            var entries = BuildWheelColliderSet(root.transform, axleZs, steerFlags, driveFlags, wheelHalfTrack, wheelRad, 0.22f, matTire, matRim);
            // Hide fallback cylinder wheels ONLY for Sketchfab GLBs that already bake static wheels into the mesh!
            if (!hasEts2WheelLocs)
            {
                var wVisRoot = root.transform.Find("WheelVisuals");
                if (wVisRoot != null) wVisRoot.gameObject.SetActive(false);
            }
            ctrl.wheels = entries;
            return ctrl;
        }

        private static float AverageList(List<float> vals)
        {
            if (vals == null || vals.Count == 0) return 0f;
            float s = 0f;
            for (int i = 0; i < vals.Count; i++) s += vals[i];
            return s / vals.Count;
        }

        private static Bounds MeasureRenderers(Transform root)
        {
            Bounds b = new Bounds(root.position, Vector3.one);
            bool has = false;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (!r.gameObject.activeInHierarchy) continue;
                string rn = r.name.ToLowerInvariant();
                if (rn.Contains("shadow") || rn == "plane") continue;
                if (!has) { b = r.bounds; has = true; }
                else b.Encapsulate(r.bounds);
            }
            return b;
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
            var matClearWindshield = GetTransparentGlassMat("EnaPanoramicWindshield", Palette.WithAlpha(Palette.EnaGlassDark, 0.22f));
            var ws = CreateBox("CabWindshield", body, new Vector3(0f, 2.22f, 4.10f), new Vector3(1.98f, 0.88f, 0.05f), matClearWindshield, false);
            ws.transform.localRotation = Quaternion.Euler(-14f, 0f, 0f);
            CreateBox("CabSideGlassL", body, new Vector3(-1.14f, 2.15f, 3.05f), new Vector3(0.04f, 0.80f, 1.38f), matClearWindshield, false);
            CreateBox("CabSideGlassR", body, new Vector3( 1.14f, 2.15f, 3.05f), new Vector3(0.04f, 0.80f, 1.38f), matClearWindshield, false);
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

            var matBlack = GetMat("EnaStripeBlackGloss", Palette.EnaStripeBlack, 0.75f, 0.12f);
            CreateBox("RhdDash", body, new Vector3(0f, 1.72f, 3.74f), new Vector3(2.06f, 0.42f, 0.62f), matInterior, false);
            CreateBox("DashHood", body, new Vector3(0.56f, 1.98f, 3.68f), new Vector3(0.62f, 0.22f, 0.34f), matBlack, false);
            CreateBox("DashCluster", body, new Vector3(0.56f, 1.96f, 3.50f), new Vector3(0.50f, 0.15f, 0.03f), matBlack, false);
            var isuzuSpeedTmp = CreateWorldSignText("IsuzuDashSpeed", body, "00 km/h | G:N", new Vector3(0.56f, 1.99f, 3.47f), new Vector2(0.46f, 0.08f), 0.42f, Palette.UiAccentGreen, Quaternion.identity, noWrap: true);
            var isuzuStatusTmp = CreateWorldSignText("IsuzuDashStatus", body, "EPH FRR 90\nKENYA GPS", new Vector3(0.56f, 1.92f, 3.47f), new Vector2(0.46f, 0.08f), 0.28f, Palette.UiAccentAmber, Quaternion.identity, noWrap: false);

            var steerPivot = new GameObject("SteeringWheelPivot").transform;
            steerPivot.SetParent(body, false);
            steerPivot.localPosition = new Vector3(0.56f, 1.82f, 3.32f);
            steerPivot.localRotation = Quaternion.Euler(64f, 0f, 0f);
            // Realistic hollow 3-spoke truck steering wheel visibly rotating with A/D
            for (int seg = 0; seg < 12; seg++)
            {
                float a = seg * 30f * Mathf.Deg2Rad;
                var rimSeg = CreateBox($"SteerRim_{seg}", steerPivot, new Vector3(Mathf.Cos(a) * 0.21f, 0f, Mathf.Sin(a) * 0.21f), new Vector3(0.036f, 0.036f, 0.12f), matBlack, false);
                rimSeg.transform.localRotation = Quaternion.Euler(0f, -seg * 30f, 0f);
            }
            CreateCylinder("SteerCenterHub", steerPivot, Vector3.zero, new Vector3(0.14f, 0.035f, 0.14f), matBlack);
            CreateBox("SteerSpokeH", steerPivot, Vector3.zero, new Vector3(0.40f, 0.028f, 0.04f), matChrome, false);
            CreateBox("SteerSpokeV", steerPivot, new Vector3(0f, 0f, -0.10f), new Vector3(0.04f, 0.028f, 0.20f), matChrome, false);
            ctrl.steeringWheelVisual = steerPivot;
            ctrl.steeringWheelSpinAxis = Vector3.up;

            // Working Live Left & Right Side Mirrors on Isuzu FRR 90 (clearly visible when pressing C!)
            var isuzuMirrorL = BuildLiveSideMirrorHousing(body, "IsuzuMirrorL", new Vector3(-1.34f, 2.18f, 4.42f), new Vector3(-1.14f, 2.26f, 4.10f), new Vector3(0f, 26f, 0f),  new Vector2(0.24f, 0.46f), matBlack, matChrome);
            var isuzuMirrorR = BuildLiveSideMirrorHousing(body, "IsuzuMirrorR", new Vector3( 1.36f, 2.18f, 4.18f), new Vector3( 1.14f, 2.26f, 4.08f), new Vector3(0f, -24f, 0f), new Vector2(0.24f, 0.46f), matBlack, matChrome);
            var isuzuCamL = new GameObject("IsuzuMirrorCamL").transform;
            isuzuCamL.SetParent(root.transform, false);
            isuzuCamL.localPosition = new Vector3(-1.38f, 2.18f, 4.20f);
            isuzuCamL.localRotation = Quaternion.Euler(4f, 173f, 0f);
            var isuzuCamR = new GameObject("IsuzuMirrorCamR").transform;
            isuzuCamR.SetParent(root.transform, false);
            isuzuCamR.localPosition = new Vector3(1.38f, 2.18f, 4.00f);
            isuzuCamR.localRotation = Quaternion.Euler(4f, 187f, 0f);
            var isuzuMirrors = root.AddComponent<KenyaVehicleMirrors>();
            isuzuMirrors.leftMirrorSurfaces = new[] { isuzuMirrorL };
            isuzuMirrors.rightMirrorSurfaces = new[] { isuzuMirrorR };
            isuzuMirrors.leftMirrorCameraAnchor = isuzuCamL;
            isuzuMirrors.rightMirrorCameraAnchor = isuzuCamR;
            isuzuMirrors.dashSpeedText = isuzuSpeedTmp;
            isuzuMirrors.dashStatusText = isuzuStatusTmp;

            var cockpitCam = new GameObject("CockpitCameraAnchor").transform;
            cockpitCam.SetParent(root.transform, false);
            cockpitCam.localPosition = new Vector3(0.56f, 2.24f, 2.86f);
            cockpitCam.localRotation = Quaternion.Euler(9.5f, 0f, 0f);
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

        private static TextMeshPro CreateWorldSignText(string name, Transform parent, string text, Vector3 localPos, Vector2 rectSize, float fontSize, Color color, Quaternion? localRot = null, bool noWrap = false)
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
            return tmp;
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
                var mats = r.sharedMaterials;
                if (mats == null || mats.Length == 0) { count++; continue; }
                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i];
                    if (m == null || m.name.Contains("Default-Material") || m.name.Contains("Lit (Instance)")) count++;
                }
            }
            return count;
        }
    }
}
