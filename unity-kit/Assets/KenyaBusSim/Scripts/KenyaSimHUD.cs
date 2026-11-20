using UnityEngine;

namespace KenyaBusSim
{
    /// <summary>
    /// Crisp, zero-dependency ETS2-style HUD for Kenya Bus & Truck Simulator in Unity 6.
    /// Shows vehicle badge, speed (km/h), automatic gear, RPM bar, Passenger Comfort /
    /// EPH Parcel Integrity meter, speed-bump warnings, and clickable quick actions.
    /// </summary>
    public class KenyaSimHUD : MonoBehaviour
    {
        public KenyaFleetManager fleetManager;
        public KenyaSimCamera simCamera;

        private Texture2D panelBgTex;
        private Texture2D barBgTex;
        private Texture2D whiteTex;
        private GUIStyle titleStyle;
        private GUIStyle subStyle;
        private GUIStyle speedStyle;
        private GUIStyle gearStyle;
        private GUIStyle labelStyle;
        private GUIStyle alertStyle;
        private GUIStyle buttonStyle;
        private bool stylesReady;

        private void Start()
        {
            if (fleetManager == null)
            {
#if UNITY_6000_0_OR_NEWER
                fleetManager = FindFirstObjectByType<KenyaFleetManager>();
#else
                fleetManager = FindObjectOfType<KenyaFleetManager>();
#endif
            }
            if (simCamera == null)
            {
#if UNITY_6000_0_OR_NEWER
                simCamera = FindFirstObjectByType<KenyaSimCamera>();
#else
                simCamera = FindObjectOfType<KenyaSimCamera>();
#endif
            }
        }

        private void InitStyles()
        {
            if (stylesReady) return;
            stylesReady = true;

            panelBgTex = MakeTex(new Color(0.06f, 0.09f, 0.16f, 0.86f));
            barBgTex = MakeTex(new Color(0.20f, 0.25f, 0.34f, 0.95f));
            whiteTex = MakeTex(Color.white);

            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };

            subStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                normal = { textColor = new Color(0.62f, 0.72f, 0.84f) }
            };

            speedStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 46,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.06f, 0.73f, 0.51f) }
            };

            gearStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 24,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.98f, 0.75f, 0.14f) }
            };

            labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.85f, 0.90f, 0.96f) }
            };

            alertStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 17,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white, background = MakeTex(new Color(0.80f, 0.14f, 0.14f, 0.92f)) }
            };

            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
        }

        private static Texture2D MakeTex(Color col)
        {
            Texture2D tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, col);
            tex.Apply();
            return tex;
        }

        private void OnGUI()
        {
            InitStyles();

            KenyaVehicleController v = fleetManager != null ? fleetManager.ActiveVehicle : null;
            if (v == null) return;

            // Scale consistently on 1080p / 720p / HiDPI monitors
            float scale = Mathf.Clamp(Screen.height / 760f, 0.85f, 1.45f);
            Matrix4x4 prevMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));

            float uiWidth = Screen.width / scale;
            float uiHeight = Screen.height / scale;

            // --- TOP-LEFT TELEMETRY PANEL ---
            Rect hudRect = new Rect(18, 18, 330, 198);
            GUI.DrawTexture(hudRect, panelBgTex);

            Color badgeCol = v.vehicleClass == KenyanVehicleClass.EnaCoachBus
                ? new Color(0.86f, 0.15f, 0.15f)
                : new Color(0.96f, 0.62f, 0.04f);

            DrawSolidRect(new Rect(30, 30, 118, 22), badgeCol);
            GUIStyle badgeText = new GUIStyle(labelStyle) { fontSize = 11, alignment = TextAnchor.MiddleCenter };
            badgeText.normal.textColor = v.vehicleClass == KenyanVehicleClass.EnaCoachBus ? Color.white : Color.black;
            GUI.Label(new Rect(30, 30, 118, 22), v.companyTag, badgeText);

            GUI.Label(new Rect(30, 54, 305, 24), v.displayName, titleStyle);
            GUI.Label(new Rect(30, 76, 305, 18), v.routeLabel, subStyle);

            // Speed & Gear
            GUI.Label(new Rect(30, 96, 120, 52), Mathf.RoundToInt(v.SpeedKmh).ToString(), speedStyle);
            GUI.Label(new Rect(118, 122, 65, 24), "KM/H", labelStyle);

            DrawSolidRect(new Rect(262, 106, 68, 36), new Color(0.98f, 0.75f, 0.14f, 0.18f));
            GUI.Label(new Rect(262, 106, 68, 36), v.CurrentGear, gearStyle);

            // RPM Bar
            float rpmFill = Mathf.Clamp01((v.CurrentRpm - 650f) / 1750f);
            DrawBar(new Rect(30, 152, 300, 7), rpmFill, rpmFill > 0.82f ? new Color(0.94f, 0.27f, 0.27f) : new Color(0.22f, 0.74f, 0.97f));

            // Condition Meter (Passenger Comfort / Parcel Integrity)
            string meterName = v.vehicleClass == KenyanVehicleClass.EnaCoachBus ? "Passenger Comfort" : "EPH Parcel Integrity";
            GUI.Label(new Rect(30, 166, 220, 20), meterName, labelStyle);
            GUI.Label(new Rect(270, 166, 60, 20), Mathf.RoundToInt(v.conditionScore) + "%", labelStyle);

            Color scoreColor = v.conditionScore > 65f
                ? new Color(0.06f, 0.73f, 0.51f)
                : (v.conditionScore > 35f ? new Color(0.96f, 0.62f, 0.04f) : new Color(0.94f, 0.27f, 0.27f));
            DrawBar(new Rect(30, 190, 300, 10), v.conditionScore / 100f, scoreColor);

            // --- TOP-CENTER ALERT / SPEED BUMP BANNER ---
            string banner = !string.IsNullOrEmpty(v.ActiveAlertMessage)
                ? v.ActiveAlertMessage
                : (v.NearSpeedBump ? "PUNGUZA MWENDO: SPEED BUMP AHEAD! (Slow below 25 km/h)" : "");

            if (!string.IsNullOrEmpty(banner))
            {
                Rect alertRect = new Rect((uiWidth - 540f) * 0.5f, 22, 540, 42);
                GUI.Box(alertRect, banner, alertStyle);
            }

            // --- TOP-RIGHT CONTROLS & FLEET SWITCHER ---
            Rect ctrlRect = new Rect(uiWidth - 268, 18, 250, 222);
            GUI.DrawTexture(ctrlRect, panelBgTex);

            GUI.Label(new Rect(uiWidth - 254, 28, 224, 22), "Controls (Kenya LHT / RHD)", titleStyle);
            GUI.Label(new Rect(uiWidth - 254, 54, 224, 18), "W / S : Drive / Brake / Reverse", subStyle);
            GUI.Label(new Rect(uiWidth - 254, 72, 224, 18), "A / D : Steer   |   Space : Handbrake", subStyle);
            GUI.Label(new Rect(uiWidth - 254, 90, 224, 18), "H : Horn   |   G : NTSA 80 km/h Limit", subStyle);
            string camText = simCamera != null ? simCamera.CurrentModeLabel : "Chase";
            GUI.Label(new Rect(uiWidth - 254, 108, 224, 18), "C : Camera (" + camText + ")", subStyle);

            if (GUI.Button(new Rect(uiWidth - 254, 134, 222, 28), "Switch Vehicle (V)", buttonStyle))
            {
                fleetManager.SwitchNextVehicle();
            }

            if (GUI.Button(new Rect(uiWidth - 254, 168, 107, 28), "Camera (C)", buttonStyle) && simCamera != null)
            {
                simCamera.CycleViewMode();
            }

            if (GUI.Button(new Rect(uiWidth - 139, 168, 107, 28), "Horn (H)", buttonStyle))
            {
                KenyaVehicleAudio audio = v.GetComponent<KenyaVehicleAudio>();
                if (audio != null) audio.PlayHorn();
            }

            string govLabel = v.ntsaSpeedGovernorEnabled ? "NTSA Limit: ON (80)" : "NTSA Limit: OFF";
            if (GUI.Button(new Rect(uiWidth - 254, 202, 222, 26), govLabel, buttonStyle))
            {
                v.ntsaSpeedGovernorEnabled = !v.ntsaSpeedGovernorEnabled;
            }

            // Bottom subtle hint
            GUI.Label(new Rect(18, uiHeight - 28, 560, 22),
                "Tip: Hold Right Mouse Button to look around / orbit. Drop custom .FBX/.GLB models into VisualBodyRoot!",
                subStyle);

            GUI.matrix = prevMatrix;
        }

        private void DrawBar(Rect rect, float fill01, Color fillColor)
        {
            GUI.DrawTexture(rect, barBgTex);
            Rect inner = new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(fill01), rect.height);
            DrawSolidRect(inner, fillColor);
        }

        private void DrawSolidRect(Rect rect, Color color)
        {
            Color prev = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, whiteTex);
            GUI.color = prev;
        }
    }
}
