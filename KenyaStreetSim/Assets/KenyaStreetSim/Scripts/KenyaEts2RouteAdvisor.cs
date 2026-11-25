using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace KenyaStreetSim
{
    /// <summary>
    /// ETS2-Class Route Advisor & Driver Simulation Manager:
    ///  - Live Journey Ahead tracker (Next Stop km + ETA, Total Remaining Journey km + ETA, visual progress bar,
    ///    and Upcoming Towns itinerary so you ALWAYS know how much journey is in front of you!).
    ///  - Visible Onboard Seated Passenger count (e.g. 32 / 44 SEATS) synced with KenyaBusPassengerCabin.
    ///  - Cruise Control [X], Exhaust Retarder [N], Driver Rest/Fatigue % ([F] Sleep at Petrol Stations/Terminals),
    ///    Passenger Comfort %, Diesel Range (km), and NTSA 80 KPH Speed-Governor Monitor!
    /// </summary>
    public class KenyaEts2RouteAdvisor : MonoBehaviour
    {
        public static KenyaEts2RouteAdvisor Instance { get; private set; }

        public float DriverRest01 { get; private set; } = 1.0f;
        public float PassengerComfort01 { get; private set; } = 0.96f;
        public bool IsAdvisorVisible { get; private set; } = true;

        private Canvas advisorCanvas;
        private GameObject advisorCard;
        private TextMeshProUGUI lineRouteHeader;
        private TextMeshProUGUI lineJourneyAhead;
        private TextMeshProUGUI lineUpcomingStops;
        private TextMeshProUGUI linePaxCruise;
        private TextMeshProUGUI lineComfortRest;
        private TextMeshProUGUI lineFuelRange;
        private TextMeshProUGUI linePrompt;
        private Image journeyBarFill;
        private Image restBarFill;
        private Image comfortBarFill;

        private float overspeedTimer;
        private float lastWarningTime = -10f;
        private float lastRestHintTime = -20f;
        private float prevSpeedKmh;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            BuildAdvisorUI();
            if (StreetGameLoop.Instance != null)
            {
                StreetGameLoop.Instance.OnStageBoarded += HandleStageBoarded;
                StreetGameLoop.Instance.OnSafetyPenalty += HandlePenalty;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (StreetGameLoop.Instance != null)
            {
                StreetGameLoop.Instance.OnStageBoarded -= HandleStageBoarded;
                StreetGameLoop.Instance.OnSafetyPenalty -= HandlePenalty;
            }
        }

        private void Update()
        {
            var loop = StreetGameLoop.Instance;
            bool playing = loop != null && loop.State == StreetGameLoop.RunState.Running && loop.ActiveVehicle != null;
            if (advisorCard != null)
            {
                advisorCard.SetActive(playing && IsAdvisorVisible);
            }
            if (!playing) return;

            if (WasF1Pressed())
            {
                IsAdvisorVisible = !IsAdvisorVisible;
                KenyaStreetAudio.Instance?.PlayUiClick();
            }

            var veh = loop.ActiveVehicle;
            float dt = Time.deltaTime;

            // 1. Realistic Driver Rest drain while driving long Kenyan inter-county hauls
            if (veh.SpeedKmh > 6f)
            {
                DriverRest01 = Mathf.Clamp01(DriverRest01 - dt * 0.0014f);
            }

            // 2. NTSA 80 KPH PSV Speed-Governor Monitor & Harsh Braking Comfort Check
            float speedLimit = veh.isCoach ? 85f : 92f;
            if (veh.SpeedKmh > speedLimit)
            {
                overspeedTimer += dt;
                PassengerComfort01 = Mathf.Clamp01(PassengerComfort01 - dt * 0.025f);
                if (overspeedTimer > 2.8f && Time.time - lastWarningTime > 9f)
                {
                    lastWarningTime = Time.time;
                    KenyaStreetAudio.Instance?.PlayUiClick();
                    loop.hud?.ShowToast("NTSA 80 KPH GOVERNOR ALERT! PASSENGERS: 'DEREVA POLE POLE!'");
                }
            }
            else
            {
                overspeedTimer = 0f;
                if (veh.SpeedKmh > 12f)
                {
                    PassengerComfort01 = Mathf.Clamp01(PassengerComfort01 + dt * 0.008f);
                }
            }

            float decel = (prevSpeedKmh - veh.SpeedKmh) / Mathf.Max(0.01f, dt);
            if (decel > 52f && veh.SpeedKmh > 5f)
            {
                PassengerComfort01 = Mathf.Clamp01(PassengerComfort01 - 0.03f);
            }
            prevSpeedKmh = veh.SpeedKmh;

            // 3. Petrol Station / Terminal Driver Rest check
            bool canSleepHere = IsNearRestPoint(veh.transform.position) && veh.SpeedKmh < 8f;
            if (DriverRest01 < 0.28f && Time.time - lastRestHintTime > 25f)
            {
                lastRestHintTime = Time.time;
                loop.hud?.ShowToast("DRIVER FATIGUE LOW! PULL INTO ANY PETROL STATION OR TERMINAL & PRESS [F] TO REST");
            }
            if (canSleepHere && WasSleepKeyPressed())
            {
                PerformDriverRest(loop);
            }

            // 4. Refresh ETS2 Route Advisor UI (Journey Ahead + Passengers + Systems!)
            if (IsAdvisorVisible && advisorCard != null)
            {
                var gps = KenyaGpsRouteGuide.Instance;
                int pct = gps != null ? Mathf.RoundToInt(gps.JourneyProgress01 * 100f) : 0;
                int stopDone = loop.CompletedStageCount;
                int stopTotal = loop.RouteTotalStops;

                if (lineRouteHeader != null)
                {
                    string dest = gps != null ? gps.FinalDestinationName : "KENYA";
                    lineRouteHeader.text = $"TO {dest}  •  STOP {Mathf.Min(stopDone + 1, stopTotal)}/{stopTotal}  ({pct}%)";
                }
                if (journeyBarFill != null && gps != null)
                {
                    journeyBarFill.fillAmount = Mathf.Clamp01(gps.JourneyProgress01);
                }
                if (lineJourneyAhead != null && gps != null)
                {
                    string nextD = KenyaGpsRouteGuide.FormatDistance(gps.DistanceToNextStop);
                    string nextEta = KenyaGpsRouteGuide.FormatEta(gps.NextStopEtaSeconds);
                    string remD = KenyaGpsRouteGuide.FormatDistance(gps.TotalRemainingJourneyMeters);
                    string remEta = KenyaGpsRouteGuide.FormatEta(gps.TotalJourneyEtaSeconds);
                    lineJourneyAhead.text = $"NEXT {nextD} ({nextEta})  •  LEFT {remD} ({remEta})";
                }
                if (lineUpcomingStops != null && gps != null)
                {
                    lineUpcomingStops.text = $"AHEAD: {gps.UpcomingStopsSummary}";
                }

                string ccStr = veh.CruiseControlOn ? $"CC {veh.CruiseTargetKmh:0}" : "CC OFF [X]";
                string retStr = veh.RetarderStage == 0 ? "RET OFF [N]" : $"RET {veh.RetarderStage}";
                var cabin = veh.GetComponent<KenyaBusPassengerCabin>();
                string loadStr = cabin != null && cabin.MaxSeatCapacity > 0
                    ? $"PAX {cabin.CurrentOnboardCount}/{cabin.MaxSeatCapacity}"
                    : (loop.ActiveJobRole == KenyaStreetVehicle.VehicleJobRole.CargoParcel ? $"PARCELS {loop.PassengersBoarded}" : "PATROL");

                linePaxCruise.text = $"{loadStr}  |  {ccStr}  |  {retStr}";
                linePaxCruise.color = (veh.CruiseControlOn || veh.RetarderStage > 0) ? Palette.UiAccentGreen : Palette.UiTextPrimary;

                lineComfortRest.text = $"COMFORT {Mathf.RoundToInt(PassengerComfort01 * 100f)}%   •   REST {Mathf.RoundToInt(DriverRest01 * 100f)}%";
                if (comfortBarFill != null) comfortBarFill.fillAmount = PassengerComfort01;
                if (restBarFill != null) restBarFill.fillAmount = DriverRest01;

                string speedWarn = veh.SpeedKmh > speedLimit ? " • NTSA OVERSPEED!" : "";
                lineFuelRange.text = $"DIESEL {veh.FuelPercent:0}% (~{veh.EstimatedRangeKm:0} KM){speedWarn}";
                lineFuelRange.color = veh.SpeedKmh > speedLimit ? Palette.UiAccentRed : Palette.UiAccentAmber;

                linePrompt.text = canSleepHere
                    ? "PRESS [F] TO REST 6 HRS (+100% ENERGY)"
                    : "[F1] ADVISOR • [F2] GPS ZOOM • [O] DOOR • [M] MAP";
                linePrompt.color = canSleepHere ? Palette.UiAccentGreen : Palette.UiTextMuted;
            }
        }

        private void PerformDriverRest(StreetGameLoop loop)
        {
            DriverRest01 = 1.0f;
            PassengerComfort01 = Mathf.Clamp01(PassengerComfort01 + 0.15f);
            KenyaTimeOfDay.Instance?.CyclePreset();
            KenyaStreetAudio.Instance?.PlayPickupBlip();
            loop.hud?.ShowMilestoneBanner("DRIVER RESTED — 6 HOURS", "Energy restored to 100%! Fresh Kenyan shift ready on the highway.");
        }

        private void HandleStageBoarded(BusStageZone stage)
        {
            if (PassengerComfort01 >= 0.88f && KenyaBusCareerManager.Instance != null)
            {
                KenyaBusCareerManager.Instance.AwardVipComfortTip(900);
            }
        }

        private void HandlePenalty(int penalty, string reason)
        {
            PassengerComfort01 = Mathf.Clamp01(PassengerComfort01 - 0.18f);
        }

        private static bool IsNearRestPoint(Vector3 pos)
        {
            Vector2 p = new Vector2(pos.x, pos.z);
            for (int i = 0; i < KenyaNetworkData.Hubs.Length; i++)
            {
                if (Vector2.Distance(p, KenyaNetworkData.Hubs[i].xz) < 75f) return true;
            }
            return false;
        }

        private void BuildAdvisorUI()
        {
            if (advisorCard != null) return;
            var cGo = new GameObject("KenyaEts2RouteAdvisorCanvas");
            cGo.transform.SetParent(transform, false);
            advisorCanvas = cGo.AddComponent<Canvas>();
            advisorCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            advisorCanvas.sortingOrder = 105;
            var sc = cGo.AddComponent<CanvasScaler>();
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            sc.referenceResolution = new Vector2(1920f, 1080f);
            sc.matchWidthOrHeight = 0.5f;

            var tex = new Texture2D(2, 2);
            tex.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
            tex.Apply();
            var sp = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f));

            // Sits cleanly right ABOVE the bottom-right Speedometer card (zero HUD overlap!)
            var border = new GameObject("Ets2AdvisorBorder", typeof(RectTransform), typeof(Image));
            border.transform.SetParent(cGo.transform, false);
            var brt = border.GetComponent<RectTransform>();
            brt.anchorMin = new Vector2(1f, 0f);
            brt.anchorMax = new Vector2(1f, 0f);
            brt.pivot = new Vector2(1f, 0f);
            brt.anchoredPosition = new Vector2(-28f, 188f);
            brt.sizeDelta = new Vector2(368f, 206f);
            var bImg = border.GetComponent<Image>();
            bImg.sprite = sp;
            bImg.color = Palette.Hex("#14E8FF");

            var inner = new GameObject("Inner", typeof(RectTransform), typeof(Image));
            inner.transform.SetParent(border.transform, false);
            var irt = inner.GetComponent<RectTransform>();
            irt.anchorMin = Vector2.zero; irt.anchorMax = Vector2.one;
            irt.offsetMin = new Vector2(2f, 2f); irt.offsetMax = new Vector2(-2f, -2f);
            var iImg = inner.GetComponent<Image>();
            iImg.sprite = sp;
            iImg.color = Palette.Hex("#141B26");

            lineRouteHeader   = MakeLine(irt, "RouteHeader",   "TO MIGORI  •  STOP 1/13  (0%)",                 14.5f, FontStyles.Bold,   Palette.Hex("#14E8FF"), -8f);
            journeyBarFill    = MakeBar(irt, sp, "JourneyBar", 12f, -31f, 340f, 8f, Palette.Hex("#14E8FF"));
            lineJourneyAhead  = MakeLine(irt, "JourneyAhead",  "NEXT 420 m (0:26)  •  LEFT 3.25 km (3:18)",    14.0f, FontStyles.Bold,   Palette.UiTextPrimary,  -45f);
            lineUpcomingStops = MakeLine(irt, "UpcomingStops", "AHEAD: MAI MAHIU → NAROK → BOMET",             12.0f, FontStyles.Normal, Palette.Hex("#93C5FD"), -68f);
            linePaxCruise     = MakeLine(irt, "PaxCruise",     "PAX 18/44  |  CC OFF [X]  |  RET OFF [N]",     13.5f, FontStyles.Bold,   Palette.UiTextPrimary,  -94f);
            lineComfortRest   = MakeLine(irt, "ComfortRest",   "COMFORT 96%   •   REST 100%",                  12.5f, FontStyles.Bold,   Palette.UiAccentGreen,  -118f);
            comfortBarFill    = MakeBar(irt, sp, "ComfortBar", 12f, -139f, 162f, 6f, Palette.UiAccentGreen);
            restBarFill       = MakeBar(irt, sp, "RestBar",   190f, -139f, 162f, 6f, Palette.Hex("#38BDF8"));
            lineFuelRange     = MakeLine(irt, "FuelRange",     "DIESEL 88% (~313 KM)",                         13.5f, FontStyles.Bold,   Palette.UiAccentAmber,  -152f);
            linePrompt        = MakeLine(irt, "Prompt",        "[F1] ADVISOR • [F2] GPS ZOOM • [O] DOOR • [M] MAP", 11.0f, FontStyles.Normal, Palette.UiTextMuted, -176f);

            advisorCard = border;
            advisorCard.SetActive(false);
        }

        private static TextMeshProUGUI MakeLine(RectTransform parent, string name, string text, float size, FontStyles style, Color col, float yOff)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(10f, yOff - 22f);
            rt.offsetMax = new Vector2(-10f, yOff);
            var tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.fontStyle = style;
            tmp.color = col;
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Truncate;
            return tmp;
        }

        private static Image MakeBar(RectTransform parent, Sprite sp, string name, float x, float y, float w, float h, Color fillCol)
        {
            var bg = new GameObject(name + "_Bg", typeof(RectTransform), typeof(Image));
            bg.transform.SetParent(parent, false);
            var rt = bg.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, h);
            var bgImg = bg.GetComponent<Image>();
            bgImg.sprite = sp;
            bgImg.color = new Color(0.22f, 0.27f, 0.36f, 1f);

            var fg = new GameObject(name + "_Fill", typeof(RectTransform), typeof(Image));
            fg.transform.SetParent(bg.transform, false);
            var frt = fg.GetComponent<RectTransform>();
            frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one;
            frt.offsetMin = Vector2.zero; frt.offsetMax = Vector2.zero;
            var img = fg.GetComponent<Image>();
            img.sprite = sp;
            img.type = Image.Type.Filled;
            img.fillMethod = Image.FillMethod.Horizontal;
            img.fillAmount = 1f;
            img.color = fillCol;
            return img;
        }

        private static bool WasF1Pressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.F1);
#endif
        }

        private static bool WasSleepKeyPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.F);
#endif
        }
    }
}
