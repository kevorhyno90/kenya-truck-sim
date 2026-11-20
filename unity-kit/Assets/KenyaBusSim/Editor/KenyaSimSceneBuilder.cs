#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace KenyaBusSim.Editor
{
    /// <summary>
    /// One-click Unity 6 URP Scene Builder for ENA Safari Hauler: Kenya Bus & Truck Simulator.
    /// Run via top menu: Kenya Bus Sim -> 1. Build A104 Kenyan Highway Scene
    /// </summary>
    public static class KenyaSimSceneBuilder
    {
        private const string BaseFolder = "Assets/KenyaBusSim";
        private const string MaterialsFolder = "Assets/KenyaBusSim/Materials";
        private const string ScenesFolder = "Assets/KenyaBusSim/Scenes";
        private const string ScenePath = "Assets/KenyaBusSim/Scenes/KenyaA104Highway.unity";

        private static readonly Dictionary<string, Material> MatCache = new Dictionary<string, Material>();

        [MenuItem("Kenya Bus Sim/1. Build A104 Kenyan Highway Scene", priority = 1)]
        public static void BuildKenyanHighwayScene()
        {
            MatCache.Clear();
            EnsureFolders();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            SetupLightingAndAtmosphere();
            BuildKenyanA104Highway();

            // Create both commercial vehicles (Left-Hand Traffic: left lane is x = -3.6 when heading +Z)
            KenyaVehicleController enaBus = BuildEnaCoach8Wheeler(new Vector3(-3.6f, 0.2f, -350f));
            KenyaVehicleController isuzuTruck = BuildIsuzuFrr6Wheeler(new Vector3(-3.6f, 0.2f, -350f));
            isuzuTruck.gameObject.SetActive(false);

            // Configure Main Camera
            Camera mainCam = Camera.main;
            if (mainCam == null)
            {
                GameObject camGo = new GameObject("Main Camera");
                camGo.tag = "MainCamera";
                mainCam = camGo.AddComponent<Camera>();
                camGo.AddComponent<AudioListener>();
            }
            mainCam.farClipPlane = 1400f;
            mainCam.fieldOfView = 60f;
            KenyaSimCamera simCam = mainCam.GetComponent<KenyaSimCamera>();
            if (simCam == null) simCam = mainCam.gameObject.AddComponent<KenyaSimCamera>();
            simCam.targetVehicle = enaBus;

            // Create Fleet Manager & HUD
            GameObject managerGo = new GameObject("KenyaBusSim_Managers");
            KenyaFleetManager fleet = managerGo.AddComponent<KenyaFleetManager>();
            fleet.simCamera = simCam;
            fleet.vehicles = new List<KenyaVehicleController> { enaBus, isuzuTruck };
            fleet.activeIndex = 0;

            KenyaSimHUD hud = managerGo.AddComponent<KenyaSimHUD>();
            hud.fleetManager = fleet;
            hud.simCamera = simCam;

            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene, ScenePath);
            Selection.activeGameObject = enaBus.gameObject;

            EditorUtility.DisplayDialog(
                "Kenya Bus Sim Scene Ready!",
                "Created A104 Kenyan Highway with:\n" +
                "- ENA Coach KDK 057M (8-Wheel Twin-Steer WheelCollider Physics)\n" +
                "- EPH Isuzu FRR 90 (6-Wheel Dual-Rear WheelCollider Physics)\n" +
                "- Kenyan Speed Bumps, Guardrails, RHD Cockpit & Audio\n\n" +
                "Press PLAY (Ctrl+P) to drive!\n" +
                "Controls: W/A/S/D Drive, Space Handbrake, H Horn, C Camera, V Switch Vehicle, G NTSA 80 km/h Limiter.",
                "Let's Drive!"
            );
        }

        [MenuItem("Kenya Bus Sim/2. Select ENA Coach VisualBodyRoot (Swap 3D Model)", priority = 20)]
        public static void SelectBusVisualRoot()
        {
            GameObject bus = GameObject.Find("ENA_Coach_KDK057M_8Wheel");
            if (bus == null)
            {
                EditorUtility.DisplayDialog("Not Found", "Build or open the KenyaA104Highway scene first.", "OK");
                return;
            }
            Transform root = bus.transform.Find("VisualBodyRoot");
            Selection.activeGameObject = root != null ? root.gameObject : bus;
        }

        [MenuItem("Kenya Bus Sim/3. Select Isuzu FRR VisualBodyRoot (Swap 3D Model)", priority = 21)]
        public static void SelectTruckVisualRoot()
        {
            KenyaVehicleController[] all = Resources.FindObjectsOfTypeAll<KenyaVehicleController>();
            foreach (var v in all)
            {
                if (v.name == "EPH_Isuzu_FRR_6Wheel")
                {
                    Transform root = v.transform.Find("VisualBodyRoot");
                    Selection.activeGameObject = root != null ? root.gameObject : v.gameObject;
                    return;
                }
            }
            EditorUtility.DisplayDialog("Not Found", "Build or open the KenyaA104Highway scene first.", "OK");
        }

        private static void EnsureFolders()
        {
            if (!Directory.Exists(MaterialsFolder)) Directory.CreateDirectory(MaterialsFolder);
            if (!Directory.Exists(ScenesFolder)) Directory.CreateDirectory(ScenesFolder);
            AssetDatabase.Refresh();
        }

        private static Material GetOrCreateMat(string matName, Color color, float metallic = 0f, float smoothness = 0.4f, bool transparent = false)
        {
            if (MatCache.TryGetValue(matName, out Material cached) && cached != null)
            {
                return cached;
            }

            string assetPath = $"{MaterialsFolder}/{matName}.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (mat == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("Standard");

                mat = new Material(shader);
                ApplyColorAndSurface(mat, color, metallic, smoothness, transparent);
                AssetDatabase.CreateAsset(mat, assetPath);
            }
            else
            {
                ApplyColorAndSurface(mat, color, metallic, smoothness, transparent);
                EditorUtility.SetDirty(mat);
            }

            MatCache[matName] = mat;
            return mat;
        }

        private static void ApplyColorAndSurface(Material mat, Color color, float metallic, float smoothness, bool transparent)
        {
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", smoothness);

            if (transparent)
            {
                if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
                if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 0f);
                mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.renderQueue = (int)RenderQueue.Transparent;
            }
        }

        private static void SetupLightingAndAtmosphere()
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.76f, 0.87f, 0.96f);
            RenderSettings.fogDensity = 0.0014f;
            RenderSettings.ambientSkyColor = new Color(0.65f, 0.78f, 0.92f);
            RenderSettings.ambientEquatorColor = new Color(0.85f, 0.72f, 0.56f);
            RenderSettings.ambientGroundColor = new Color(0.55f, 0.30f, 0.16f);

            Light sun = Object.FindFirstObjectByType<Light>();
            if (sun != null)
            {
                sun.name = "KenyanSun";
                sun.type = LightType.Directional;
                sun.color = new Color(1.0f, 0.96f, 0.88f);
                sun.intensity = 1.65f;
                sun.shadows = LightShadows.Soft;
                sun.transform.rotation = Quaternion.Euler(46f, 34f, 0f);
            }
        }

        private static void BuildKenyanA104Highway()
        {
            GameObject env = new GameObject("Environment_A104_Kenya");

            Material redSoil = GetOrCreateMat("KenyaRedMurram", new Color(0.71f, 0.39f, 0.20f), 0f, 0.12f);
            Material tarmac = GetOrCreateMat("HighwayTarmac", new Color(0.13f, 0.14f, 0.17f), 0.05f, 0.25f);
            Material whiteLine = GetOrCreateMat("RoadWhitePaint", Color.white, 0f, 0.2f);
            Material yellowLine = GetOrCreateMat("RoadYellowPaint", new Color(0.96f, 0.65f, 0.08f), 0f, 0.2f);
            Material steelGuard = GetOrCreateMat("GuardrailSteel", new Color(0.68f, 0.70f, 0.74f), 0.8f, 0.62f);
            Material bumpMat = GetOrCreateMat("SpeedBumpAsphalt", new Color(0.10f, 0.10f, 0.12f), 0f, 0.18f);
            Material signGreen = GetOrCreateMat("HighwaySignGreen", new Color(0.08f, 0.48f, 0.22f), 0.1f, 0.45f);
            Material signYellow = GetOrCreateMat("WarningSignYellow", new Color(0.96f, 0.68f, 0.05f), 0.1f, 0.45f);
            Material barkMat = GetOrCreateMat("AcaciaBark", new Color(0.29f, 0.20f, 0.14f), 0f, 0.1f);
            Material leafMat = GetOrCreateMat("AcaciaCanopy", new Color(0.30f, 0.48f, 0.08f), 0f, 0.15f);
            Material hillMat = GetOrCreateMat("RiftValleyHills", new Color(0.49f, 0.33f, 0.22f), 0f, 0.08f);

            const float roadLen = 2200f;
            const float roadWidth = 16f;

            // 1. Kenyan Red Soil Ground
            CreateBox("KenyanRedEarth", env.transform, new Vector3(0f, -0.5f, 0f), new Vector3(2400f, 1f, 2400f), redSoil, keepCollider: true);

            // 2. A104 Tarmac Surface (slightly above soil for clean shadow/contact)
            CreateBox("A104_TarmacRoad", env.transform, new Vector3(0f, -0.45f, 0f), new Vector3(roadWidth, 1f, roadLen), tarmac, keepCollider: true);

            // 3. Solid White Shoulder Edge Lines
            CreateBox("EdgeLine_Left", env.transform, new Vector3(-7.3f, 0.06f, 0f), new Vector3(0.28f, 0.02f, roadLen), whiteLine, keepCollider: false);
            CreateBox("EdgeLine_Right", env.transform, new Vector3(7.3f, 0.06f, 0f), new Vector3(0.28f, 0.02f, roadLen), whiteLine, keepCollider: false);

            // 4. Yellow Broken Center Lane Markings (Kenyan Standard)
            GameObject markingsRoot = new GameObject("CenterYellowMarkings");
            markingsRoot.transform.SetParent(env.transform);
            for (float z = -roadLen * 0.48f; z < roadLen * 0.48f; z += 12f)
            {
                CreateBox("Stripe", markingsRoot.transform, new Vector3(0f, 0.065f, z), new Vector3(0.32f, 0.02f, 5.2f), yellowLine, keepCollider: false);
            }

            // 5. Continuous Galvanized Steel W-Beam Guardrails
            GameObject guardRoot = new GameObject("HighwayGuardrails");
            guardRoot.transform.SetParent(env.transform);
            foreach (float side in new[] { -8.35f, 8.35f })
            {
                CreateBox(side < 0 ? "Guardrail_Left" : "Guardrail_Right", guardRoot.transform,
                    new Vector3(side, 0.72f, 0f), new Vector3(0.14f, 0.42f, roadLen), steelGuard, keepCollider: true);

                for (float z = -roadLen * 0.48f; z < roadLen * 0.48f; z += 14f)
                {
                    CreateBox("Post", guardRoot.transform, new Vector3(side, 0.42f, z), new Vector3(0.12f, 0.85f, 0.12f), steelGuard, keepCollider: false);
                }
            }

            // 6. Physical Kenyan Speed Bumps + Warning Signs + Trigger Zones
            GameObject bumpsRoot = new GameObject("KenyanSpeedBumps");
            bumpsRoot.transform.SetParent(env.transform);
            float[] bumpPositions = { -220f, -40f, 180f, 420f, 680f, 920f };
            for (int i = 0; i < bumpPositions.Length; i++)
            {
                float bz = bumpPositions[i];
                GameObject bumpGroup = new GameObject($"SpeedBump_{i + 1}");
                bumpGroup.transform.SetParent(bumpsRoot.transform);
                bumpGroup.transform.position = new Vector3(0f, 0f, bz);

                // Smooth ramped bump geometry (so WheelColliders ride cleanly without snagging)
                GameObject rampUp = CreateBox("RampUp", bumpGroup.transform, new Vector3(0f, 0.07f, -0.55f), new Vector3(roadWidth - 0.8f, 0.16f, 0.9f), bumpMat, keepCollider: true);
                rampUp.transform.localRotation = Quaternion.Euler(-12f, 0f, 0f);

                CreateBox("BumpCrown", bumpGroup.transform, new Vector3(0f, 0.13f, 0f), new Vector3(roadWidth - 0.8f, 0.22f, 0.7f), bumpMat, keepCollider: true);

                GameObject rampDown = CreateBox("RampDown", bumpGroup.transform, new Vector3(0f, 0.07f, 0.55f), new Vector3(roadWidth - 0.8f, 0.16f, 0.9f), bumpMat, keepCollider: true);
                rampDown.transform.localRotation = Quaternion.Euler(12f, 0f, 0f);

                // Yellow painted stripes on the speed bump
                for (float sx = -6f; sx <= 6f; sx += 2f)
                {
                    CreateBox("BumpStripe", bumpGroup.transform, new Vector3(sx, 0.245f, 0f), new Vector3(0.75f, 0.02f, 0.55f), yellowLine, keepCollider: false);
                }

                // Roadside yellow warning sign on left (Kenyan LHT side!)
                CreateBox("WarnPole", bumpGroup.transform, new Vector3(-9.4f, 1.7f, -20f), new Vector3(0.12f, 3.4f, 0.12f), steelGuard, keepCollider: false);
                GameObject diamond = CreateBox("WarnBoard", bumpGroup.transform, new Vector3(-9.4f, 3.4f, -20f), new Vector3(1.1f, 1.1f, 0.08f), signYellow, keepCollider: false);
                diamond.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);

                // Trigger zone 35m before the bump
                GameObject trig = new GameObject("SpeedBumpWarningTrigger");
                trig.transform.SetParent(bumpGroup.transform);
                trig.transform.localPosition = new Vector3(0f, 2f, -22f);
                BoxCollider triggerCol = trig.AddComponent<BoxCollider>();
                triggerCol.isTrigger = true;
                triggerCol.size = new Vector3(roadWidth, 5f, 36f);
                trig.AddComponent<SpeedBumpWarningZone>();
            }

            // 7. Green A104 Highway Destination Signs
            foreach (float sz in new[] { -300f, 60f, 520f })
            {
                CreateBox("SignPole", env.transform, new Vector3(-9.8f, 2.3f, sz), new Vector3(0.14f, 4.6f, 0.14f), steelGuard, keepCollider: false);
                CreateBox("A104_GreenSign", env.transform, new Vector3(-9.8f, 3.8f, sz), new Vector3(3.0f, 1.4f, 0.12f), signGreen, keepCollider: false);
            }

            // 8. Savanna Acacia Trees & Distant Great Rift Valley Hills
            GameObject scenery = new GameObject("KenyanScenery");
            scenery.transform.SetParent(env.transform);
            System.Random rng = new System.Random(254);

            for (int i = 0; i < 95; i++)
            {
                float side = rng.NextDouble() > 0.5 ? 1f : -1f;
                float tx = side * (16f + (float)rng.NextDouble() * 85f);
                float tz = ((float)rng.NextDouble() - 0.5f) * (roadLen - 140f);

                GameObject tree = new GameObject($"Acacia_{i}");
                tree.transform.SetParent(scenery.transform);
                tree.transform.position = new Vector3(tx, 0f, tz);

                GameObject trunk = CreateCylinder("Trunk", tree.transform, new Vector3(0f, 3.5f, 0f), new Vector3(0.75f, 3.5f, 0.75f), barkMat, keepCollider: false);
                trunk.transform.localRotation = Quaternion.Euler(0f, 0f, ((float)rng.NextDouble() - 0.5f) * 10f);

                CreateCylinder("Canopy", tree.transform, new Vector3(0f, 7.2f, 0f), new Vector3(8.5f, 0.65f, 8.5f), leafMat, keepCollider: false);
            }

            foreach (float side in new[] { -1f, 1f })
            {
                for (float hz = -950f; hz <= 950f; hz += 160f)
                {
                    float hx = side * (165f + (float)rng.NextDouble() * 55f);
                    float hScaleY = 38f + (float)rng.NextDouble() * 42f;
                    CreateSphere("RiftHill", scenery.transform, new Vector3(hx, hScaleY * 0.25f, hz),
                        new Vector3(170f, hScaleY, 190f), hillMat, keepCollider: false);
                }
            }
        }

        private static KenyaVehicleController BuildEnaCoach8Wheeler(Vector3 spawnPos)
        {
            GameObject bus = new GameObject("ENA_Coach_KDK057M_8Wheel");
            bus.transform.position = spawnPos;

            Rigidbody rb = bus.AddComponent<Rigidbody>();
            rb.mass = 14500f;
#if UNITY_6000_0_OR_NEWER
            rb.linearDamping = 0.05f;
            rb.angularDamping = 0.25f;
#else
            rb.drag = 0.05f;
            rb.angularDrag = 0.25f;
#endif

            // Main physical collision hull (raised above suspension travel so tires touch tarmac)
            GameObject collidersRoot = new GameObject("ChassisColliders");
            collidersRoot.transform.SetParent(bus.transform, false);
            BoxCollider mainHull = collidersRoot.AddComponent<BoxCollider>();
            mainHull.center = new Vector3(0f, 2.35f, 0f);
            mainHull.size = new Vector3(2.62f, 2.75f, 13.8f);

            // Materials matching user's ENA Coach KDK 057M photo
            Material pearlWhite = GetOrCreateMat("ENA_PearlWhite", new Color(0.97f, 0.98f, 0.99f), 0.12f, 0.88f);
            Material redSkirt = GetOrCreateMat("ENA_RedSkirt", new Color(0.84f, 0.13f, 0.13f), 0.15f, 0.78f);
            Material glossBlack = GetOrCreateMat("ENA_GlossBlack", new Color(0.05f, 0.06f, 0.08f), 0.25f, 0.90f);
            Material orangeLogo = GetOrCreateMat("ENA_OrangeLogo", new Color(0.98f, 0.45f, 0.06f), 0.05f, 0.65f);
            Material chrome = GetOrCreateMat("ENA_Chrome", new Color(0.88f, 0.90f, 0.94f), 0.94f, 0.92f);
            Material glass = GetOrCreateMat("ENA_TintedGlass", new Color(0.07f, 0.11f, 0.18f, 0.78f), 0.1f, 0.95f, transparent: true);
            Material headlightMat = GetOrCreateMat("ENA_Headlights", new Color(1.0f, 0.95f, 0.72f), 0f, 0.9f);
            Material tailRedMat = GetOrCreateMat("ENA_TailLights", new Color(0.92f, 0.10f, 0.10f), 0f, 0.85f);
            Material darkDash = GetOrCreateMat("CockpitDash", new Color(0.11f, 0.12f, 0.15f), 0.1f, 0.35f);
            Material greenGauges = GetOrCreateMat("CockpitGauges", new Color(0.08f, 0.78f, 0.48f), 0f, 0.8f);

            // VisualBodyRoot: Drop any real .FBX / .GLB / .OBJ bus mesh here to replace placeholder geometry!
            GameObject visualRoot = new GameObject("VisualBodyRoot");
            visualRoot.transform.SetParent(bus.transform, false);

            // Main pearl white coach shell
            CreateBox("UpperCoachBody", visualRoot.transform, new Vector3(0f, 2.55f, 0f), new Vector3(2.68f, 2.25f, 13.8f), pearlWhite, false);
            // Thick ENA red lower skirt band
            CreateBox("LowerRedSkirt", visualRoot.transform, new Vector3(0f, 1.05f, 0f), new Vector3(2.70f, 0.76f, 13.82f), redSkirt, false);
            // Slim black divider pinstripe
            CreateBox("SkirtBlackStripe", visualRoot.transform, new Vector3(0f, 1.46f, 0f), new Vector3(2.72f, 0.08f, 13.84f), glossBlack, false);

            // Side ENA Red emblem badges (both sides)
            CreateBox("SideBadge_L", visualRoot.transform, new Vector3(-1.36f, 2.05f, 2.4f), new Vector3(0.04f, 0.58f, 1.65f), redSkirt, false);
            CreateBox("SideBadge_R", visualRoot.transform, new Vector3(1.36f, 2.05f, 2.4f), new Vector3(0.04f, 0.58f, 1.65f), redSkirt, false);

            // Panoramic Tinted Side Windows (left & right strips)
            CreateBox("SideWindows", visualRoot.transform, new Vector3(0f, 2.95f, -0.35f), new Vector3(2.72f, 1.18f, 11.9f), glass, false);

            // Gloss Black Forehead Brow + Orange ENA Logo Bar + 5 Amber Marker Lights
            GameObject brow = CreateBox("GlossBlackBrow", visualRoot.transform, new Vector3(0f, 3.55f, 6.86f), new Vector3(2.66f, 0.48f, 0.36f), glossBlack, false);
            brow.transform.localRotation = Quaternion.Euler(-10f, 0f, 0f);
            CreateBox("OrangeENABadgeFront", visualRoot.transform, new Vector3(0.55f, 3.55f, 7.06f), new Vector3(0.75f, 0.24f, 0.05f), orangeLogo, false);
            CreateBox("OrangeCoachTextBar", visualRoot.transform, new Vector3(-0.45f, 3.55f, 7.06f), new Vector3(0.70f, 0.18f, 0.05f), orangeLogo, false);

            foreach (float mx in new[] { -0.9f, -0.45f, 0f, 0.45f, 0.9f })
            {
                CreateBox("ClearanceLED", visualRoot.transform, new Vector3(mx, 3.74f, 6.98f), new Vector3(0.11f, 0.06f, 0.07f), orangeLogo, false);
            }

            // Tilted Panoramic Windshield
            GameObject ws = CreateBox("PanoramicWindshield", visualRoot.transform, new Vector3(0f, 2.55f, 6.95f), new Vector3(2.58f, 1.68f, 0.12f), glass, false);
            ws.transform.localRotation = Quaternion.Euler(-8f, 0f, 0f);

            // White NAIROBI destination card behind windshield (passenger side)
            CreateBox("DestinationBoard_NAIROBI", visualRoot.transform, new Vector3(-0.62f, 1.98f, 6.82f), new Vector3(0.72f, 0.24f, 0.04f), pearlWhite, false);

            // Front Bumper + MAN Chrome Horizontal V-Grille + KDK 057M Plate
            CreateBox("FrontBumperWhite", visualRoot.transform, new Vector3(0f, 1.25f, 7.02f), new Vector3(2.64f, 0.98f, 0.32f), pearlWhite, false);
            CreateBox("GrilleBacking", visualRoot.transform, new Vector3(0f, 1.32f, 7.16f), new Vector3(1.55f, 0.62f, 0.08f), glossBlack, false);
            for (int s = 0; s < 5; s++)
            {
                float w = 1.48f - s * 0.08f;
                CreateBox($"ChromeSlat_{s}", visualRoot.transform, new Vector3(0f, 1.54f - s * 0.11f, 7.21f), new Vector3(w, 0.04f, 0.05f), chrome, false);
            }
            CreateBox("Plate_KDK057M", visualRoot.transform, new Vector3(0f, 0.86f, 7.21f), new Vector3(0.56f, 0.16f, 0.04f), pearlWhite, false);

            // Swept Headlamps & Lower Fog Lights
            foreach (float side in new[] { -1f, 1f })
            {
                CreateBox("Headlight", visualRoot.transform, new Vector3(side * 1.05f, 1.28f, 7.18f), new Vector3(0.42f, 0.20f, 0.10f), headlightMat, false);
                CreateBox("FogLight", visualRoot.transform, new Vector3(side * 0.92f, 0.86f, 7.18f), new Vector3(0.26f, 0.12f, 0.08f), headlightMat, false);

                // Iconic White Swept "Elephant-Ear" Mirrors from User's Photo
                CreateBox("MirrorTopArm", visualRoot.transform, new Vector3(side * 1.56f, 3.62f, 6.72f), new Vector3(0.52f, 0.11f, 0.14f), pearlWhite, false);
                GameObject downArm = CreateBox("MirrorDownArm", visualRoot.transform, new Vector3(side * 1.76f, 3.10f, 6.86f), new Vector3(0.11f, 1.05f, 0.15f), pearlWhite, false);
                downArm.transform.localRotation = Quaternion.Euler(10f, 0f, side * -6f);
                CreateBox("MirrorHousing", visualRoot.transform, new Vector3(side * 1.72f, 2.55f, 6.82f), new Vector3(0.18f, 0.68f, 0.24f), pearlWhite, false);
                CreateBox("MirrorGlass", visualRoot.transform, new Vector3(side * 1.72f, 2.55f, 6.69f), new Vector3(0.15f, 0.60f, 0.02f), chrome, false);
            }

            // Roof AC Pod, Rear Spoiler, Tail Lights & Dual Exhausts
            CreateBox("RoofACPod", visualRoot.transform, new Vector3(0f, 3.88f, -0.2f), new Vector3(1.85f, 0.42f, 4.4f), pearlWhite, false);
            CreateBox("RearSpoiler", visualRoot.transform, new Vector3(0f, 3.75f, -6.95f), new Vector3(2.68f, 0.30f, 0.65f), pearlWhite, false);
            foreach (float side in new[] { -1f, 1f })
            {
                CreateBox("TailLight", visualRoot.transform, new Vector3(side * 1.15f, 1.55f, -6.94f), new Vector3(0.22f, 0.88f, 0.08f), tailRedMat, false);
                GameObject ex = CreateCylinder("Exhaust", visualRoot.transform, new Vector3(side * 0.72f, 0.52f, -7.02f), new Vector3(0.14f, 0.25f, 0.14f), chrome, false);
                ex.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            }

            // Kenyan Right-Hand Drive (RHD) Cockpit Dashboard & Rotating Steering Wheel
            GameObject cockpit = new GameObject("RHD_Cockpit");
            cockpit.transform.SetParent(visualRoot.transform, false);
            cockpit.transform.localPosition = new Vector3(0.68f, 2.05f, 5.95f);

            CreateBox("Dashboard", cockpit.transform, new Vector3(0f, 0f, 0.18f), new Vector3(1.1f, 0.42f, 0.65f), darkDash, false);
            GameObject gauge = CreateBox("InstrumentCluster", cockpit.transform, new Vector3(0f, 0.18f, 0.12f), new Vector3(0.55f, 0.18f, 0.04f), greenGauges, false);
            gauge.transform.localRotation = Quaternion.Euler(-28f, 0f, 0f);

            GameObject steeringPivot = new GameObject("SteeringWheelPivot");
            steeringPivot.transform.SetParent(cockpit.transform, false);
            steeringPivot.transform.localPosition = new Vector3(0f, 0.26f, -0.10f);
            steeringPivot.transform.localRotation = Quaternion.Euler(32f, 0f, 0f);

            // Stylized steering wheel ring built from spokes + rim segments
            CreateCylinder("WheelHub", steeringPivot.transform, Vector3.zero, new Vector3(0.14f, 0.03f, 0.14f), chrome, false).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            CreateBox("SpokeHoriz", steeringPivot.transform, Vector3.zero, new Vector3(0.46f, 0.04f, 0.025f), glossBlack, false);
            CreateBox("SpokeVert", steeringPivot.transform, new Vector3(0f, -0.11f, 0f), new Vector3(0.045f, 0.22f, 0.025f), glossBlack, false);
            for (int i = 0; i < 16; i++)
            {
                float a = (i / 16f) * Mathf.PI * 2f;
                GameObject seg = CreateBox($"Rim_{i}", steeringPivot.transform,
                    new Vector3(Mathf.Cos(a) * 0.23f, Mathf.Sin(a) * 0.23f, 0f),
                    new Vector3(0.10f, 0.035f, 0.035f), glossBlack, false);
                seg.transform.localRotation = Quaternion.Euler(0f, 0f, (a * Mathf.Rad2Deg) + 90f);
            }

            // Camera Anchors
            Transform cockpitAnchor = CreateAnchor("CockpitCameraAnchor", bus.transform, new Vector3(0.68f, 2.75f, 5.45f));
            Transform chaseTarget = CreateAnchor("ChaseCameraTarget", bus.transform, new Vector3(0f, 2.4f, 0.5f));
            Transform wheelCam = CreateAnchor("WheelCamAnchor", bus.transform, new Vector3(-4.2f, 1.15f, 6.2f));

            // Controller & Audio
            KenyaVehicleController ctrl = bus.AddComponent<KenyaVehicleController>();
            ctrl.vehicleClass = KenyanVehicleClass.EnaCoachBus;
            ctrl.displayName = "ENA Coach KDK 057M (8 Wheels)";
            ctrl.companyTag = "ENA COACH";
            ctrl.routeLabel = "Nairobi (River Road) -> Nakuru -> Kericho -> Kisii -> Kisumu";
            ctrl.maxMotorTorque = 5800f;
            ctrl.maxBrakeTorque = 13500f;
            ctrl.handbrakeTorque = 24000f;
            ctrl.maxHighwaySpeedKmh = 98f;
            ctrl.centerOfMassOffset = new Vector3(0f, 0.72f, 0.1f);
            ctrl.cockpitCameraAnchor = cockpitAnchor;
            ctrl.chaseCameraTarget = chaseTarget;
            ctrl.wheelCamAnchor = wheelCam;
            ctrl.steeringWheelTransform = steeringPivot.transform;

            KenyaVehicleAudio audio = bus.AddComponent<KenyaVehicleAudio>();
            audio.useBusChimeHorn = true;

            // 8 REAL WHEELCOLLIDERS (4 AXLES: TWIN FRONT STEER + DRIVE + TAG)
            GameObject wheelColsRoot = new GameObject("WheelColliders");
            wheelColsRoot.transform.SetParent(bus.transform, false);
            GameObject wheelVisRoot = new GameObject("WheelVisuals");
            wheelVisRoot.transform.SetParent(bus.transform, false);

            var axleSpecs = new (string name, float z, float steerMult, bool motor)[]
            {
                ("Axle1_FrontSteer1", 4.9f, 1.00f, false),
                ("Axle2_FrontSteer2", 3.35f, 0.72f, false),
                ("Axle3_RearDrive", -3.25f, 0.00f, true),
                ("Axle4_RearTag", -4.75f, 0.00f, false)
            };

            foreach (var spec in axleSpecs)
            {
                AxleConfig axle = BuildAxlePair(
                    spec.name, wheelColsRoot.transform, wheelVisRoot.transform,
                    trackHalfWidth: 1.24f, wheelY: 0.68f, wheelZ: spec.z,
                    radius: 0.56f, width: 0.36f, suspensionDist: 0.24f,
                    spring: 155000f, damper: 16500f,
                    steerMult: spec.steerMult, isMotor: spec.motor,
                    dualTires: false, isBusAlcoa: true
                );
                ctrl.axles.Add(axle);
            }

            return ctrl;
        }

        private static KenyaVehicleController BuildIsuzuFrr6Wheeler(Vector3 spawnPos)
        {
            GameObject truck = new GameObject("EPH_Isuzu_FRR_6Wheel");
            truck.transform.position = spawnPos;

            Rigidbody rb = truck.AddComponent<Rigidbody>();
            rb.mass = 9200f;
#if UNITY_6000_0_OR_NEWER
            rb.linearDamping = 0.06f;
            rb.angularDamping = 0.28f;
#else
            rb.drag = 0.06f;
            rb.angularDrag = 0.28f;
#endif

            // Chassis colliders
            GameObject collidersRoot = new GameObject("ChassisColliders");
            collidersRoot.transform.SetParent(truck.transform, false);
            BoxCollider boxCol = collidersRoot.AddComponent<BoxCollider>();
            boxCol.center = new Vector3(0f, 2.25f, -0.45f);
            boxCol.size = new Vector3(2.52f, 2.55f, 8.9f);

            Material ephNavy = GetOrCreateMat("EPH_DeepNavy", new Color(0.06f, 0.10f, 0.19f), 0.2f, 0.72f);
            Material ephYellow = GetOrCreateMat("EPH_IndustrialYellow", new Color(0.96f, 0.62f, 0.04f), 0.1f, 0.65f);
            Material steelBlack = GetOrCreateMat("Isuzu_BlackSteel", new Color(0.08f, 0.08f, 0.10f), 0.65f, 0.45f);
            Material chrome = GetOrCreateMat("ENA_Chrome", new Color(0.88f, 0.90f, 0.94f), 0.94f, 0.92f);
            Material aluminum = GetOrCreateMat("Isuzu_Aluminum", new Color(0.72f, 0.75f, 0.80f), 0.8f, 0.65f);
            Material glass = GetOrCreateMat("ENA_TintedGlass", new Color(0.07f, 0.11f, 0.18f, 0.78f), 0.1f, 0.95f, transparent: true);
            Material headlightMat = GetOrCreateMat("ENA_Headlights", new Color(1.0f, 0.95f, 0.72f), 0f, 0.9f);
            Material orangeTurn = GetOrCreateMat("ENA_OrangeLogo", new Color(0.98f, 0.45f, 0.06f), 0.05f, 0.65f);
            Material darkDash = GetOrCreateMat("CockpitDash", new Color(0.11f, 0.12f, 0.15f), 0.1f, 0.35f);

            // VisualBodyRoot: Drop any real Isuzu FRR .FBX / .GLB here!
            GameObject visualRoot = new GameObject("VisualBodyRoot");
            visualRoot.transform.SetParent(truck.transform, false);

            // Exposed Steel Ladder Chassis Rails
            CreateBox("ChassisRail_L", visualRoot.transform, new Vector3(-0.62f, 0.92f, -0.3f), new Vector3(0.18f, 0.30f, 8.8f), steelBlack, false);
            CreateBox("ChassisRail_R", visualRoot.transform, new Vector3(0.62f, 0.92f, -0.3f), new Vector3(0.18f, 0.30f, 8.8f), steelBlack, false);

            // Isuzu FRR Forward Cab
            CreateBox("IsuzuCab", visualRoot.transform, new Vector3(0f, 2.15f, 2.75f), new Vector3(2.44f, 2.25f, 2.25f), ephNavy, false);
            GameObject ws = CreateBox("IsuzuWindshield", visualRoot.transform, new Vector3(0f, 2.58f, 3.88f), new Vector3(2.24f, 1.08f, 0.10f), glass, false);
            ws.transform.localRotation = Quaternion.Euler(-7f, 0f, 0f);
            CreateBox("SideWindow_L", visualRoot.transform, new Vector3(-1.23f, 2.50f, 2.85f), new Vector3(0.05f, 0.88f, 1.15f), glass, false);
            CreateBox("SideWindow_R", visualRoot.transform, new Vector3(1.23f, 2.50f, 2.85f), new Vector3(0.05f, 0.88f, 1.15f), glass, false);

            // Roof Wind Deflector Scoop
            GameObject deflector = CreateBox("RoofDeflector", visualRoot.transform, new Vector3(0f, 3.45f, 2.50f), new Vector3(2.34f, 0.58f, 1.55f), ephNavy, false);
            deflector.transform.localRotation = Quaternion.Euler(18f, 0f, 0f);

            // Isuzu Grille, Chrome Bar & Vertical Stacked Headlights
            CreateBox("IsuzuGrille", visualRoot.transform, new Vector3(0f, 1.52f, 3.89f), new Vector3(1.76f, 0.62f, 0.08f), steelBlack, false);
            CreateBox("IsuzuChromeBar", visualRoot.transform, new Vector3(0f, 1.54f, 3.93f), new Vector3(1.66f, 0.08f, 0.05f), chrome, false);
            CreateBox("IsuzuBadgeHeader", visualRoot.transform, new Vector3(0f, 1.92f, 3.90f), new Vector3(0.85f, 0.14f, 0.05f), chrome, false);

            foreach (float side in new[] { -1f, 1f })
            {
                CreateBox("StackedHeadlight", visualRoot.transform, new Vector3(side * 0.96f, 1.52f, 3.90f), new Vector3(0.24f, 0.36f, 0.09f), headlightMat, false);
                CreateBox("CornerIndicator", visualRoot.transform, new Vector3(side * 0.96f, 1.24f, 3.90f), new Vector3(0.24f, 0.14f, 0.09f), orangeTurn, false);
                CreateBox("MirrorArm", visualRoot.transform, new Vector3(side * 1.40f, 2.55f, 3.45f), new Vector3(0.32f, 0.05f, 0.05f), steelBlack, false);
                CreateBox("MirrorHead", visualRoot.transform, new Vector3(side * 1.52f, 2.50f, 3.48f), new Vector3(0.14f, 0.62f, 0.20f), steelBlack, false);
            }

            // Heavy-Duty Kenyan Front Bull-Bar (Kikapu)
            CreateBox("KikapuLowerBumper", visualRoot.transform, new Vector3(0f, 0.98f, 4.10f), new Vector3(2.54f, 0.24f, 0.30f), steelBlack, false);
            CreateBox("KikapuCenterLoop", visualRoot.transform, new Vector3(0f, 1.48f, 4.15f), new Vector3(1.52f, 0.74f, 0.14f), steelBlack, false);
            CreateBox("KikapuGuard_L", visualRoot.transform, new Vector3(-0.98f, 1.44f, 4.14f), new Vector3(0.42f, 0.54f, 0.12f), steelBlack, false);
            CreateBox("KikapuGuard_R", visualRoot.transform, new Vector3(0.98f, 1.44f, 4.14f), new Vector3(0.42f, 0.54f, 0.12f), steelBlack, false);

            // Vertical Snorkel Air Intake Behind Cab
            CreateCylinder("SnorkelPipe", visualRoot.transform, new Vector3(-1.18f, 2.55f, 1.50f), new Vector3(0.14f, 1.25f, 0.14f), steelBlack, false);
            CreateBox("SnorkelHead", visualRoot.transform, new Vector3(-1.18f, 3.82f, 1.50f), new Vector3(0.24f, 0.20f, 0.28f), steelBlack, false);

            // EPH Logistics Parcel Container Box + Yellow Accent Stripes + Rear Locking Rods
            CreateBox("EPH_CargoBox", visualRoot.transform, new Vector3(0f, 2.55f, -1.95f), new Vector3(2.58f, 2.85f, 6.7f), ephNavy, false);
            CreateBox("EPH_TopStripe", visualRoot.transform, new Vector3(0f, 3.72f, -1.95f), new Vector3(2.61f, 0.22f, 6.72f), ephYellow, false);
            CreateBox("EPH_BottomStripe", visualRoot.transform, new Vector3(0f, 1.32f, -1.95f), new Vector3(2.61f, 0.24f, 6.72f), ephYellow, false);
            CreateBox("EPH_SideLogo_L", visualRoot.transform, new Vector3(-1.31f, 2.65f, -1.95f), new Vector3(0.03f, 0.78f, 3.4f), ephYellow, false);
            CreateBox("EPH_SideLogo_R", visualRoot.transform, new Vector3(1.31f, 2.65f, -1.95f), new Vector3(0.03f, 0.78f, 3.4f), ephYellow, false);
            CreateCylinder("CamLockRod_L", visualRoot.transform, new Vector3(-0.36f, 2.55f, -5.33f), new Vector3(0.05f, 1.32f, 0.05f), chrome, false);
            CreateCylinder("CamLockRod_R", visualRoot.transform, new Vector3(0.36f, 2.55f, -5.33f), new Vector3(0.05f, 1.32f, 0.05f), chrome, false);
            CreateBox("ICC_RearBumper", visualRoot.transform, new Vector3(0f, 0.70f, -5.25f), new Vector3(2.44f, 0.18f, 0.16f), ephYellow, false);

            // Undercarriage: Aluminum Diesel Tank, Battery Box & Spare Tire
            GameObject fuelTank = CreateCylinder("AluminumFuelTank", visualRoot.transform, new Vector3(1.02f, 0.90f, 0.35f), new Vector3(0.75f, 0.85f, 0.75f), aluminum, false);
            fuelTank.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            CreateBox("BatteryBox", visualRoot.transform, new Vector3(-1.02f, 0.92f, 0.45f), new Vector3(0.50f, 0.44f, 0.68f), steelBlack, false);
            CreateCylinder("SpareTire", visualRoot.transform, new Vector3(0f, 0.85f, -4.15f), new Vector3(1.02f, 0.18f, 1.02f), steelBlack, false);

            // RHD Cockpit & Steering Wheel
            GameObject cockpit = new GameObject("RHD_Cockpit");
            cockpit.transform.SetParent(visualRoot.transform, false);
            cockpit.transform.localPosition = new Vector3(0.62f, 2.02f, 3.15f);
            CreateBox("Dashboard", cockpit.transform, new Vector3(0f, 0f, 0.15f), new Vector3(0.95f, 0.38f, 0.55f), darkDash, false);

            GameObject steeringPivot = new GameObject("SteeringWheelPivot");
            steeringPivot.transform.SetParent(cockpit.transform, false);
            steeringPivot.transform.localPosition = new Vector3(0f, 0.24f, -0.08f);
            steeringPivot.transform.localRotation = Quaternion.Euler(34f, 0f, 0f);
            CreateBox("IsuzuSpoke", steeringPivot.transform, Vector3.zero, new Vector3(0.42f, 0.04f, 0.025f), steelBlack, false);
            for (int i = 0; i < 14; i++)
            {
                float a = (i / 14f) * Mathf.PI * 2f;
                GameObject seg = CreateBox($"Rim_{i}", steeringPivot.transform,
                    new Vector3(Mathf.Cos(a) * 0.21f, Mathf.Sin(a) * 0.21f, 0f),
                    new Vector3(0.10f, 0.032f, 0.032f), steelBlack, false);
                seg.transform.localRotation = Quaternion.Euler(0f, 0f, (a * Mathf.Rad2Deg) + 90f);
            }

            // Camera Anchors
            Transform cockpitAnchor = CreateAnchor("CockpitCameraAnchor", truck.transform, new Vector3(0.62f, 2.62f, 2.75f));
            Transform chaseTarget = CreateAnchor("ChaseCameraTarget", truck.transform, new Vector3(0f, 2.2f, -0.4f));
            Transform wheelCam = CreateAnchor("WheelCamAnchor", truck.transform, new Vector3(-3.8f, 1.05f, -3.5f));

            // Controller & Audio
            KenyaVehicleController ctrl = truck.AddComponent<KenyaVehicleController>();
            ctrl.vehicleClass = KenyanVehicleClass.EphIsuzuTruck;
            ctrl.displayName = "EPH Isuzu FRR 90 (6 Wheels)";
            ctrl.companyTag = "EPH LOGISTICS";
            ctrl.routeLabel = "River Road Parcel Hub -> Nakuru -> Western Depot";
            ctrl.maxMotorTorque = 4600f;
            ctrl.maxBrakeTorque = 10500f;
            ctrl.handbrakeTorque = 18000f;
            ctrl.maxHighwaySpeedKmh = 88f;
            ctrl.centerOfMassOffset = new Vector3(0f, 0.78f, -0.35f);
            ctrl.cockpitCameraAnchor = cockpitAnchor;
            ctrl.chaseCameraTarget = chaseTarget;
            ctrl.wheelCamAnchor = wheelCam;
            ctrl.steeringWheelTransform = steeringPivot.transform;

            KenyaVehicleAudio audio = truck.AddComponent<KenyaVehicleAudio>();
            audio.useBusChimeHorn = false;

            // 6 REAL WHEELS (2 AXLES: FRONT STEER + DUAL-TIRE REAR DRIVE)
            GameObject wheelColsRoot = new GameObject("WheelColliders");
            wheelColsRoot.transform.SetParent(truck.transform, false);
            GameObject wheelVisRoot = new GameObject("WheelVisuals");
            wheelVisRoot.transform.SetParent(truck.transform, false);

            ctrl.axles.Add(BuildAxlePair(
                "Axle1_FrontSteer", wheelColsRoot.transform, wheelVisRoot.transform,
                trackHalfWidth: 1.12f, wheelY: 0.64f, wheelZ: 2.68f,
                radius: 0.52f, width: 0.32f, suspensionDist: 0.22f,
                spring: 185000f, damper: 18000f,
                steerMult: 1.0f, isMotor: false,
                dualTires: false, isBusAlcoa: false
            ));

            ctrl.axles.Add(BuildAxlePair(
                "Axle2_RearDualDrive", wheelColsRoot.transform, wheelVisRoot.transform,
                trackHalfWidth: 1.06f, wheelY: 0.64f, wheelZ: -2.28f,
                radius: 0.52f, width: 0.30f, suspensionDist: 0.22f,
                spring: 245000f, damper: 22000f,
                steerMult: 0.0f, isMotor: true,
                dualTires: true, isBusAlcoa: false
            ));

            return ctrl;
        }

        private static AxleConfig BuildAxlePair(
            string axleName, Transform colParent, Transform visParent,
            float trackHalfWidth, float wheelY, float wheelZ,
            float radius, float width, float suspensionDist,
            float spring, float damper,
            float steerMult, bool isMotor,
            bool dualTires, bool isBusAlcoa)
        {
            AxleConfig axle = new AxleConfig
            {
                axleName = axleName,
                steerMultiplier = steerMult,
                isMotor = isMotor,
                isBraking = true,
                antiRollForce = isBusAlcoa ? 18000f : 14000f
            };

            axle.leftCollider = CreateWheelCollider($"{axleName}_L", colParent, new Vector3(-trackHalfWidth, wheelY, wheelZ), radius, suspensionDist, spring, damper);
            axle.rightCollider = CreateWheelCollider($"{axleName}_R", colParent, new Vector3(trackHalfWidth, wheelY, wheelZ), radius, suspensionDist, spring, damper);

            axle.leftVisual = CreateWheelVisual($"{axleName}_Vis_L", visParent, radius, width, dualTires, isBusAlcoa, isLeft: true);
            axle.rightVisual = CreateWheelVisual($"{axleName}_Vis_R", visParent, radius, width, dualTires, isBusAlcoa, isLeft: false);

            return axle;
        }

        private static WheelCollider CreateWheelCollider(string name, Transform parent, Vector3 localPos, float radius, float suspDist, float springVal, float damperVal)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;

            WheelCollider wc = go.AddComponent<WheelCollider>();
            wc.mass = 45f;
            wc.radius = radius;
            wc.wheelDampingRate = 0.35f;
            wc.suspensionDistance = suspDist;
            wc.forceAppPointDistance = 0.25f;

            JointSpring js = wc.suspensionSpring;
            js.spring = springVal;
            js.damper = damperVal;
            js.targetPosition = 0.5f;
            wc.suspensionSpring = js;

            WheelFrictionCurve fwd = wc.forwardFriction;
            fwd.stiffness = 1.45f;
            wc.forwardFriction = fwd;

            WheelFrictionCurve side = wc.sidewaysFriction;
            side.stiffness = 1.65f;
            wc.sidewaysFriction = side;

            return wc;
        }

        private static Transform CreateWheelVisual(string name, Transform parent, float radius, float width, bool dualTires, bool isBusAlcoa, bool isLeft)
        {
            GameObject pivot = new GameObject(name);
            pivot.transform.SetParent(parent, false);

            Material rubber = GetOrCreateMat("TireRubber", new Color(0.09f, 0.09f, 0.11f), 0.05f, 0.15f);
            Material rimMat = isBusAlcoa
                ? GetOrCreateMat("AlcoaChromeRim", new Color(0.88f, 0.90f, 0.94f), 0.92f, 0.90f)
                : GetOrCreateMat("IsuzuSteelRim", new Color(0.24f, 0.27f, 0.32f), 0.65f, 0.50f);
            Material hubMat = isBusAlcoa
                ? GetOrCreateMat("ScaniaBlueHub", new Color(0.11f, 0.30f, 0.85f), 0.6f, 0.7f)
                : GetOrCreateMat("IsuzuBlackHub", new Color(0.06f, 0.06f, 0.08f), 0.7f, 0.45f);

            float[] offsets = dualTires ? new[] { -width * 0.58f, width * 0.58f } : new[] { 0f };
            for (int i = 0; i < offsets.Length; i++)
            {
                float xOff = offsets[i];
                GameObject tire = CreateCylinder($"Tire_{i}", pivot.transform, new Vector3(xOff, 0f, 0f), new Vector3(radius * 2f, width * 0.5f, radius * 2f), rubber, false);
                tire.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);

                GameObject rim = CreateCylinder($"Rim_{i}", pivot.transform, new Vector3(xOff, 0f, 0f), new Vector3(radius * 1.24f, width * 0.52f, radius * 1.24f), rimMat, false);
                rim.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            }

            // Outer hub dome
            float hubOffset = (dualTires ? width * 0.62f : 0f) * (isLeft ? -1f : 1f);
            GameObject hub = CreateCylinder("HubCap", pivot.transform, new Vector3(hubOffset, 0f, 0f), new Vector3(radius * 0.54f, width * 0.56f, radius * 0.54f), hubMat, false);
            hub.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);

            return pivot.transform;
        }

        private static Transform CreateAnchor(string name, Transform parent, Vector3 localPos)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            return go.transform;
        }

        private static GameObject CreateBox(string name, Transform parent, Vector3 localPos, Vector3 scale, Material mat, bool keepCollider)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            if (!keepCollider) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        private static GameObject CreateCylinder(string name, Transform parent, Vector3 localPos, Vector3 scale, Material mat, bool keepCollider)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            if (!keepCollider) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        private static GameObject CreateSphere(string name, Transform parent, Vector3 localPos, Vector3 scale, Material mat, bool keepCollider)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            if (!keepCollider) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }
    }
}
#endif
