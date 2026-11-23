using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace KenyaStreetSim.Editor
{
    /// <summary>
    /// Modular realistic vehicle interior & exterior features builder:
    /// 1) Real 512x512 textured 3D Analog Speedometer (0-140) & Tachometer (RPM) Dials with live sweeping red needles
    /// 2) Sculpted 32x12 smooth circular leather Torus Steering Wheel + brand horn pad + brushed spokes + column stalks
    /// 3) Full ETS2-grade multi-part RHD Cab Dashboard (Infotainment / Police VHF Radio, AC vents, climate knobs,
    ///    gear lever, A-pillars, windshield sun visor) framed squarely in the driver's [C] sightline!
    /// 4) Dual Animated 3D Windshield Wipers ([K] key & auto-on in Kenyan Rain)
    /// 5) Animated Left-Side Curb Passenger Door ([O] key & auto-open at Bus Stages + warm amber step light!)
    /// </summary>
    public static class KenyaVehicleFeaturesBuilder
    {
        private const string TorusMeshPath = "Assets/KenyaStreetSim/Meshes/SculptedSteeringWheelTorus.asset";

        public static Mesh EnsureSteeringTorusMesh()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(TorusMeshPath);
            if (existing != null) return existing;

            string dir = Path.GetDirectoryName(TorusMeshPath);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            const int majorSegs = 32;
            const int minorSegs = 12;
            const float majorR = 0.205f;
            const float minorR = 0.020f;

            var verts = new Vector3[(majorSegs + 1) * (minorSegs + 1)];
            var norms = new Vector3[verts.Length];
            var uvs = new Vector2[verts.Length];
            var tris = new int[majorSegs * minorSegs * 6];

            int vi = 0;
            for (int i = 0; i <= majorSegs; i++)
            {
                float u = (float)i / majorSegs * Mathf.PI * 2f;
                float cu = Mathf.Cos(u), su = Mathf.Sin(u);
                Vector3 center = new Vector3(cu * majorR, 0f, su * majorR);
                for (int j = 0; j <= minorSegs; j++)
                {
                    float v = (float)j / minorSegs * Mathf.PI * 2f;
                    float cv = Mathf.Cos(v), sv = Mathf.Sin(v);
                    Vector3 n = new Vector3(cu * cv, sv, su * cv);
                    verts[vi] = center + n * minorR;
                    norms[vi] = n.normalized;
                    uvs[vi] = new Vector2((float)i / majorSegs * 4f, (float)j / minorSegs);
                    vi++;
                }
            }

            int ti = 0;
            int stride = minorSegs + 1;
            for (int i = 0; i < majorSegs; i++)
            {
                for (int j = 0; j < minorSegs; j++)
                {
                    int a = i * stride + j;
                    int b = (i + 1) * stride + j;
                    int c = (i + 1) * stride + (j + 1);
                    int d = i * stride + (j + 1);
                    tris[ti++] = a; tris[ti++] = b; tris[ti++] = c;
                    tris[ti++] = a; tris[ti++] = c; tris[ti++] = d;
                }
            }

            var mesh = new Mesh { name = "SculptedSteeringWheelTorus" };
            mesh.vertices = verts;
            mesh.normals = norms;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, TorusMeshPath);
            AssetDatabase.SaveAssets();
            return mesh;
        }

        /// <summary>
        /// Builds a genuine smooth circular leather-wrapped 3-spoke Steering Wheel + Horn Pad + Brand Badge + Column Stalks!
        /// </summary>
        public static Transform BuildSmoothSteeringWheel(
            Transform parent, Vector3 localPos, float tiltPitchDeg, float scaleFactor, string brandLabel,
            Material matLeather, Material matChrome, Material matDarkHub)
        {
            var colBase = new GameObject("SteeringColumnAssembly").transform;
            colBase.SetParent(parent, false);
            colBase.localPosition = localPos;
            // Negative X tilt points local +Y cleanly BACK toward the driver's face and local -Y FORWARD into the dashboard!
            colBase.localRotation = Quaternion.Euler(-tiltPitchDeg, 0f, 0f);

            // Steering column shaft + indicator/wiper stalks cleanly tucked BEHIND the wheel into the dash!
            Cyl("SteerColumnShaft", colBase, new Vector3(0f, -0.15f, 0f), new Vector3(0.082f, 0.14f, 0.082f), matDarkHub, Quaternion.identity);
            Box("LeftSignalStalk", colBase, new Vector3(-0.14f, -0.055f, 0.02f), new Vector3(0.16f, 0.015f, 0.016f), matDarkHub);
            Box("RightWiperStalk", colBase, new Vector3( 0.14f, -0.055f, 0.02f), new Vector3(0.16f, 0.015f, 0.016f), matDarkHub);

            // Rotating steering wheel pivot (rotates smoothly around local +Y!)
            var wheelPivot = new GameObject("SteeringWheelPivot").transform;
            wheelPivot.SetParent(colBase, false);
            wheelPivot.localPosition = Vector3.zero;
            wheelPivot.localRotation = Quaternion.identity;
            wheelPivot.localScale = Vector3.one * scaleFactor;

            var rimGo = new GameObject("SculptedLeatherRim");
            rimGo.transform.SetParent(wheelPivot, false);
            rimGo.AddComponent<MeshFilter>().sharedMesh = EnsureSteeringTorusMesh();
            rimGo.AddComponent<MeshRenderer>().sharedMaterial = matLeather;

            // Contoured center airbag horn pad + chrome ring + 3 brushed metallic spokes (facing driver!)
            Cyl("CenterAirbagPad", wheelPivot, new Vector3(0f, 0.012f, 0f), new Vector3(0.145f, 0.026f, 0.125f), matDarkHub, Quaternion.identity);
            Cyl("AirbagChromeBezel", wheelPivot, new Vector3(0f, 0.032f, 0f), new Vector3(0.072f, 0.008f, 0.054f), matChrome, Quaternion.identity);
            Box("SpokeLeft",   wheelPivot, new Vector3(-0.115f, 0f, -0.01f), new Vector3(0.165f, 0.022f, 0.042f), matChrome);
            Box("SpokeRight",  wheelPivot, new Vector3( 0.115f, 0f, -0.01f), new Vector3(0.165f, 0.022f, 0.042f), matChrome);
            Box("SpokeBottom", wheelPivot, new Vector3( 0.000f, 0f, -0.115f), new Vector3(0.050f, 0.022f, 0.165f), matChrome);

            if (!string.IsNullOrEmpty(brandLabel))
            {
                var bGo = new GameObject("WheelBrandBadge");
                bGo.transform.SetParent(wheelPivot, false);
                bGo.transform.localPosition = new Vector3(0f, 0.040f, 0f);
                bGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                var tmp = bGo.AddComponent<TextMeshPro>();
                tmp.text = brandLabel;
                tmp.fontSize = 0.13f;
                tmp.fontStyle = FontStyles.Bold;
                tmp.color = Color.white;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.rectTransform.sizeDelta = new Vector2(0.09f, 0.028f);
            }

            return wheelPivot;
        }

        /// <summary>
        /// Builds a complete, properly framed ETS2-style multi-part RHD Cab Dashboard + Smooth Steering Wheel +
        /// Center GPS/Police Console + AC Vents + A-Pillars + Sun Visor + Dual Wipers + Opening Curb Door!
        /// Guaranteed 100% visible and razor-sharp in [C] Cockpit View!
        /// </summary>
        public static KenyaVehicleDoorAndWipers BuildFullRealisticCabCockpit(
            GameObject vehicleRoot,
            Transform bodyRoot,
            Transform cockpitHost,
            float driverX,
            float halfW,
            float halfL,
            bool isCoach,
            bool isPoliceLC79,
            string brandName,
            string plateNum,
            Material matInterior,
            Material matBlack,
            Material matChrome,
            Material matBodyAccent,
            Material matClearGlass,
            out Transform steeringWheelPivot,
            out TextMeshPro dashSpeedTmp,
            out TextMeshPro dashStatusTmp,
            bool buildOuterPillars = true,
            Transform externalWiperParent = null,
            Vector3 externalWiperPos = default)
        {
            KenyaDashboardTextures.GenerateAll();
            var urpLit = Shader.Find("Universal Render Pipeline/Lit");

            Material matLeather    = MakeAssetMat("Dash_LeatherGrip", urpLit, new Color(0.11f, 0.12f, 0.14f, 1f), 0.34f, 0.08f, Color.black);
            Material matSoftDash   = MakeAssetMat("Dash_SoftCharcoal", urpLit, new Color(0.15f, 0.165f, 0.19f, 1f), 0.28f, 0.08f, Color.black);
            Material matTrimSilver = MakeAssetMat("Dash_BrushedAlum", urpLit, new Color(0.68f, 0.72f, 0.78f, 1f), 0.76f, 0.78f, Color.black);
            Material matInfoScreen = MakeTexturedScreenMat("Dash_InfotainmentScreen", urpLit, KenyaDashboardTextures.InfotainmentTex, 0.85f);
            Material matPoliceScreen = MakeTexturedScreenMat("Dash_PoliceVhfScreen", urpLit, KenyaDashboardTextures.PoliceRadioTex, 0.90f);
            Material matAcVent     = MakeTexturedScreenMat("Dash_AcVentMat", urpLit, KenyaDashboardTextures.AcVentTex, 0.0f);

            float cabW = Mathf.Clamp(halfW * 1.74f, 1.56f, 2.25f);

            // 1. Sculpted Multi-Tier Contoured Dashboard Deck (cleanly below windshield, spanning full cab width!)
            Box("DashUpperPad",    cockpitHost, new Vector3(0f, -0.08f, 0.24f), new Vector3(cabW, 0.10f, 0.50f), matSoftDash);
            Box("DashLowerBody",   cockpitHost, new Vector3(0f, -0.26f, 0.18f), new Vector3(cabW * 0.98f, 0.28f, 0.42f), matInterior);
            Box("DashSilverTrim",  cockpitHost, new Vector3(0f, -0.14f, 0.02f), new Vector3(cabW * 0.96f, 0.024f, 0.04f), matTrimSilver);

            // Driver Instrument Binnacle Hood framing the textured Speedometer & Tachometer dials!
            Box("DriverBinnacleTop",   cockpitHost, new Vector3(driverX, 0.125f, 0.14f), new Vector3(0.56f, 0.032f, 0.32f), matLeather);
            Box("DriverBinnacleSideL", cockpitHost, new Vector3(driverX - 0.27f, 0.03f, 0.14f), new Vector3(0.032f, 0.19f, 0.30f), matLeather);
            Box("DriverBinnacleSideR", cockpitHost, new Vector3(driverX + 0.27f, 0.03f, 0.14f), new Vector3(0.032f, 0.19f, 0.30f), matLeather);

            // 2. Center Console: GPS Infotainment Touchscreen (or Kenya Police VHF Radio & Siren Panel for KCP 380D!)
            float centerX = driverX - 0.48f; // Cleanly to driver's left in RHD cabin!
            Box("CenterConsoleStack", cockpitHost, new Vector3(centerX, -0.12f, 0.08f), new Vector3(0.38f, 0.40f, 0.26f), matLeather);
            var screenQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            screenQuad.name = isPoliceLC79 ? "PoliceVhfRadioScreen" : "GpsInfotainmentScreen";
            screenQuad.transform.SetParent(cockpitHost, false);
            screenQuad.transform.localPosition = new Vector3(centerX, -0.02f, -0.062f);
            screenQuad.transform.localRotation = Quaternion.identity;
            screenQuad.transform.localScale = new Vector3(0.33f, 0.195f, 1f);
            Object.DestroyImmediate(screenQuad.GetComponent<Collider>());
            screenQuad.GetComponent<Renderer>().sharedMaterial = isPoliceLC79 ? matPoliceScreen : matInfoScreen;

            // Louvered AC Air Vents (Driver Right, Center Left & Right) + 3 Rotary Climate Knobs!
            CreateTexturedQuad("AcVentDriverR", cockpitHost, new Vector3(driverX + 0.36f, -0.04f, -0.02f), new Vector3(0.14f, 0.095f, 1f), Quaternion.Euler(0f, -12f, 0f), matAcVent);
            CreateTexturedQuad("AcVentCenterL", cockpitHost, new Vector3(centerX - 0.10f, 0.095f, -0.01f), new Vector3(0.13f, 0.075f, 1f), Quaternion.Euler(0f, 6f, 0f), matAcVent);
            CreateTexturedQuad("AcVentCenterR", cockpitHost, new Vector3(centerX + 0.10f, 0.095f, -0.01f), new Vector3(0.13f, 0.075f, 1f), Quaternion.Euler(0f, 6f, 0f), matAcVent);
            for (int k = -1; k <= 1; k++)
            {
                Cyl($"ClimateKnob_{k}", cockpitHost, new Vector3(centerX + k * 0.085f, -0.175f, -0.06f), new Vector3(0.042f, 0.020f, 0.042f), matTrimSilver, Quaternion.Euler(90f, 0f, 0f));
            }

            // Realistic Gear Shifter Lever + Knob on driver's left center tunnel!
            var shifter = new GameObject("GearShiftLever").transform;
            shifter.SetParent(cockpitHost, false);
            shifter.localPosition = new Vector3(centerX + 0.14f, -0.34f, -0.18f);
            shifter.localRotation = Quaternion.Euler(14f, 0f, -6f);
            Cyl("ShiftBoot", shifter, new Vector3(0f, 0.03f, 0f), new Vector3(0.09f, 0.05f, 0.09f), matLeather, Quaternion.identity);
            Cyl("ShiftRod",  shifter, new Vector3(0f, 0.12f, 0f), new Vector3(0.016f, 0.11f, 0.016f), matChrome, Quaternion.identity);
            var knob = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            knob.name = "ShiftKnob";
            knob.transform.SetParent(shifter, false);
            knob.transform.localPosition = new Vector3(0f, 0.22f, 0f);
            knob.transform.localScale = new Vector3(0.052f, 0.058f, 0.052f);
            Object.DestroyImmediate(knob.GetComponent<Collider>());
            knob.GetComponent<Renderer>().sharedMaterial = matLeather;

            // Live 3D Digital Telemetry Readout cleanly above center screen (never colliding with gauges or steering wheel!)
            dashSpeedTmp = CreateDashText("CockpitSpeedTxt", cockpitHost, "00 km/h | G:N", new Vector3(centerX, 0.165f, -0.04f), new Vector2(0.36f, 0.055f), 0.30f, Palette.UiAccentGreen, Quaternion.Euler(0f, 8f, 0f));
            dashStatusTmp = CreateDashText("CockpitStatusTxt", cockpitHost, $"{plateNum} • KENYA", new Vector3(centerX, 0.125f, -0.04f), new Vector2(0.36f, 0.048f), 0.20f, Palette.UiAccentAmber, Quaternion.Euler(0f, 8f, 0f));

            // 3. REAL SCULPTED 32x12 SMOOTH CIRCULAR STEERING WHEEL (squarely visible in lower driver view!)
            float wheelScale = isCoach ? 1.06f : (isPoliceLC79 ? 0.94f : 1.00f);
            float wheelTilt = isCoach ? 56f : (isPoliceLC79 ? 68f : 62f);
            steeringWheelPivot = BuildSmoothSteeringWheel(
                cockpitHost,
                new Vector3(driverX, -0.045f, -0.14f),
                wheelTilt,
                wheelScale,
                brandName,
                matLeather,
                matTrimSilver,
                matBlack);

            // 4. Framing Cab A-Pillars + Overhead Windshield Header + Sun Visor (so imported mod cabs feel 100% enclosed & authentic!)
            if (buildOuterPillars)
            {
                float pilX = cabW * 0.49f;
                var pilL = Box("CabAPillarL", cockpitHost, new Vector3(-pilX, 0.48f, 0.24f), new Vector3(0.085f, 1.18f, 0.11f), matLeather);
                pilL.transform.localRotation = Quaternion.Euler(-14f, 0f, -4f);
                var pilR = Box("CabAPillarR", cockpitHost, new Vector3( pilX, 0.48f, 0.24f), new Vector3(0.085f, 1.18f, 0.11f), matLeather);
                pilR.transform.localRotation = Quaternion.Euler(-14f, 0f,  4f);
                Box("CabRoofHeader", cockpitHost, new Vector3(0f, 0.96f, 0.04f), new Vector3(cabW + 0.12f, 0.12f, 0.42f), matSoftDash);
                Box("DriverSunVisor", cockpitHost, new Vector3(driverX, 0.88f, -0.08f), new Vector3(0.56f, 0.11f, 0.025f), matLeather);
            }

            // 5. Textured Analog Speedo & RPM Cluster + Sweeping 3D Wipers + Animated Curb Passenger Door!
            Transform wiperHost = externalWiperParent != null ? externalWiperParent : cockpitHost;
            Vector3 wiperPos = externalWiperParent != null ? externalWiperPos : new Vector3(0.04f, 0.02f, 0.44f);
            float wiperSpan = isPoliceLC79 ? 0.36f : 0.44f;
            float wiperLen = isPoliceLC79 ? 0.46f : 0.56f;

            return AttachRealDashWipersAndDoor(
                vehicleRoot, bodyRoot, cockpitHost, new Vector3(driverX, 0.028f, 0.04f),
                wiperHost, wiperPos, wiperSpan, wiperLen,
                halfW, halfL, isCoach, isPoliceLC79,
                matBlack, matChrome, matBodyAccent, matClearGlass);
        }

        public static KenyaVehicleDoorAndWipers AttachRealDashWipersAndDoor(
            GameObject vehicleRoot,
            Transform bodyRoot,
            Transform dashParent,
            Vector3 gaugeClusterLocalPos,
            Transform wiperParent,
            Vector3 wiperCenterLocalPos,
            float wiperSpanX,
            float wiperBladeLen,
            float halfW,
            float halfL,
            bool isCoach,
            bool isPoliceLC79,
            Material matBlack,
            Material matChrome,
            Material matBodyAccent,
            Material matClearGlass)
        {
            KenyaDashboardTextures.GenerateAll();
            var urpLit = Shader.Find("Universal Render Pipeline/Lit");

            Material matSpeedoFace = MakeTexturedScreenMat("Dash_SpeedoDialFace", urpLit, KenyaDashboardTextures.SpeedoDialTex, 0.35f);
            Material matRpmFace    = MakeTexturedScreenMat("Dash_RpmDialFace", urpLit, KenyaDashboardTextures.RpmDialTex, 0.35f);
            Material matDialBack   = MakeAssetMat("Dash_DialFace", urpLit, new Color(0.05f, 0.06f, 0.08f, 1f), 0.35f, 0.1f, Color.black);
            Material matNeedleRed  = MakeAssetMat("Dash_NeedleRed", urpLit, new Color(0.98f, 0.16f, 0.10f, 1f), 0.5f, 0f, new Color(1.0f, 0.22f, 0.12f) * 2.6f);
            Material matLedGreen   = MakeAssetMat("Dash_LedGreen", urpLit, new Color(0.14f, 0.92f, 0.42f, 1f), 0.5f, 0f, new Color(0.14f, 0.96f, 0.44f) * 2.4f);
            Material matLedAmber   = MakeAssetMat("Dash_LedAmber", urpLit, new Color(1.0f, 0.72f, 0.10f, 1f), 0.5f, 0f, new Color(1.0f, 0.72f, 0.10f) * 2.2f);
            Material matLedCyan    = MakeAssetMat("Dash_LedCyan", urpLit, new Color(0.10f, 0.86f, 1.0f, 1f), 0.5f, 0f, new Color(0.10f, 0.88f, 1.0f) * 2.2f);
            Material matRubber     = MakeAssetMat("Dash_WiperRubber", urpLit, new Color(0.07f, 0.075f, 0.085f, 1f), 0.18f, 0.05f, Color.black);
            Material matWarmStep   = MakeAssetMat("Door_WarmStep", urpLit, new Color(1.0f, 0.84f, 0.45f, 1f), 0.6f, 0f, new Color(1.0f, 0.78f, 0.34f) * 2.8f);

            var feat = vehicleRoot.GetComponent<KenyaVehicleDoorAndWipers>();
            if (feat == null) feat = vehicleRoot.AddComponent<KenyaVehicleDoorAndWipers>();

            // =========================================================================
            // 1. REAL 512x512 TEXTURED 3D ANALOG INSTRUMENT CLUSTER (SPEEDO + RPM + NEEDLES + LEDs)
            // =========================================================================
            if (dashParent != null)
            {
                var cluster = new GameObject("RealAnalogInstrumentCluster").transform;
                cluster.SetParent(dashParent, false);
                cluster.localPosition = gaugeClusterLocalPos;
                cluster.localRotation = Quaternion.Euler(10f, 0f, 0f);

                Box("ClusterBackplate", cluster, new Vector3(0f, 0f, 0.015f), new Vector3(0.47f, 0.175f, 0.022f), matBlack);
                Box("ClusterTopVisor",  cluster, new Vector3(0f, 0.092f, -0.025f), new Vector3(0.49f, 0.018f, 0.085f), matRubber);

                // Left Dial: Textured Speedometer (0 - 140 km/h)
                feat.speedNeedlePivot = BuildTexturedAnalogDial(cluster, "SpeedoDial", new Vector3(-0.130f, 0.004f, 0f), 0.074f, "KM/H", matSpeedoFace, matChrome, matNeedleRed);
                // Right Dial: Textured Tachometer (RPM x1000 with green diesel torque band)
                feat.rpmNeedlePivot = BuildTexturedAnalogDial(cluster, "RpmDial", new Vector3(0.130f, 0.004f, 0f), 0.074f, "RPM", matRpmFace, matChrome, matNeedleRed);

                // Center Illuminated Warning & Status LEDs + Fuel/Temp Bar Display
                Box("LedGreen", cluster, new Vector3(-0.022f, 0.048f, -0.004f), new Vector3(0.014f, 0.014f, 0.008f), matLedGreen);
                Box("LedCyan",  cluster, new Vector3( 0.000f, 0.048f, -0.004f), new Vector3(0.014f, 0.014f, 0.008f), matLedCyan);
                Box("LedAmber", cluster, new Vector3( 0.022f, 0.048f, -0.004f), new Vector3(0.014f, 0.014f, 0.008f), matLedAmber);
                Box("CenterMiniLcd", cluster, new Vector3(0f, -0.012f, -0.003f), new Vector3(0.068f, 0.056f, 0.008f), matDialBack);
                Box("FuelBarFill",   cluster, new Vector3(0f, -0.024f, -0.008f), new Vector3(0.052f, 0.010f, 0.004f), matLedGreen);
                Box("TempBarFill",   cluster, new Vector3(0f, -0.006f, -0.008f), new Vector3(0.044f, 0.010f, 0.004f), matLedCyan);
            }

            // =========================================================================
            // 2. DUAL ANIMATED 3D WINDSHIELD WIPERS
            // =========================================================================
            if (wiperParent != null)
            {
                var wiperRoot = new GameObject("DualWindshieldWipers").transform;
                wiperRoot.SetParent(wiperParent, false);
                wiperRoot.localPosition = wiperCenterLocalPos;
                wiperRoot.localRotation = Quaternion.Euler(-12f, 0f, 0f);

                feat.wiperPivotL = BuildSingleWiper(wiperRoot, "WiperPivotL", new Vector3(-wiperSpanX, 0f, 0f), wiperBladeLen, matRubber, matBlack);
                feat.wiperPivotR = BuildSingleWiper(wiperRoot, "WiperPivotR", new Vector3( wiperSpanX, 0f, 0f), wiperBladeLen, matRubber, matBlack);
                feat.wiperSweepDeg = isCoach ? 52f : 48f;
            }

            // =========================================================================
            // 3. ANIMATED LEFT-SIDE CURB PASSENGER DOOR ([O] KEY & AUTO-OPEN AT BUS STAGES!)
            // =========================================================================
            var doorHolder = new GameObject("AnimatedPassengerDoorSystem").transform;
            doorHolder.SetParent(bodyRoot, false);

            float doorW = isCoach ? 1.06f : (isPoliceLC79 ? 0.92f : 0.88f);
            float doorH = isCoach ? 2.18f : (isPoliceLC79 ? 1.04f : 1.48f);
            float doorY = isCoach ? 1.42f : (isPoliceLC79 ? 1.32f : 1.44f);
            float doorZ = isCoach ? (halfL * 0.66f) : (isPoliceLC79 ? 0.52f : (halfL * 0.54f));
            float doorX = -halfW - 0.028f;

            if (isCoach)
            {
                Box("DoorEntryWellBack", doorHolder, new Vector3(doorX + 0.045f, doorY, doorZ), new Vector3(0.04f, doorH - 0.06f, doorW - 0.06f), matRubber);
                Box("DoorGlowStep1", doorHolder, new Vector3(doorX + 0.025f, 0.52f, doorZ), new Vector3(0.10f, 0.06f, doorW * 0.86f), matWarmStep);
                Box("DoorGlowStep2", doorHolder, new Vector3(doorX + 0.025f, 0.82f, doorZ), new Vector3(0.10f, 0.06f, doorW * 0.86f), matWarmStep);
                Box("DoorHandrailL", doorHolder, new Vector3(doorX + 0.015f, doorY - 0.10f, doorZ + doorW * 0.40f), new Vector3(0.035f, 1.35f, 0.035f), matChrome);
            }

            var doorPivot = new GameObject("PassengerDoorLeafPivot").transform;
            doorPivot.SetParent(doorHolder, false);
            if (isCoach)
            {
                doorPivot.localPosition = new Vector3(doorX, doorY, doorZ);
                Box("DoorFrameOuter", doorPivot, Vector3.zero, new Vector3(0.055f, doorH, doorW), matBlack);
                Box("DoorLowerPanel", doorPivot, new Vector3(-0.008f, -doorH * 0.24f, 0f), new Vector3(0.058f, doorH * 0.46f, doorW * 0.90f), matBodyAccent);
                Box("DoorUpperGlass", doorPivot, new Vector3(-0.008f,  doorH * 0.21f, 0f), new Vector3(0.035f, doorH * 0.48f, doorW * 0.86f), matClearGlass);
                Box("DoorCenterSeam", doorPivot, new Vector3(-0.020f, 0f, 0f), new Vector3(0.022f, doorH * 0.96f, 0.035f), matRubber);
                Box("DoorChromeHandle", doorPivot, new Vector3(-0.038f, -0.10f, 0.14f), new Vector3(0.03f, 0.24f, 0.035f), matChrome);

                feat.passengerDoorPivot = doorPivot;
                feat.doorClosedLocalPos = doorPivot.localPosition;
                feat.doorOpenLocalPos = doorPivot.localPosition + new Vector3(-0.24f, 0f, -doorW * 0.86f);
                feat.doorClosedLocalEuler = Vector3.zero;
                feat.doorOpenLocalEuler = Vector3.zero;
            }
            else
            {
                Vector3 hingePos = new Vector3(doorX, doorY, doorZ + doorW * 0.48f);
                doorPivot.localPosition = hingePos;
                Box("SwingDoorFrame", doorPivot, new Vector3(0f, 0f, -doorW * 0.48f), new Vector3(0.05f, doorH, doorW), matBodyAccent);
                Box("SwingDoorGlass", doorPivot, new Vector3(-0.005f, doorH * 0.22f, -doorW * 0.48f), new Vector3(0.03f, doorH * 0.44f, doorW * 0.84f), matClearGlass);
                Box("SwingDoorHandle", doorPivot, new Vector3(-0.035f, -0.05f, -doorW * 0.82f), new Vector3(0.028f, 0.05f, 0.14f), matChrome);

                feat.passengerDoorPivot = doorPivot;
                feat.doorClosedLocalPos = hingePos;
                feat.doorOpenLocalPos = hingePos;
                feat.doorClosedLocalEuler = Vector3.zero;
                feat.doorOpenLocalEuler = new Vector3(0f, -66f, 0f);
            }

            var stepLightGo = new GameObject("PassengerDoorStepLight");
            stepLightGo.transform.SetParent(doorHolder, false);
            stepLightGo.transform.localPosition = new Vector3(doorX - 0.35f, doorY + 0.55f, doorZ);
            var stepLight = stepLightGo.AddComponent<Light>();
            stepLight.type = LightType.Point;
            stepLight.color = new Color(1.0f, 0.84f, 0.52f);
            stepLight.range = 6.5f;
            stepLight.intensity = 5.5f;
            stepLight.enabled = false;
            feat.doorStepLight = stepLight;

            var boardPt = new GameObject("BoardingDoorPoint").transform;
            boardPt.SetParent(doorHolder, false);
            boardPt.localPosition = new Vector3(doorX - 0.55f, 0f, doorZ);
            feat.boardingDoorPoint = boardPt;

            return feat;
        }

        private static Transform BuildTexturedAnalogDial(
            Transform parent, string name, Vector3 localPos, float radius, string unitLabel,
            Material matPrintedFace, Material matChrome, Material matNeedleRed)
        {
            var dialRoot = new GameObject(name).transform;
            dialRoot.SetParent(parent, false);
            dialRoot.localPosition = localPos;

            // Chrome cylindrical bezel + crisp 512x512 textured circular dial face quad!
            Cyl("Bezel", dialRoot, new Vector3(0f, 0f, 0.002f), new Vector3(radius * 2.12f, 0.008f, radius * 2.12f), matChrome, Quaternion.Euler(90f, 0f, 0f));

            var faceQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            faceQuad.name = "PrintedDialFace";
            faceQuad.transform.SetParent(dialRoot, false);
            faceQuad.transform.localPosition = new Vector3(0f, 0f, -0.005f);
            faceQuad.transform.localScale = new Vector3(radius * 1.94f, radius * 1.94f, 1f);
            Object.DestroyImmediate(faceQuad.GetComponent<Collider>());
            faceQuad.GetComponent<Renderer>().sharedMaterial = matPrintedFace;

            // Live Sweeping 3D Crimson Needle Pivot!
            var needlePivot = new GameObject("NeedlePivot").transform;
            needlePivot.SetParent(dialRoot, false);
            needlePivot.localPosition = new Vector3(0f, 0f, -0.009f);
            needlePivot.localRotation = Quaternion.Euler(0f, 0f, 120f);
            Box("RedNeedle", needlePivot, new Vector3(0f, radius * 0.34f, 0f), new Vector3(0.0055f, radius * 0.74f, 0.004f), matNeedleRed);
            Cyl("NeedleCap", dialRoot, new Vector3(0f, 0f, -0.011f), new Vector3(0.018f, 0.006f, 0.018f), matChrome, Quaternion.Euler(90f, 0f, 0f));

            CreateDashText("UnitTxt", dialRoot, unitLabel, new Vector3(0f, -radius * 0.38f, -0.008f), new Vector2(0.09f, 0.025f), 0.14f, new Color(0.86f, 0.92f, 0.98f, 1f), Quaternion.identity);
            return needlePivot;
        }

        private static Transform BuildSingleWiper(Transform parent, string name, Vector3 localPos, float bladeLen, Material matRubber, Material matBlack)
        {
            var pivot = new GameObject(name).transform;
            pivot.SetParent(parent, false);
            pivot.localPosition = localPos;
            pivot.localRotation = Quaternion.Euler(0f, 0f, -18f);

            Cyl("Spindle", pivot, Vector3.zero, new Vector3(0.030f, 0.028f, 0.030f), matBlack, Quaternion.Euler(90f, 0f, 0f));
            Box("Arm",   pivot, new Vector3(0f, bladeLen * 0.34f, 0f), new Vector3(0.016f, bladeLen * 0.68f, 0.014f), matBlack);
            Box("Blade", pivot, new Vector3(0f, bladeLen * 0.72f, -0.010f), new Vector3(0.022f, bladeLen * 0.66f, 0.012f), matRubber);
            return pivot;
        }

        private static void CreateTexturedQuad(string name, Transform parent, Vector3 localPos, Vector3 scale, Quaternion rot, Material mat)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = name;
            q.transform.SetParent(parent, false);
            q.transform.localPosition = localPos;
            q.transform.localRotation = rot;
            q.transform.localScale = scale;
            Object.DestroyImmediate(q.GetComponent<Collider>());
            q.GetComponent<Renderer>().sharedMaterial = mat;
        }

        private static TextMeshPro CreateDashText(string name, Transform parent, string text, Vector3 localPos, Vector2 size, float fontSize, Color col, Quaternion rot)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = rot;
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = col;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.rectTransform.sizeDelta = size;
            return tmp;
        }

        private static GameObject Box(string name, Transform parent, Vector3 localPos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        private static GameObject Cyl(string name, Transform parent, Vector3 localPos, Vector3 scale, Material mat, Quaternion localRot)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot;
            go.transform.localScale = scale;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        private static Material MakeTexturedScreenMat(string matName, Shader shader, Texture2D tex, float emissionBoost)
        {
            string path = $"Assets/KenyaStreetSim/Materials/{matName}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = shader;
            m.SetTexture("_BaseMap", tex);
            m.mainTexture = tex;
            m.SetColor("_BaseColor", new Color(0.82f, 0.85f, 0.90f, 1f));
            m.SetFloat("_Smoothness", 0.18f);
            m.SetFloat("_Metallic", 0.02f);
            if (emissionBoost > 0.01f && tex != null)
            {
                m.EnableKeyword("_EMISSION");
                m.SetTexture("_EmissionMap", tex);
                m.SetColor("_EmissionColor", Color.white * emissionBoost);
            }
            else
            {
                m.DisableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", Color.black);
            }
            EditorUtility.SetDirty(m);
            return m;
        }

        private static Material MakeAssetMat(string matName, Shader shader, Color baseCol, float smooth, float metal, Color emission)
        {
            string path = $"Assets/KenyaStreetSim/Materials/{matName}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = shader;
            m.SetColor("_BaseColor", baseCol);
            m.SetFloat("_Smoothness", smooth);
            m.SetFloat("_Metallic", metal);
            if (emission.maxColorComponent > 0.01f)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emission);
            }
            else
            {
                m.DisableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", Color.black);
            }
            EditorUtility.SetDirty(m);
            return m;
        }
    }
}
