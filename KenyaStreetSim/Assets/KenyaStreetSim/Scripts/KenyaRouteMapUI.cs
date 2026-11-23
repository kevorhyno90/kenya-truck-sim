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
        private readonly List<GameObject> hubPointBadges = new List<GameObject>();
        private readonly List<TextMeshProUGUI> hubPointBadgeTexts = new List<TextMeshProUGUI>();
        private readonly List<Image> hubPointBadgeBgs = new List<Image>();
        private readonly List<Button> routeSelectButtons = new List<Button>();
        private List<Image>[] fullHighwaySegImages;
        private List<Image>[] miniHighwaySegImages;
        private int lastHighlightedRoute = -1;

        private void Awake()
        {
            int hwCount = KenyaNetworkData.Highways.Length;
            fullHighwaySegImages = new List<Image>[hwCount];
            miniHighwaySegImages = new List<Image>[hwCount];
            for (int i = 0; i < hwCount; i++)
            {
                fullHighwaySegImages[i] = new List<Image>();
                miniHighwaySegImages[i] = new List<Image>();
            }
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

            if (lastHighlightedRoute != loop.ActiveRouteIndex)
            {
                RefreshRouteOverlay(loop.ActiveRouteIndex);
            }
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
                RefreshRouteOverlay(loop != null ? loop.ActiveRouteIndex : 0);
                SelectHub(activeIdx);
            }
            KenyaStreetAudio.Instance?.PlayUiClick();
        }

        public void RefreshRouteOverlay(int routeIndex)
        {
            lastHighlightedRoute = routeIndex;
            var route = KenyaRouteCatalog.GetRoute(routeIndex);
            bool isParcel = route.kind == KenyaRouteCatalog.RouteKind.FrrParcelCargo;
            Color activeRoadCol = isParcel ? Palette.Hex("#FF9E1B") : Palette.Hex("#14E8FF");

            // Highlight selected route highways in bright Cyan/Orange & softly dim off-route highways on BOTH maps!
            for (int h = 0; h < KenyaNetworkData.Highways.Length && h < fullHighwaySegImages.Length; h++)
            {
                bool onRoute = KenyaRouteCatalog.IsHighwayOnRoute(routeIndex, h);
                Color baseCol = KenyaNetworkData.Highways[h].mapColor;
                Color c = onRoute ? activeRoadCol : Palette.WithAlpha(baseCol, 0.36f);

                if (fullHighwaySegImages[h] != null)
                {
                    for (int i = 0; i < fullHighwaySegImages[h].Count; i++)
                    {
                        var img = fullHighwaySegImages[h][i];
                        if (img == null) continue;
                        img.color = c;
                        var sd = img.rectTransform.sizeDelta;
                        sd.y = onRoute ? 10.5f : 5.5f;
                        img.rectTransform.sizeDelta = sd;
                    }
                }
                if (miniHighwaySegImages[h] != null)
                {
                    for (int i = 0; i < miniHighwaySegImages[h].Count; i++)
                    {
                        var img = miniHighwaySegImages[h][i];
                        if (img == null) continue;
                        img.color = c;
                        var sd = img.rectTransform.sizeDelta;
                        sd.y = onRoute ? 4.4f : 2.2f;
                        img.rectTransform.sizeDelta = sd;
                    }
                }
            }

            // Update PAX (Passenger Pickup) / PARCEL (FRR Depot) point badges on all 16 town hubs!
            string tag = isParcel ? "PARCEL" : "PAX";
            Color badgeCol = isParcel ? Palette.Hex("#E86A0C") : Palette.Hex("#16A85A");
            for (int i = 0; i < hubPointBadges.Count; i++)
            {
                bool onRoute = KenyaRouteCatalog.IsHubOnRoute(routeIndex, i);
                if (hubPointBadges[i] != null) hubPointBadges[i].SetActive(onRoute);
                if (i < hubPointBadgeTexts.Count && hubPointBadgeTexts[i] != null) hubPointBadgeTexts[i].text = tag;
                if (i < hubPointBadgeBgs.Count && hubPointBadgeBgs[i] != null) hubPointBadgeBgs[i].color = badgeCol;
            }

            // Highlight active route button in the right-hand route selector list
            for (int r = 0; r < routeSelectButtons.Count; r++)
            {
                var btn = routeSelectButtons[r];
                if (btn == null) continue;
                bool sel = (r == routeIndex);
                var rKind = KenyaRouteCatalog.GetRoute(r).kind;
                var img = btn.GetComponent<Image>();
                if (img != null)
                {
                    img.color = sel
                        ? (rKind == KenyaRouteCatalog.RouteKind.FrrParcelCargo ? Palette.Hex("#E86A0C") : Palette.Hex("#0FA8C8"))
                        : Palette.Hex("#212C3C");
                }
                var txt = btn.GetComponentInChildren<TextMeshProUGUI>();
                if (txt != null)
                {
                    txt.color = sel ? Palette.UiTextPrimary : Palette.Hex("#C8D4E4");
                }
            }

            if (hubPinImages.Count > 0)
            {
                SelectHub(selectedHubIndex);
            }
        }

        private void SelectHub(int hubIndex)
        {
            selectedHubIndex = Mathf.Clamp(hubIndex, 0, KenyaNetworkData.Hubs.Length - 1);
            var hub = KenyaNetworkData.Hubs[selectedHubIndex];

            if (fullSelectedRing != null)
            {
                fullSelectedRing.anchoredPosition = WorldToMapPos(hub.xz, fullPlotSize);
            }

            var loop = StreetGameLoop.Instance;
            int activeRouteIdx = loop != null ? loop.ActiveRouteIndex : 0;
            var activeRoute = KenyaRouteCatalog.GetRoute(activeRouteIdx);
            bool isParcelRoute = activeRoute.kind == KenyaRouteCatalog.RouteKind.FrrParcelCargo;

            for (int i = 0; i < hubPinImages.Count; i++)
            {
                if (hubPinImages[i] == null) continue;
                bool isSel = (i == selectedHubIndex);
                bool onRoute = KenyaRouteCatalog.IsHubOnRoute(activeRouteIdx, i);
                hubPinImages[i].color = isSel
                    ? Palette.UiAccentAmber
                    : (onRoute ? (isParcelRoute ? Palette.Hex("#FF9E1B") : Palette.Hex("#2EE87A")) : Palette.WithAlpha(Palette.EnaBodyWhite, 0.55f));
                hubPinImages[i].rectTransform.localScale = Vector3.one * (isSel ? 1.40f : (onRoute ? 1.15f : 0.90f));
            }

            float distKm = 0f;
            if (loop != null && loop.ActiveVehicle != null)
            {
                var vp = loop.ActiveVehicle.transform.position;
                distKm = Vector2.Distance(new Vector2(vp.x, vp.z), hub.xz) / 100f;
            }

            string pointLabel = isParcelRoute ? "FRR PARCEL DEPOT" : "PASSENGER PICKUP STAGE";
            if (detailTitleText != null)
            {
                detailTitleText.text = hub.name.ToUpperInvariant();
            }
            if (detailInfoText != null)
            {
                detailInfoText.text =
                    $"POINT:  {pointLabel}\n" +
                    $"ROUTE:  {activeRoute.shortName}\n" +
                    $"CORRIDOR:  {hub.corridorTag}  •  {distKm:0.1} km\n" +
                    $"PAYOUT:  KES {hub.fareKes:N0}  ({hub.passengers} {(isParcelRoute ? "parcels" : "pax")})";
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

            // Draw stylized water landmarks for Lake Victoria (far west: Sori -> Homa Bay -> Kisumu) & Indian Ocean (Mombasa -> Kilifi -> Malindi)
            DrawMapWaterBlob(plotRt, new Vector2(-2020f, 440f), 60f, Palette.Hex("#173B5E"), miniMapSize);
            DrawMapWaterBlob(plotRt, new Vector2(-2040f, 80f), 46f, Palette.Hex("#173B5E"), miniMapSize);
            DrawMapWaterBlob(plotRt, new Vector2(1240f, -340f), 62f, Palette.Hex("#144963"), miniMapSize);
            DrawMapWaterBlob(plotRt, new Vector2(1355f, 10f), 50f, Palette.Hex("#144963"), miniMapSize);

            // Draw all 15 connected Kenyan highways (tracked for live route highlighting!)
            DrawAllHighways(plotRt, miniMapSize, thickness: 2.5f, miniHighwaySegImages);

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

            var title = CreateText("MapTitle", modal, "KENYA BUS & FRR PARCEL ROUTES  —  INTERACTIVE GUIDED GPS MAP", 31f, FontStyles.Bold, Palette.UiAccentAmber, TextAlignmentOptions.Left);
            SetBox(title.rectTransform, 32f, -20f, 1220f, 44f, new Vector2(0f, 1f));

            var sub = CreateText("MapSub", modal, "Select a Kenyan Bus or FRR Parcel Route on the right to highlight its guided path & PAX / PARCEL pickup points  •  Press [M] or [ESC] to Close", 16.5f, FontStyles.Normal, Palette.UiTextMuted, TextAlignmentOptions.Left);
            SetBox(sub.rectTransform, 32f, -66f, 1320f, 26f, new Vector2(0f, 1f));

            CreateButton("CloseMapBtn", modal, "CLOSE  [M]", new Vector2(665f, 340f), new Vector2(165f, 44f), Palette.UiAccentRed, Palette.UiTextPrimary, () => SetFullMapOpen(false));

            // Left: Large interactive Kenya Map Plot
            var plotFrame = CreateBorderedCorner("MapPlotFrame", modal, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(28f, 28f), new Vector2(1070f, 624f), Palette.Hex("#0D141D"), Palette.UiPanelBorder, 2f);
            fullMapPlot = plotFrame;
            fullPlotSize = new Vector2(1060f, 610f);

            // Geographic stylized water silhouettes on the full map (Lake Victoria West: Sori -> Homa Bay -> Kisumu; Indian Ocean East: Mombasa -> Kilifi -> Malindi)
            DrawMapWaterBlob(fullMapPlot, new Vector2(-2030f, 540f), 175f, Palette.Hex("#153A5C"), fullPlotSize);
            DrawMapWaterBlob(fullMapPlot, new Vector2(-2050f, 240f), 165f, Palette.Hex("#153A5C"), fullPlotSize);
            DrawMapWaterBlob(fullMapPlot, new Vector2(-2060f, -60f), 145f, Palette.Hex("#153A5C"), fullPlotSize);
            var lakeLbl = CreateText("LakeVictoriaLbl", fullMapPlot, "LAKE\nVICTORIA", 14f, FontStyles.Bold, Palette.Hex("#7EC3EE"), TextAlignmentOptions.Center);
            lakeLbl.rectTransform.anchoredPosition = WorldToMapPos(new Vector2(-2015f, 440f), fullPlotSize);
            lakeLbl.rectTransform.sizeDelta = new Vector2(105f, 42f);

            DrawMapWaterBlob(fullMapPlot, new Vector2(1265f, -345f), 195f, Palette.Hex("#134D69"), fullPlotSize);
            DrawMapWaterBlob(fullMapPlot, new Vector2(1375f, -120f), 185f, Palette.Hex("#134D69"), fullPlotSize);
            DrawMapWaterBlob(fullMapPlot, new Vector2(1395f,  140f), 165f, Palette.Hex("#134D69"), fullPlotSize);
            var oceanLbl = CreateText("IndianOceanLbl", fullMapPlot, "INDIAN\nOCEAN", 14.5f, FontStyles.Bold, Palette.Hex("#7BE0EC"), TextAlignmentOptions.Center);
            oceanLbl.rectTransform.anchoredPosition = WorldToMapPos(new Vector2(1310f, -260f), fullPlotSize);
            oceanLbl.rectTransform.sizeDelta = new Vector2(110f, 44f);

            // Draw all 15 connected Kenyan highways + 10 roundabouts (tracked for bright Cyan/Orange route highlight!)
            DrawAllHighways(fullMapPlot, fullPlotSize, thickness: 5.5f, fullHighwaySegImages);

            foreach (var rb in KenyaNetworkData.Roundabouts)
            {
                CreateCircleMarker($"FullRbt_{rb.name}", fullMapPlot, WorldToMapPos(rb.center, fullPlotSize), 15f, Palette.UiAccentAmber);
            }

            // Selected ring
            var selRing = CreateCircleMarker("FullSelectedRing", fullMapPlot, Vector2.zero, 32f, Palette.WithAlpha(Palette.UiAccentAmber, 0.45f));
            fullSelectedRing = selRing.rectTransform;

            // Clean National Kenya Road Atlas Pins for ALL 41 Towns!
            // Major Cities & Regional Hubs show crisp named pills; compact Transit Towns show clickable pins + sleek mini tags!
            hubPinImages.Clear();
            for (int i = 0; i < KenyaNetworkData.Hubs.Length; i++)
            {
                int captured = i;
                var h = KenyaNetworkData.Hubs[i];
                Vector2 pos = WorldToMapPos(h.xz, fullPlotSize);
                bool isMajorOrRegional = h.tier != KenyaNetworkData.TownTier.TransitTown;

                var btnGo = new GameObject($"HubPin_{h.name}", typeof(RectTransform), typeof(Image), typeof(Button));
                btnGo.transform.SetParent(fullMapPlot, false);
                var rt = btnGo.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = pos;
                float pinSz = h.tier == KenyaNetworkData.TownTier.MajorCity ? 18f : (isMajorOrRegional ? 14f : 10f);
                rt.sizeDelta = new Vector2(pinSz, pinSz);

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

                // Smart non-overlapping label offset so dense Western/Nyanza corridors stay clean and legible!
                Vector2 lOff = (i % 2 == 0) ? new Vector2(0f, -18f) : new Vector2(0f, 18f);
                if (h.name.Contains("Nairobi")) lOff = new Vector2(0f, -24f);
                else if (h.name.Contains("Thika")) lOff = new Vector2(46f, 8f);
                else if (h.name.Contains("Migori")) lOff = new Vector2(58f, 0f);
                else if (h.name.Contains("Sori")) lOff = new Vector2(-42f, -14f);
                else if (h.name.Contains("Homa Bay")) lOff = new Vector2(-54f, 6f);
                else if (h.name.Contains("Kisumu")) lOff = new Vector2(-54f, 0f);
                else if (h.name.Contains("Malaba")) lOff = new Vector2(-52f, 14f);
                else if (h.name.Contains("Kitale")) lOff = new Vector2(46f, 10f);
                else if (h.name.Contains("Eldoret")) lOff = new Vector2(56f, 4f);
                else if (h.name.Contains("Nakuru")) lOff = new Vector2(54f, 6f);
                else if (h.name.Contains("Naivasha")) lOff = new Vector2(58f, 0f);
                else if (h.name.Contains("Mombasa")) lOff = new Vector2(-48f, -20f);
                else if (h.name.Contains("Malindi")) lOff = new Vector2(-52f, 14f);

                if (isMajorOrRegional)
                {
                    float pillW = Mathf.Clamp(h.name.Length * 7.2f + 12f, 64f, 128f);
                    var pillGo = new GameObject($"HubLblPill_{i}", typeof(RectTransform), typeof(Image));
                    pillGo.transform.SetParent(fullMapPlot, false);
                    var prt = pillGo.GetComponent<RectTransform>();
                    prt.anchorMin = new Vector2(0.5f, 0.5f);
                    prt.anchorMax = new Vector2(0.5f, 0.5f);
                    prt.anchoredPosition = pos + lOff;
                    prt.sizeDelta = new Vector2(pillW, 18f);
                    var pBg = pillGo.GetComponent<Image>();
                    pBg.sprite = solidSprite;
                    pBg.color = h.tier == KenyaNetworkData.TownTier.MajorCity ? Palette.Hex("#0B1626F2") : Palette.Hex("#0D141DD8");
                    pBg.raycastTarget = false;

                    var lbl = CreateText($"HubLbl_{i}", prt, h.name, h.tier == KenyaNetworkData.TownTier.MajorCity ? 12f : 10.5f, FontStyles.Bold, Palette.UiTextPrimary, TextAlignmentOptions.Center);
                    lbl.textWrappingMode = TextWrappingModes.NoWrap;
                    StretchRect(lbl.rectTransform, 3f, 0f, -3f, 0f);
                }

                // Compact PAX / PARCEL badge on active route stops (only shown on active route!)
                var badgeGo = new GameObject($"HubPointBadge_{i}", typeof(RectTransform), typeof(Image));
                badgeGo.transform.SetParent(fullMapPlot, false);
                var brt = badgeGo.GetComponent<RectTransform>();
                brt.anchorMin = new Vector2(0.5f, 0.5f);
                brt.anchorMax = new Vector2(0.5f, 0.5f);
                float badgeY = lOff.y > 0f ? -14f : 14f;
                brt.anchoredPosition = pos + new Vector2(0f, badgeY);
                brt.sizeDelta = new Vector2(44f, 13f);
                var bImg = badgeGo.GetComponent<Image>();
                bImg.sprite = solidSprite;
                bImg.color = Palette.Hex("#16A85A");
                bImg.raycastTarget = false;
                var bTxt = CreateText($"BadgeTxt_{i}", brt, "PAX", 8.5f, FontStyles.Bold, Palette.UiTextPrimary, TextAlignmentOptions.Center);
                bTxt.textWrappingMode = TextWrappingModes.NoWrap;
                StretchRect(bTxt.rectTransform, 2f, 0f, -2f, 0f);

                hubPointBadges.Add(badgeGo);
                hubPointBadgeTexts.Add(bTxt);
                hubPointBadgeBgs.Add(bImg);
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

            // Right Side: Selectable Kenyan Bus & FRR Parcel Routes + Active Terminal Actions Panel
            var side = CreateBorderedCorner("HubDetailPanel", modal, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-28f, 28f), new Vector2(396f, 624f), Palette.Hex("#1A222E"), Palette.UiPanelBorder, 2f);

            var routesHdr = CreateText("RoutesHdr", side, "SELECT KENYA BUS / FRR PARCEL ROUTE", 15f, FontStyles.Bold, Palette.Hex("#14E8FF"), TextAlignmentOptions.Left);
            SetBox(routesHdr.rectTransform, 18f, -12f, 360f, 22f, new Vector2(0f, 1f));

            routeSelectButtons.Clear();
            for (int r = 0; r < KenyaRouteCatalog.Routes.Length; r++)
            {
                int capturedRoute = r;
                var rd = KenyaRouteCatalog.Routes[r];
                string prefix = rd.kind == KenyaRouteCatalog.RouteKind.FrrParcelCargo ? "[PARCEL] " : "[BUS] ";
                string btnLabel = prefix + rd.shortName;
                float yCenter = 262f - r * 31f; // neatly fills top half of 624px panel (center = 0, top = +312)
                var rBtn = CreateButton($"RouteBtn_{r}", side, btnLabel, new Vector2(0f, yCenter), new Vector2(362f, 27f), Palette.Hex("#212C3C"), Palette.UiTextPrimary, () =>
                {
                    StreetGameLoop.Instance?.SelectRoute(capturedRoute, moveToStartIfRunning: false);
                    RefreshRouteOverlay(capturedRoute);
                    var firstStop = KenyaRouteCatalog.GetRoute(capturedRoute).stopHubIndices[0];
                    SelectHub(firstStop);
                });
                var rLbl = rBtn.GetComponentInChildren<TextMeshProUGUI>();
                if (rLbl != null)
                {
                    rLbl.fontSize = 12.5f;
                    rLbl.alignment = TextAlignmentOptions.Left;
                    rLbl.textWrappingMode = TextWrappingModes.NoWrap;
                    rLbl.overflowMode = TextOverflowModes.Truncate;
                }
                routeSelectButtons.Add(rBtn);
            }

            // Crisp divider between Route Picker and Selected Town Actions
            var divGo = new GameObject("SideDivider", typeof(RectTransform), typeof(Image));
            divGo.transform.SetParent(side, false);
            var drt = divGo.GetComponent<RectTransform>();
            drt.anchorMin = new Vector2(0.5f, 0.5f);
            drt.anchorMax = new Vector2(0.5f, 0.5f);
            drt.anchoredPosition = new Vector2(0f, 18f);
            drt.sizeDelta = new Vector2(360f, 2f);
            var dImg = divGo.GetComponent<Image>();
            dImg.sprite = solidSprite;
            dImg.color = Palette.UiPanelBorder;

            detailTitleText = CreateText("DetailTitle", side, "NAIROBI CBD", 24f, FontStyles.Bold, Palette.UiAccentAmber, TextAlignmentOptions.Left);
            SetBox(detailTitleText.rectTransform, 18f, -304f, 360f, 32f, new Vector2(0f, 1f));

            detailInfoText = CreateText("DetailInfo", side, "", 15f, FontStyles.Normal, Palette.UiTextPrimary, TextAlignmentOptions.TopLeft);
            detailInfoText.lineSpacing = 6f;
            SetBox(detailInfoText.rectTransform, 18f, -340f, 360f, 128f, new Vector2(0f, 1f));

            CreateButton("SetGpsBtn", side, "GUIDE ME TO THIS STOP", new Vector2(0f, -196f), new Vector2(356f, 44f), Palette.UiAccentGreen, Palette.UiFadeCurtain, () =>
            {
                SetFullMapOpen(false);
                StreetGameLoop.Instance?.SetActiveStageByIndex(selectedHubIndex);
            });

            CreateButton("TeleportBtn", side, "DRIVE FROM THIS TOWN", new Vector2(0f, -252f), new Vector2(356f, 44f), Palette.UiAccentAmber, Palette.UiFadeCurtain, () =>
            {
                SetFullMapOpen(false);
                StreetGameLoop.Instance?.TeleportToTownHub(selectedHubIndex);
            });

            fullMapOverlay.SetActive(false);
            RefreshRouteOverlay(0);
        }

        private void DrawAllHighways(RectTransform parent, Vector2 size, float thickness, List<Image>[] bucket)
        {
            for (int h = 0; h < KenyaNetworkData.Highways.Length; h++)
            {
                var def = KenyaNetworkData.Highways[h];
                var pts = KenyaNetworkData.SampleHighway(def, 28f);
                for (int i = 0; i < pts.Count - 1; i++)
                {
                    Vector2 a = WorldToMapPos(new Vector2(pts[i].x, pts[i].z), size);
                    Vector2 b = WorldToMapPos(new Vector2(pts[i + 1].x, pts[i + 1].z), size);
                    var segImg = DrawUiSegment(parent, a, b, thickness, def.mapColor);
                    if (segImg != null && bucket != null && h < bucket.Length && bucket[h] != null)
                    {
                        bucket[h].Add(segImg);
                    }
                }
            }
        }

        private Image DrawUiSegment(RectTransform parent, Vector2 a, Vector2 b, float thickness, Color color)
        {
            Vector2 delta = b - a;
            float len = delta.magnitude;
            if (len < 0.5f) return null;

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
            return img;
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
