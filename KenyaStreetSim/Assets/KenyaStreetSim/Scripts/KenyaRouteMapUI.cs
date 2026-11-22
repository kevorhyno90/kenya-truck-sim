using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace KenyaStreetSim
{
    /// <summary>
    /// ETS2-style Live Corner GPS Minimap + Interactive Full-Screen [M] Kenya ENA Coach Route Map.
    /// Shows all 5 connected Kenyan highways, both roundabouts, all 10 ENA Coach town terminals,
    /// player live heading arrow, and lets the driver set GPS destination or fast-travel to any Kenyan town!
    /// </summary>
    public class KenyaRouteMapUI : MonoBehaviour
    {
        public bool IsFullMapOpen { get; private set; }

        private Canvas mapCanvas;
        private Sprite solidSprite;
        private Sprite circleSprite;

        // Live corner GPS minimap
        private GameObject miniMapRoot;
        private RectTransform miniPlayerArrow;
        private RectTransform miniTargetRing;
        private TextMeshProUGUI miniLocationText;
        private Vector2 miniMapSize = new Vector2(268f, 152f);

        // Full-screen interactive [M] Kenya ENA Coach Map
        private GameObject fullMapOverlay;
        private RectTransform fullMapPlot;
        private RectTransform fullPlayerArrow;
        private RectTransform fullSelectedRing;
        private Vector2 fullPlotSize = new Vector2(1060f, 560f);

        private int selectedHubIndex;
        private TextMeshProUGUI detailTitleText;
        private TextMeshProUGUI detailInfoText;
        private readonly List<Image> hubPinImages = new List<Image>();

        private void Awake()
        {
            solidSprite = CreateSolidSprite();
            circleSprite = CreateCircleSprite(32);
            BuildMapCanvas();
        }

        private void Update()
        {
            var loop = StreetGameLoop.Instance;
            if (loop == null) return;

            bool playing = loop.State == StreetGameLoop.RunState.Running;
            if (miniMapRoot != null)
            {
                miniMapRoot.SetActive(playing && !IsFullMapOpen);
            }

            if (playing || IsFullMapOpen)
            {
                if (WasMapKeyPressed())
                {
                    ToggleFullMap();
                }
                else if (IsFullMapOpen && WasEscPressed())
                {
                    SetFullMapOpen(false);
                }
            }
            else if (IsFullMapOpen)
            {
                SetFullMapOpen(false);
            }

            UpdatePlayerAndTargetMarkers(loop);
        }

        public void ToggleFullMap()
        {
            SetFullMapOpen(!IsFullMapOpen);
        }

        public void SetFullMapOpen(bool open)
        {
            var loop = StreetGameLoop.Instance;
            if (open && (loop == null || loop.State != StreetGameLoop.RunState.Running))
            {
                return;
            }

            IsFullMapOpen = open;
            if (fullMapOverlay != null) fullMapOverlay.SetActive(open);
            Time.timeScale = open ? 0f : (loop != null && loop.State == StreetGameLoop.RunState.Paused ? 0f : 1f);

            if (open)
            {
                int activeIdx = loop != null ? Mathf.Clamp(loop.CurrentStageIndex, 0, KenyaNetworkData.Hubs.Length - 1) : 0;
                SelectHub(activeIdx);
            }
            KenyaStreetAudio.Instance?.PlayUiClick();
        }

        private void SelectHub(int hubIndex)
        {
            selectedHubIndex = Mathf.Clamp(hubIndex, 0, KenyaNetworkData.Hubs.Length - 1);
            var hub = KenyaNetworkData.Hubs[selectedHubIndex];

            if (fullSelectedRing != null)
            {
                fullSelectedRing.anchoredPosition = WorldToMapPos(hub.xz, fullPlotSize);
            }

            for (int i = 0; i < hubPinImages.Count; i++)
            {
                if (hubPinImages[i] == null) continue;
                bool isSel = (i == selectedHubIndex);
                hubPinImages[i].color = isSel ? Palette.UiAccentAmber : Palette.EnaBodyWhite;
                hubPinImages[i].rectTransform.localScale = Vector3.one * (isSel ? 1.35f : 1f);
            }

            var loop = StreetGameLoop.Instance;
            float distKm = 0f;
            if (loop != null && loop.ActiveVehicle != null)
            {
                var vp = loop.ActiveVehicle.transform.position;
                distKm = Vector2.Distance(new Vector2(vp.x, vp.z), hub.xz) / 100f; // ETS2 visual scale km
            }

            if (detailTitleText != null)
            {
                detailTitleText.text = hub.name.ToUpperInvariant();
            }
            if (detailInfoText != null)
            {
                detailInfoText.text =
                    $"CORRIDOR:  {hub.corridorTag}\n" +
                    $"REGION:  {FormatBiome(hub.biome)}\n" +
                    $"DISTANCE:  {distKm:0.1} km (ETS2 Scale)\n" +
                    $"STAGE FARE:  KES {hub.fareKes:N0}  ({hub.passengers} pax)\n\n" +
                    "Set as your active GPS Bus Stage to drive there, or jump directly to this Kenyan terminal to explore its streets & highways!";
            }
        }

        private void UpdatePlayerAndTargetMarkers(StreetGameLoop loop)
        {
            var veh = loop.ActiveVehicle;
            if (veh == null) return;

            Vector2 vehXZ = new Vector2(veh.transform.position.x, veh.transform.position.z);
            float yaw = veh.transform.eulerAngles.y;

            if (miniPlayerArrow != null)
            {
                miniPlayerArrow.anchoredPosition = WorldToMapPos(vehXZ, miniMapSize);
                miniPlayerArrow.localRotation = Quaternion.Euler(0f, 0f, -yaw);
            }
            if (fullPlayerArrow != null)
            {
                fullPlayerArrow.anchoredPosition = WorldToMapPos(vehXZ, fullPlotSize);
                fullPlayerArrow.localRotation = Quaternion.Euler(0f, 0f, -yaw);
            }

            var activeStage = loop.ActiveStage;
            if (activeStage != null && miniTargetRing != null)
            {
                Vector2 stXZ = new Vector2(activeStage.transform.position.x, activeStage.transform.position.z);
                miniTargetRing.anchoredPosition = WorldToMapPos(stXZ, miniMapSize);
                miniTargetRing.gameObject.SetActive(true);
            }
            else if (miniTargetRing != null)
            {
                miniTargetRing.gameObject.SetActive(false);
            }

            if (miniLocationText != null)
            {
                string nearestTown = FindNearestHubName(vehXZ);
                miniLocationText.text = $"{nearestTown.ToUpperInvariant()}  •  [M] MAP";
            }
        }

        private static string FindNearestHubName(Vector2 xz)
        {
            float best = float.MaxValue;
            string name = "Nairobi CBD";
            for (int i = 0; i < KenyaNetworkData.Hubs.Length; i++)
            {
                float d = Vector2.Distance(xz, KenyaNetworkData.Hubs[i].xz);
                if (d < best)
                {
                    best = d;
                    name = KenyaNetworkData.Hubs[i].name;
                }
            }
            return name;
        }

        private static Vector2 WorldToMapPos(Vector2 worldXZ, Vector2 panelSize)
        {
            float pad = 26f;
            float nx = Mathf.InverseLerp(KenyaNetworkData.WorldMinXZ.x, KenyaNetworkData.WorldMaxXZ.x, worldXZ.x);
            float nz = Mathf.InverseLerp(KenyaNetworkData.WorldMinXZ.y, KenyaNetworkData.WorldMaxXZ.y, worldXZ.y);
            float x = Mathf.Lerp(-panelSize.x * 0.5f + pad, panelSize.x * 0.5f - pad, nx);
            float y = Mathf.Lerp(-panelSize.y * 0.5f + pad, panelSize.y * 0.5f - pad, nz);
            return new Vector2(x, y);
        }

        private void BuildMapCanvas()
        {
            var go = new GameObject("KenyaRouteMapCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(transform, false);

            mapCanvas = go.GetComponent<Canvas>();
            mapCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            mapCanvas.sortingOrder = 115;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var root = go.GetComponent<RectTransform>();
            BuildCornerMiniMap(root);
            BuildFullInteractiveMap(root);
        }

        private void BuildCornerMiniMap(RectTransform root)
        {
            // Bottom-left ETS2 Route Advisor GPS Minimap cleanly above the bottom controls pill
            var outer = CreateBorderedCorner("KenyaMiniMap", root, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(28f, 82f), new Vector2(280f, 192f), Palette.Hex("#141B25"), Palette.UiAccentAmber, 2f);
            miniMapRoot = outer.gameObject;

            miniLocationText = CreateText("MiniHeader", outer, "NAIROBI CBD  •  [M] MAP", 13.5f, FontStyles.Bold, Palette.UiAccentAmber, TextAlignmentOptions.Center);
            miniLocationText.textWrappingMode = TextWrappingModes.NoWrap;
            miniLocationText.overflowMode = TextOverflowModes.Truncate;
            SetBox(miniLocationText.rectTransform, 0f, -6f, 264f, 22f, new Vector2(0.5f, 1f));

            var plotGo = new GameObject("MiniPlot", typeof(RectTransform), typeof(Image));
            plotGo.transform.SetParent(outer, false);
            var plotRt = plotGo.GetComponent<RectTransform>();
            plotRt.anchorMin = new Vector2(0.5f, 0f);
            plotRt.anchorMax = new Vector2(0.5f, 0f);
            plotRt.pivot = new Vector2(0.5f, 0f);
            plotRt.anchoredPosition = new Vector2(0f, 8f);
            plotRt.sizeDelta = miniMapSize;
            var plotImg = plotGo.GetComponent<Image>();
            plotImg.sprite = solidSprite;
            plotImg.color = Palette.Hex("#0E151E");

            // Draw stylized water landmarks for Lake Victoria (far west) & Indian Ocean (south-east)
            DrawMapWaterBlob(plotRt, new Vector2(-1260f, 540f), 58f, Palette.Hex("#173B5E"), miniMapSize);
            DrawMapWaterBlob(plotRt, new Vector2(-1240f, 440f), 42f, Palette.Hex("#173B5E"), miniMapSize);
            DrawMapWaterBlob(plotRt, new Vector2(845f, -260f), 64f, Palette.Hex("#144963"), miniMapSize);
            DrawMapWaterBlob(plotRt, new Vector2(770f, -310f), 46f, Palette.Hex("#144963"), miniMapSize);

            // Draw all 6 connected Kenyan highways
            DrawAllHighways(plotRt, miniMapSize, thickness: 2.8f);

            // Draw town hub dots
            for (int i = 0; i < KenyaNetworkData.Hubs.Length; i++)
            {
                var h = KenyaNetworkData.Hubs[i];
                var dot = CreateCircleMarker($"MiniHub_{i}", plotRt, WorldToMapPos(h.xz, miniMapSize), 7f, Palette.EnaBodyWhite);
                dot.raycastTarget = false;
            }

            // Active destination ring & live player marker
            var ringImg = CreateCircleMarker("MiniTargetRing", plotRt, Vector2.zero, 15f, Palette.WithAlpha(Palette.UiAccentGreen, 0.85f));
            miniTargetRing = ringImg.rectTransform;

            var playerImg = CreateCircleMarker("MiniPlayerDot", plotRt, Vector2.zero, 11f, Palette.UiAccentAmber);
            miniPlayerArrow = playerImg.rectTransform;
            var nose = new GameObject("ArrowTip", typeof(RectTransform), typeof(Image));
            nose.transform.SetParent(miniPlayerArrow, false);
            var nrt = nose.GetComponent<RectTransform>();
            nrt.anchoredPosition = new Vector2(0f, 6f);
            nrt.sizeDelta = new Vector2(5f, 7f);
            var nImg = nose.GetComponent<Image>();
            nImg.sprite = solidSprite;
            nImg.color = Palette.UiAccentRed;
        }

        private void BuildFullInteractiveMap(RectTransform root)
        {
            var scrim = new GameObject("FullKenyaMapOverlay", typeof(RectTransform), typeof(Image));
            scrim.transform.SetParent(root, false);
            StretchRect(scrim.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);
            var scrimImg = scrim.GetComponent<Image>();
            scrimImg.sprite = solidSprite;
            scrimImg.color = Palette.UiBackdropScrim;
            fullMapOverlay = scrim;

            var modal = CreateBorderedAnchored("KenyaMapModal", scrim.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(1540f, 780f), Palette.Hex("#141B25"), Palette.UiAccentAmber, 3f);

            var title = CreateText("MapTitle", modal, "KENYA ENA COACH — FULL CONNECTED ROUTE NETWORK", 34f, FontStyles.Bold, Palette.UiAccentAmber, TextAlignmentOptions.Left);
            SetBox(title.rectTransform, 32f, -20f, 1180f, 44f, new Vector2(0f, 1f));

            var sub = CreateText("MapSub", modal, "Click any Kenyan city/town terminal below to set GPS Stage or Fast-Travel directly to its highway  •  Press [M] or [ESC] to Close", 17f, FontStyles.Normal, Palette.UiTextMuted, TextAlignmentOptions.Left);
            SetBox(sub.rectTransform, 32f, -66f, 1280f, 26f, new Vector2(0f, 1f));

            CreateButton("CloseMapBtn", modal, "CLOSE  [M]", new Vector2(665f, 340f), new Vector2(165f, 44f), Palette.UiAccentRed, Palette.UiTextPrimary, () => SetFullMapOpen(false));

            // Left: Large interactive Kenya Map Plot
            var plotFrame = CreateBorderedCorner("MapPlotFrame", modal, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(28f, 28f), new Vector2(1070f, 624f), Palette.Hex("#0D141D"), Palette.UiPanelBorder, 2f);
            fullMapPlot = plotFrame;
            fullPlotSize = new Vector2(1060f, 610f);

            // Geographic stylized water silhouettes on the full map (Lake Victoria West, Indian Ocean East/SE)
            DrawMapWaterBlob(fullMapPlot, new Vector2(-1305f, 595f), 175f, Palette.Hex("#153A5C"), fullPlotSize);
            DrawMapWaterBlob(fullMapPlot, new Vector2(-1320f, 445f), 155f, Palette.Hex("#153A5C"), fullPlotSize);
            DrawMapWaterBlob(fullMapPlot, new Vector2(-1255f, 540f), 115f, Palette.Hex("#153A5C"), fullPlotSize);
            var lakeLbl = CreateText("LakeVictoriaLbl", fullMapPlot, "LAKE\nVICTORIA", 14.5f, FontStyles.Bold, Palette.Hex("#7EC3EE"), TextAlignmentOptions.Center);
            lakeLbl.rectTransform.anchoredPosition = WorldToMapPos(new Vector2(-1310f, 530f), fullPlotSize);
            lakeLbl.rectTransform.sizeDelta = new Vector2(110f, 44f);

            DrawMapWaterBlob(fullMapPlot, new Vector2(960f, -245f), 210f, Palette.Hex("#134D69"), fullPlotSize);
            DrawMapWaterBlob(fullMapPlot, new Vector2(1015f, -35f), 180f, Palette.Hex("#134D69"), fullPlotSize);
            DrawMapWaterBlob(fullMapPlot, new Vector2(825f, -325f), 155f, Palette.Hex("#134D69"), fullPlotSize);
            var oceanLbl = CreateText("IndianOceanLbl", fullMapPlot, "INDIAN\nOCEAN", 15f, FontStyles.Bold, Palette.Hex("#7BE0EC"), TextAlignmentOptions.Center);
            oceanLbl.rectTransform.anchoredPosition = WorldToMapPos(new Vector2(955f, -230f), fullPlotSize);
            oceanLbl.rectTransform.sizeDelta = new Vector2(115f, 46f);

            // Draw all 8 connected Kenyan highways + 3 roundabouts
            DrawAllHighways(fullMapPlot, fullPlotSize, thickness: 7.0f);

            foreach (var rb in KenyaNetworkData.Roundabouts)
            {
                CreateCircleMarker($"FullRbt_{rb.name}", fullMapPlot, WorldToMapPos(rb.center, fullPlotSize), 19f, Palette.UiAccentAmber);
            }

            // Selected ring
            var selRing = CreateCircleMarker("FullSelectedRing", fullMapPlot, Vector2.zero, 32f, Palette.WithAlpha(Palette.UiAccentAmber, 0.45f));
            fullSelectedRing = selRing.rectTransform;

            // Explicit non-overlapping label offsets for all 16 Kenyan ENA Coach hubs
            Vector2[] labelOffsets =
            {
                new Vector2(  0f, -26f), //  0: Nairobi CBD (cleanly below pin)
                new Vector2(  0f, -25f), //  1: Narok Town (below B3 dip)
                new Vector2( 22f,  25f), //  2: Bomet Highlands (above B3)
                new Vector2( 58f, -24f), //  3: Kisii Town (below-right of Kisii pin)
                new Vector2( 84f,   0f), //  4: Migori / Sirare (right of vertical A1)
                new Vector2( 74f,   0f), //  5: Naivasha Town (right of A104)
                new Vector2( 74f,  -8f), //  6: Nakuru City (right of Nakuru pin)
                new Vector2(  0f, -25f), //  7: Kericho Town (below B1)
                new Vector2( 16f, -25f), //  8: Kisumu Lakeside (below B1/lake corner)
                new Vector2(  0f,  25f), //  9: Eldoret City (above North Rift peak)
                new Vector2(-46f,  25f), // 10: Kakamega Town (above-left of C39)
                new Vector2( 74f,   0f), // 11: Homa Bay Town (right of C19, clear of Lake Victoria)
                new Vector2( 72f,  16f), // 12: Mtito Andei (above-right of A109)
                new Vector2( 68f,  18f), // 13: Voi / Tsavo (above-right of A109)
                new Vector2(-52f, -26f), // 14: Mombasa Coast (below-left, clear of Indian Ocean)
                new Vector2(-68f,  14f)  // 15: Malindi Coast (left of North Coast highway)
            };

            // Interactive Clickable Town Hub Pins for all 16 ENA Coach Hubs!
            hubPinImages.Clear();
            for (int i = 0; i < KenyaNetworkData.Hubs.Length; i++)
            {
                int captured = i;
                var h = KenyaNetworkData.Hubs[i];
                Vector2 pos = WorldToMapPos(h.xz, fullPlotSize);

                var btnGo = new GameObject($"HubPin_{h.name}", typeof(RectTransform), typeof(Image), typeof(Button));
                btnGo.transform.SetParent(fullMapPlot, false);
                var rt = btnGo.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = pos;
                rt.sizeDelta = new Vector2(20f, 20f);

                var img = btnGo.GetComponent<Image>();
                img.sprite = circleSprite;
                img.color = Palette.EnaBodyWhite;
                hubPinImages.Add(img);

                var btn = btnGo.GetComponent<Button>();
                btn.targetGraphic = img;
                btn.onClick.AddListener(() =>
                {
                    SelectHub(captured);
                    KenyaStreetAudio.Instance?.PlayUiClick();
                });

                // Subtle dark backing pill behind each town label so road lines never clutter text
                Vector2 lOff = i < labelOffsets.Length ? labelOffsets[i] : new Vector2(0f, 24f);
                float pillW = Mathf.Clamp(h.name.Length * 8.6f + 16f, 88f, 152f);
                var pillGo = new GameObject($"HubLblPill_{i}", typeof(RectTransform), typeof(Image));
                pillGo.transform.SetParent(fullMapPlot, false);
                var prt = pillGo.GetComponent<RectTransform>();
                prt.anchorMin = new Vector2(0.5f, 0.5f);
                prt.anchorMax = new Vector2(0.5f, 0.5f);
                prt.anchoredPosition = pos + lOff;
                prt.sizeDelta = new Vector2(pillW, 22f);
                var pBg = pillGo.GetComponent<Image>();
                pBg.sprite = solidSprite;
                pBg.color = Palette.Hex("#0D141DE8");
                pBg.raycastTarget = false;

                var lbl = CreateText($"HubLbl_{i}", prt, h.name, 14f, FontStyles.Bold, Palette.UiTextPrimary, TextAlignmentOptions.Center);
                lbl.textWrappingMode = TextWrappingModes.NoWrap;
                StretchRect(lbl.rectTransform, 4f, 0f, -4f, 0f);
            }

            // Live Player Bus Marker on full map
            var pImg = CreateCircleMarker("FullPlayerMarker", fullMapPlot, Vector2.zero, 18f, Palette.UiAccentRed);
            fullPlayerArrow = pImg.rectTransform;
            var pNose = new GameObject("PlayerNose", typeof(RectTransform), typeof(Image));
            pNose.transform.SetParent(fullPlayerArrow, false);
            var pnRt = pNose.GetComponent<RectTransform>();
            pnRt.anchoredPosition = new Vector2(0f, 10f);
            pnRt.sizeDelta = new Vector2(7f, 11f);
            var pnImg = pNose.GetComponent<Image>();
            pnImg.sprite = solidSprite;
            pnImg.color = Palette.UiAccentAmber;

            // Right Side: Selected Terminal Info & Quick Actions Panel
            var side = CreateBorderedCorner("HubDetailPanel", modal, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-28f, 28f), new Vector2(396f, 624f), Palette.Hex("#1A222E"), Palette.UiPanelBorder, 2f);

            detailTitleText = CreateText("DetailTitle", side, "NAIROBI CBD", 30f, FontStyles.Bold, Palette.UiAccentAmber, TextAlignmentOptions.Left);
            SetBox(detailTitleText.rectTransform, 22f, -20f, 352f, 40f, new Vector2(0f, 1f));

            detailInfoText = CreateText("DetailInfo", side, "", 17f, FontStyles.Normal, Palette.UiTextPrimary, TextAlignmentOptions.TopLeft);
            SetBox(detailInfoText.rectTransform, 22f, -68f, 352f, 220f, new Vector2(0f, 1f));

            CreateButton("SetGpsBtn", side, "SET AS GPS STAGE", new Vector2(0f, -20f), new Vector2(344f, 50f), Palette.UiAccentGreen, Palette.UiFadeCurtain, () =>
            {
                SetFullMapOpen(false);
                StreetGameLoop.Instance?.SetActiveStageByIndex(selectedHubIndex);
            });

            CreateButton("TeleportBtn", side, "DRIVE FROM THIS TOWN", new Vector2(0f, -84f), new Vector2(344f, 50f), Palette.UiAccentAmber, Palette.UiFadeCurtain, () =>
            {
                SetFullMapOpen(false);
                StreetGameLoop.Instance?.TeleportToTownHub(selectedHubIndex);
            });

            // Corridor color legend at bottom of right panel (all 6 corridors across 16 towns!)
            var legend = CreateText("CorridorLegend", side,
                "16-TOWN KENYA HIGHWAY CORRIDORS\n" +
                "• B3 (Red): Nairobi – Narok – Bomet – Kisii\n" +
                "• A1 (Orange): Kisii – Migori – Sirare Border\n" +
                "• A104/B1 (Green): Nairobi – Naivasha – Nakuru – Kericho – Kisumu\n" +
                "• C39 (Violet): Nakuru – Eldoret – Kakamega – Kisumu\n" +
                "• C19 (Blue): Kisumu – Homa Bay – Kisii Loop\n" +
                "• A109/B8 (Teal): Nairobi – Mtito Andei – Voi – Mombasa – Malindi",
                13.5f, FontStyles.Normal, Palette.UiTextPrimary, TextAlignmentOptions.BottomLeft);
            legend.lineSpacing = 6f;
            SetBox(legend.rectTransform, 22f, 16f, 354f, 174f, new Vector2(0f, 0f));

            fullMapOverlay.SetActive(false);
        }

        private void DrawAllHighways(RectTransform parent, Vector2 size, float thickness)
        {
            for (int h = 0; h < KenyaNetworkData.Highways.Length; h++)
            {
                var def = KenyaNetworkData.Highways[h];
                var pts = KenyaNetworkData.SampleHighway(def, 28f);
                for (int i = 0; i < pts.Count - 1; i++)
                {
                    Vector2 a = WorldToMapPos(new Vector2(pts[i].x, pts[i].z), size);
                    Vector2 b = WorldToMapPos(new Vector2(pts[i + 1].x, pts[i + 1].z), size);
                    DrawUiSegment(parent, a, b, thickness, def.mapColor);
                }
            }
        }

        private void DrawUiSegment(RectTransform parent, Vector2 a, Vector2 b, float thickness, Color color)
        {
            Vector2 delta = b - a;
            float len = delta.magnitude;
            if (len < 0.5f) return;

            var go = new GameObject("RoadSeg", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = (a + b) * 0.5f;
            rt.sizeDelta = new Vector2(len + thickness * 0.45f, thickness);
            rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);

            var img = go.GetComponent<Image>();
            img.sprite = solidSprite;
            img.color = color;
            img.raycastTarget = false;
        }

        private void DrawMapWaterBlob(RectTransform parent, Vector2 worldXZ, float diameter, Color color, Vector2 mapSize)
        {
            var go = new GameObject("WaterBlob", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = WorldToMapPos(worldXZ, mapSize);
            rt.sizeDelta = new Vector2(diameter, diameter);
            var img = go.GetComponent<Image>();
            img.sprite = circleSprite;
            img.color = color;
            img.raycastTarget = false;
        }

        private Image CreateCircleMarker(string name, RectTransform parent, Vector2 pos, float diameter, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(diameter, diameter);
            var img = go.GetComponent<Image>();
            img.sprite = circleSprite;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        private RectTransform CreateBorderedAnchored(string name, RectTransform parent, Vector2 anchor, Vector2 size, Color fill, Color border, float borderPx)
        {
            var outer = new GameObject(name, typeof(RectTransform), typeof(Image));
            outer.transform.SetParent(parent, false);
            var ort = outer.GetComponent<RectTransform>();
            ort.anchorMin = anchor;
            ort.anchorMax = anchor;
            ort.pivot = new Vector2(0.5f, 0.5f);
            ort.sizeDelta = size;
            ort.anchoredPosition = Vector2.zero;
            var oImg = outer.GetComponent<Image>();
            oImg.sprite = solidSprite;
            oImg.color = border;

            var inner = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            inner.transform.SetParent(ort, false);
            StretchRect(inner.GetComponent<RectTransform>(), borderPx, borderPx, -borderPx, -borderPx);
            var iImg = inner.GetComponent<Image>();
            iImg.sprite = solidSprite;
            iImg.color = fill;
            return ort;
        }

        private RectTransform CreateBorderedCorner(string name, RectTransform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, Color fill, Color border, float borderPx)
        {
            var outer = new GameObject(name, typeof(RectTransform), typeof(Image));
            outer.transform.SetParent(parent, false);
            var ort = outer.GetComponent<RectTransform>();
            ort.anchorMin = anchor;
            ort.anchorMax = anchor;
            ort.pivot = pivot;
            ort.anchoredPosition = pos;
            ort.sizeDelta = size;
            var oImg = outer.GetComponent<Image>();
            oImg.sprite = solidSprite;
            oImg.color = border;

            var inner = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            inner.transform.SetParent(ort, false);
            StretchRect(inner.GetComponent<RectTransform>(), borderPx, borderPx, -borderPx, -borderPx);
            var iImg = inner.GetComponent<Image>();
            iImg.sprite = solidSprite;
            iImg.color = fill;
            return ort;
        }

        private Button CreateButton(string name, RectTransform parent, string label, Vector2 anchoredPos, Vector2 size, Color bgColor, Color textColor, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
            var img = go.GetComponent<Image>();
            img.sprite = solidSprite;
            img.color = bgColor;
            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(onClick);
            var tmp = CreateText("Label", rt, label, 20f, FontStyles.Bold, textColor, TextAlignmentOptions.Center);
            StretchRect(tmp.rectTransform, 8f, 4f, -8f, -4f);
            return btn;
        }

        private TextMeshProUGUI CreateText(string name, RectTransform parent, string text, float fontSize, FontStyles style, Color color, TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.fontStyle = style;
            tmp.color = color;
            tmp.alignment = align;
            tmp.raycastTarget = false;
            return tmp;
        }

        private static void StretchRect(RectTransform rt, float left, float bottom, float right, float top)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(right, top);
        }

        private static void SetBox(RectTransform rt, float x, float y, float width, float height, Vector2 anchorAndPivot)
        {
            rt.anchorMin = anchorAndPivot;
            rt.anchorMax = anchorAndPivot;
            rt.pivot = anchorAndPivot;
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(width, height);
        }

        private static string FormatBiome(KenyaNetworkData.KenyaBiome biome) => biome switch
        {
            KenyaNetworkData.KenyaBiome.UrbanNairobi => "Nairobi Capital Metropolis",
            KenyaNetworkData.KenyaBiome.RiftSavanna => "Rift Valley & Savanna Plains",
            KenyaNetworkData.KenyaBiome.TeaHighlands => "Lush Green Tea Highlands",
            KenyaNetworkData.KenyaBiome.LakeVictoria => "Lake Victoria Waterfront",
            KenyaNetworkData.KenyaBiome.TsavoRedEarth => "Tsavo Red Earth Corridor",
            KenyaNetworkData.KenyaBiome.CoastalMombasa => "Mombasa Indian Ocean Coast",
            _ => "Kenya Highway"
        };

        private static Sprite CreateSolidSprite()
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f));
        }

        private static Sprite CreateCircleSprite(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float half = (size - 1) * 0.5f;
            float r2 = half * half;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - half;
                    float dy = y - half;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01((half - dist) * 1.4f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        private static bool WasMapKeyPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.mKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.M);
#endif
        }

        private static bool WasEscPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.Escape);
#endif
        }
    }
}
