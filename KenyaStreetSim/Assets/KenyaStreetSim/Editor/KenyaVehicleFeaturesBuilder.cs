using TMPro;
using UnityEditor;
using UnityEngine;

namespace KenyaStreetSim.Editor
{
    /// <summary>
    /// Modular vehicle realism builder adding:
    /// 1) Real 3D Analog Dashboard Gauges (Speedometer 0-140 & Tachometer RPM with sweeping crimson needles + glowing LEDs + front cowl/hood brow)
    /// 2) Dual Animated 3D Windshield Wipers ([K] key & auto-on in Kenyan Rain)
    /// 3) Animated Left-Side Curb Passenger Door ([O] key & auto-open at Bus Stages/Parcel Depots + warm amber step light + passenger boarding target!)
    /// </summary>
    public static class KenyaVehicleFeaturesBuilder
    {
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
            var urpLit = Shader.Find("Universal Render Pipeline/Lit");

            Material matDialFace = MakeAssetMat("Dash_DialFace", urpLit, new Color(0.06f, 0.08f, 0.11f, 1f), 0.35f, 0.1f, new Color(0.03f, 0.05f, 0.09f));
            Material matNeedleRed = MakeAssetMat("Dash_NeedleRed", urpLit, new Color(0.95f, 0.14f, 0.12f, 1f), 0.5f, 0f, new Color(1.0f, 0.18f, 0.12f) * 2.2f);
            Material matLedGreen = MakeAssetMat("Dash_LedGreen", urpLit, new Color(0.14f, 0.92f, 0.42f, 1f), 0.5f, 0f, new Color(0.14f, 0.96f, 0.44f) * 2.4f);
            Material matLedAmber = MakeAssetMat("Dash_LedAmber", urpLit, new Color(1.0f, 0.72f, 0.10f, 1f), 0.5f, 0f, new Color(1.0f, 0.72f, 0.10f) * 2.2f);
            Material matLedCyan  = MakeAssetMat("Dash_LedCyan", urpLit, new Color(0.10f, 0.86f, 1.0f, 1f), 0.5f, 0f, new Color(0.10f, 0.88f, 1.0f) * 2.2f);
            Material matRubber   = MakeAssetMat("Dash_WiperRubber", urpLit, new Color(0.07f, 0.075f, 0.085f, 1f), 0.18f, 0.05f, Color.black);
            Material matWarmStep = MakeAssetMat("Door_WarmStep", urpLit, new Color(1.0f, 0.84f, 0.45f, 1f), 0.6f, 0f, new Color(1.0f, 0.78f, 0.34f) * 2.8f);

            var feat = vehicleRoot.GetComponent<KenyaVehicleDoorAndWipers>();
            if (feat == null) feat = vehicleRoot.AddComponent<KenyaVehicleDoorAndWipers>();

            // =========================================================================
            // 1. REAL 3D ANALOG INSTRUMENT CLUSTER (TWIN DIALS + SWEEPING RED NEEDLES + LEDs + FRONT COWL)
            // =========================================================================
            if (dashParent != null)
            {
                var cluster = new GameObject("RealAnalogInstrumentCluster").transform;
                cluster.SetParent(dashParent, false);
                cluster.localPosition = gaugeClusterLocalPos;
                cluster.localRotation = Quaternion.Euler(14f, 0f, 0f);

                // Recessed dark binnacle housing + chrome trim rim
                Box("ClusterBackplate", cluster, new Vector3(0f, 0f, 0.014f), new Vector3(0.46f, 0.165f, 0.022f), matBlack);
                Box("ClusterTopVisor", cluster, new Vector3(0f, 0.088f, -0.025f), new Vector3(0.48f, 0.018f, 0.08f), matRubber);

                // Left Dial: SPEEDOMETER (0 - 140 km/h)
                feat.speedNeedlePivot = BuildAnalogDial(cluster, "SpeedoDial", new Vector3(-0.125f, 0.005f, 0f), 0.068f, "KM/H", matDialFace, matChrome, matNeedleRed);
                // Right Dial: TACHOMETER (RPM x1000)
                feat.rpmNeedlePivot = BuildAnalogDial(cluster, "RpmDial", new Vector3(0.125f, 0.005f, 0f), 0.068f, "RPM", matDialFace, matChrome, matNeedleRed);

                // Center Mini Status LEDs (Neutral/Cruise Green, Air Pressure Amber, GPS Cyan) + Small Fuel/Temp Bars
                Box("LedGreen", cluster, new Vector3(-0.022f, 0.044f, -0.004f), new Vector3(0.014f, 0.014f, 0.008f), matLedGreen);
                Box("LedCyan",  cluster, new Vector3( 0.000f, 0.044f, -0.004f), new Vector3(0.014f, 0.014f, 0.008f), matLedCyan);
                Box("LedAmber", cluster, new Vector3( 0.022f, 0.044f, -0.004f), new Vector3(0.014f, 0.014f, 0.008f), matLedAmber);
                Box("CenterMiniLcd", cluster, new Vector3(0f, -0.012f, -0.003f), new Vector3(0.066f, 0.052f, 0.008f), matDialFace);
                Box("FuelBarFill", cluster, new Vector3(0f, -0.024f, -0.008f), new Vector3(0.050f, 0.010f, 0.004f), matLedGreen);
                Box("TempBarFill", cluster, new Vector3(0f, -0.006f, -0.008f), new Vector3(0.042f, 0.010f, 0.004f), matLedCyan);

                // Visible lower bus nose / front windshield cowl brow extending ahead in [C] cockpit view!
                if (!isPoliceLC79)
                {
                    float cowlW = Mathf.Clamp(halfW * 1.62f, 1.55f, 2.15f);
                    Box("VisibleBusFrontCowl", dashParent, new Vector3(0f, -0.04f, 0.44f), new Vector3(cowlW, 0.14f, 0.36f), matBodyAccent);
                    Box("VisibleFrontNoseLip", dashParent, new Vector3(0f, 0.032f, 0.56f), new Vector3(cowlW * 0.94f, 0.026f, 0.14f), matChrome);
                }
            }

            // =========================================================================
            // 2. DUAL ANIMATED 3D WINDSHIELD WIPERS (SWEEP CLEANLY ACROSS GLASS!)
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
            // 3. ANIMATED LEFT-SIDE CURB PASSENGER DOOR ([O] KEY & AUTO-OPEN AT STAGES!)
            // =========================================================================
            var doorHolder = new GameObject("AnimatedPassengerDoorSystem").transform;
            doorHolder.SetParent(bodyRoot, false);

            float doorW = isCoach ? 1.06f : (isPoliceLC79 ? 0.92f : 0.88f);
            float doorH = isCoach ? 2.18f : (isPoliceLC79 ? 1.04f : 1.48f);
            float doorY = isCoach ? 1.42f : (isPoliceLC79 ? 1.32f : 1.44f);
            float doorZ = isCoach ? (halfL * 0.66f) : (isPoliceLC79 ? 0.52f : (halfL * 0.54f));
            float doorX = -halfW - 0.028f; // Left curb side in Kenya Left-Hand Traffic!

            // Dark illuminated stepwell backing frame behind the sliding door so opening the door reveals a glowing bus interior entryway!
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
                // Authentic Kenyan Coach pneumatic outward-plug sliding passenger door!
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
                // Swinging cab passenger door (hinged at front edge so it swings open smoothly to welcome passengers/officers!)
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

            // Warm interior boarding step light illuminating the sidewalk when door opens!
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

            // Exact world target point right outside the open door where 3D passengers walk to board!
            var boardPt = new GameObject("BoardingDoorPoint").transform;
            boardPt.SetParent(doorHolder, false);
            boardPt.localPosition = new Vector3(doorX - 0.55f, 0f, doorZ);
            feat.boardingDoorPoint = boardPt;

            return feat;
        }

        private static Transform BuildAnalogDial(
            Transform parent, string name, Vector3 localPos, float radius, string unitLabel,
            Material matFace, Material matChrome, Material matNeedleRed)
        {
            var dialRoot = new GameObject(name).transform;
            dialRoot.SetParent(parent, false);
            dialRoot.localPosition = localPos;

            // Circular chrome bezel + matte carbon dial face
            Cyl("Bezel", dialRoot, new Vector3(0f, 0f, 0.002f), new Vector3(radius * 2.10f, 0.008f, radius * 2.10f), matChrome, Quaternion.Euler(90f, 0f, 0f));
            Cyl("Face",  dialRoot, new Vector3(0f, 0f, -0.002f), new Vector3(radius * 1.92f, 0.006f, radius * 1.92f), matFace, Quaternion.Euler(90f, 0f, 0f));

            // 9 crisp radial white/chrome tick marks sweeping from -120° to +120°
            for (int t = 0; t <= 8; t++)
            {
                float ang = Mathf.Lerp(120f, -120f, t / 8f) * Mathf.Deg2Rad;
                float rTick = radius * 0.78f;
                Vector3 tp = new Vector3(-Mathf.Sin(ang) * rTick, Mathf.Cos(ang) * rTick, -0.006f);
                var tick = Box($"Tick_{t}", dialRoot, tp, new Vector3(0.004f, radius * 0.18f, 0.003f), t >= 7 ? matNeedleRed : matChrome);
                tick.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(120f, -120f, t / 8f));
            }

            // Sweeping 3D Crimson Red Needle Pivot
            var needlePivot = new GameObject("NeedlePivot").transform;
            needlePivot.SetParent(dialRoot, false);
            needlePivot.localPosition = new Vector3(0f, 0f, -0.009f);
            needlePivot.localRotation = Quaternion.Euler(0f, 0f, 120f);
            Box("RedNeedle", needlePivot, new Vector3(0f, radius * 0.34f, 0f), new Vector3(0.005f, radius * 0.74f, 0.004f), matNeedleRed);
            Cyl("NeedleCap", dialRoot, new Vector3(0f, 0f, -0.011f), new Vector3(0.016f, 0.005f, 0.016f), matChrome, Quaternion.Euler(90f, 0f, 0f));

            // Small unit label ("KM/H" / "RPM")
            var txtGo = new GameObject("UnitTxt");
            txtGo.transform.SetParent(dialRoot, false);
            txtGo.transform.localPosition = new Vector3(0f, -radius * 0.44f, -0.008f);
            var tmp = txtGo.AddComponent<TextMeshPro>();
            tmp.text = unitLabel;
            tmp.fontSize = 0.14f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = new Color(0.84f, 0.90f, 0.96f, 1f);
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.rectTransform.sizeDelta = new Vector2(0.09f, 0.025f);

            return needlePivot;
        }

        private static Transform BuildSingleWiper(Transform parent, string name, Vector3 localPos, float bladeLen, Material matRubber, Material matBlack)
        {
            var pivot = new GameObject(name).transform;
            pivot.SetParent(parent, false);
            pivot.localPosition = localPos;
            pivot.localRotation = Quaternion.Euler(0f, 0f, -18f);

            // Pivot spindle + angled steel wiper arm + rubber squeegee blade!
            Cyl("Spindle", pivot, Vector3.zero, new Vector3(0.032f, 0.03f, 0.032f), matBlack, Quaternion.Euler(90f, 0f, 0f));
            Box("Arm",   pivot, new Vector3(0f, bladeLen * 0.34f, 0f), new Vector3(0.018f, bladeLen * 0.68f, 0.016f), matBlack);
            Box("Blade", pivot, new Vector3(0f, bladeLen * 0.72f, -0.012f), new Vector3(0.024f, bladeLen * 0.66f, 0.014f), matRubber);
            return pivot;
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
