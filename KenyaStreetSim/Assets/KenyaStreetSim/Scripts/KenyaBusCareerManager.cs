using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace KenyaStreetSim
{
    /// <summary>
    /// Kenyan Bus Business Story Mode — "SAVANNA LINE: ROAD TO THE COAST".
    /// 4-Chapter career progression:
    ///   Ch 1: The Migori Hustle (Hired 14-Seater Matatu driver, 30% daily owner hesabu cut)
    ///   Ch 2: Your First Owned Vehicle & Kenyan SACCO (0% owner cut, hire Makanga conductor)
    ///   Ch 3: Night Express & Highway Fleet (Buy 51-Seater Executive Coach, hire AI route drivers)
    ///   Ch 4: National Fleet Empire — Road to the Coast (Multi-corridor fleet & Mombasa-Malindi prestige)
    /// Includes real fuel costs, tire/brake service, passive AI driver dispatch payouts, and [B] uGUI Office overlay.
    /// </summary>
    public class KenyaBusCareerManager : MonoBehaviour
    {
        public static KenyaBusCareerManager Instance { get; private set; }

        [Serializable]
        public struct FleetOffer
        {
            public string id;
            public string title;
            public string subtitle;
            public int priceKes;
            public string matchKeyword;
            public int requiredChapter;
        }

        [Serializable]
        public struct DriverCorridor
        {
            public string id;
            public string name;
            public string routeLabel;
            public int hireCostKes;
            public int payoutPerCycleKes;
        }

        public static readonly string[] SaccoPresets =
        {
            "SAVANNA LINE SACCO",
            "MIGORI EXPRESS SACCO",
            "RIFT HIGHLAND COACHES",
            "LAKE VICTORIA SHUTTLE",
            "COASTLINE ROYAL BUS CO."
        };

        public static readonly FleetOffer[] Offers =
        {
            new FleetOffer
            {
                id = "matatu",
                title = "14-Seater Matatu (Migori Shuttle)",
                subtitle = "Ch 1 Starter • Nimble town feeder • 30% owner hesabu until you buy your own!",
                priceKes = 0,
                matchKeyword = "Matatu",
                requiredChapter = 1
            },
            new FleetOffer
            {
                id = "lorry",
                title = "Savanna Parcel Lorry / Shuttle",
                subtitle = "Ch 2 Owner-Operator • Keep 100% fares & parcels • Unlock SACCO!",
                priceKes = 9500,
                matchKeyword = "Parcel",
                requiredChapter = 1
            },
            new FleetOffer
            {
                id = "coach",
                title = "51-Seater Savanna Express Coach",
                subtitle = "Ch 3 Inter-City Flagship • High passenger capacity • Nairobi–Kisumu–Eldoret–Malaba!",
                priceKes = 24000,
                matchKeyword = "Coach",
                requiredChapter = 2
            },
            new FleetOffer
            {
                id = "police",
                title = "Highway Escort Land Cruiser 79",
                subtitle = "Special VIP Escort & Road Patrol Utility • 4x4 Rift Valley & Tsavo!",
                priceKes = 38000,
                matchKeyword = "Police",
                requiredChapter = 2
            }
        };

        public static readonly DriverCorridor[] Corridors =
        {
            new DriverCorridor { id = "nyanza",  name = "Driver Otieno (Nyanza)",    routeLabel = "Migori – Kisii – Kisumu",      hireCostKes = 12000, payoutPerCycleKes = 1650 },
            new DriverCorridor { id = "rift",    name = "Driver Kiprop (Rift)",      routeLabel = "Kericho – Nakuru – Nairobi",   hireCostKes = 16500, payoutPerCycleKes = 2350 },
            new DriverCorridor { id = "western", name = "Driver Wanjala (Border)",   routeLabel = "Eldoret – Webuye – Malaba",    hireCostKes = 21000, payoutPerCycleKes = 3100 },
            new DriverCorridor { id = "coast",   name = "Driver Juma (Coast)",       routeLabel = "Nairobi – Mombasa – Malindi",  hireCostKes = 28000, payoutPerCycleKes = 4200 }
        };

        // Persistent Career State
        public int BankKes { get; private set; } = 3500;
        public int LifetimeEarnedKes { get; private set; }
        public int StoryChapter { get; private set; } = 1;
        public int SaccoNameIndex { get; private set; }
        public bool HasConductor { get; private set; }
        public float BusCondition { get; private set; } = 1.0f; // 1.0 = 100% healthy tires/brakes
        public float ReputationStars { get; private set; } = 3.2f;
        public int StagesCompletedCareer { get; private set; }
        public bool[] OwnedOffers { get; private set; } = new bool[4];
        public bool[] HiredDrivers { get; private set; } = new bool[4];

        public bool IsOfficeOpen => officeRoot != null && officeRoot.activeSelf;
        public string SaccoName => SaccoPresets[Mathf.Clamp(SaccoNameIndex, 0, SaccoPresets.Length - 1)];
        public bool IsOwnerOperator => OwnedOffers != null && (OwnedOffers[1] || OwnedOffers[2]);

        // UI Elements
        private Canvas officeCanvas;
        private GameObject officeRoot;
        private Sprite solidSprite;
        private TextMeshProUGUI headerSaccoText;
        private TextMeshProUGUI chapterTitleText;
        private TextMeshProUGUI chapterStoryText;
        private TextMeshProUGUI statsSummaryText;
        private TextMeshProUGUI[] offerBtnLabels;
        private TextMeshProUGUI conductorBtnLabel;
        private TextMeshProUGUI serviceBtnLabel;
        private TextMeshProUGUI[] driverBtnLabels;

        private float passiveIncomeTimer;
        private float wearDistanceAccum;
        private Vector3 lastVehiclePos;
        private bool hasLastPos;
        private float fuelCostAccum;

        private const float PassiveCycleSeconds = 25f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            LoadCareer();
        }

        private void Start()
        {
            BuildOfficeUI();
            if (StreetGameLoop.Instance != null)
            {
                StreetGameLoop.Instance.OnStageBoarded += HandleStageBoarded;
            }
            RefreshOfficeUI();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (StreetGameLoop.Instance != null)
            {
                StreetGameLoop.Instance.OnStageBoarded -= HandleStageBoarded;
            }
        }

        private void Update()
        {
            var loop = StreetGameLoop.Instance;
            if (loop == null) return;

            if (WasOfficeHotkeyPressed() && (loop.State == StreetGameLoop.RunState.Running || IsOfficeOpen))
            {
                ToggleOffice();
            }

            if (loop.State != StreetGameLoop.RunState.Running) return;

            // 1. Track driving wear on tires & brakes (heavier on steep Salgaa / Mai Mahiu slopes!)
            if (loop.ActiveVehicle != null)
            {
                Vector3 pos = loop.ActiveVehicle.transform.position;
                if (hasLastPos)
                {
                    float dist = Vector3.Distance(pos, lastVehiclePos);
                    if (dist < 40f)
                    {
                        float slopeFactor = 1f + Mathf.Abs(pos.y - lastVehiclePos.y) * 1.4f;
                        wearDistanceAccum += dist * slopeFactor;
                        if (wearDistanceAccum >= 120f)
                        {
                            wearDistanceAccum = 0f;
                            BusCondition = Mathf.Clamp01(BusCondition - 0.006f);
                        }
                    }
                }
                lastVehiclePos = pos;
                hasLastPos = true;
            }

            // 2. Passive income from hired Kenyan AI route drivers across your SACCO network!
            int activeDriverCount = CountHiredDrivers();
            if (activeDriverCount > 0)
            {
                passiveIncomeTimer += Time.deltaTime;
                if (passiveIncomeTimer >= PassiveCycleSeconds)
                {
                    passiveIncomeTimer = 0f;
                    int totalPayout = 0;
                    for (int i = 0; i < Corridors.Length; i++)
                    {
                        if (HiredDrivers[i]) totalPayout += Corridors[i].payoutPerCycleKes;
                    }
                    if (totalPayout > 0)
                    {
                        BankKes += totalPayout;
                        LifetimeEarnedKes += totalPayout;
                        CheckChapterProgression();
                        SaveCareer();
                        loop.hud?.ShowToast($"{SaccoName} FLEET DISPATCH: +KES {totalPayout:N0} ({activeDriverCount} DRIVERS)");
                        if (IsOfficeOpen) RefreshOfficeUI();
                    }
                }
            }
        }

        public void ToggleOffice()
        {
            if (officeRoot == null) BuildOfficeUI();
            bool next = !officeRoot.activeSelf;
            officeRoot.SetActive(next);
            KenyaStreetAudio.Instance?.PlayUiClick();
            if (next)
            {
                RefreshOfficeUI();
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
            }
        }

        public void CloseOffice()
        {
            if (officeRoot != null && officeRoot.activeSelf)
            {
                officeRoot.SetActive(false);
            }
        }

        /// <summary>Called by KenyaFuelStationZone while refueling to charge realistic Kenyan diesel costs.</summary>
        public bool TryChargeFuelStep(float fuelDeltaNormalized)
        {
            // Full tank costs KES 1,400 (authentic scaled Kenyan diesel cost!)
            fuelCostAccum += fuelDeltaNormalized * 1400f;
            int wholeKes = Mathf.FloorToInt(fuelCostAccum);
            if (wholeKes <= 0) return true;
            fuelCostAccum -= wholeKes;
            if (BankKes >= wholeKes)
            {
                BankKes -= wholeKes;
                return true;
            }
            // Never strand a beginner — allow emergency SACCO credit up to KES 0!
            BankKes = 0;
            return true;
        }

        public string GetCompactHudBadgeText()
        {
            string roleTag = IsOwnerOperator ? SaccoName : "HIRED DRIVER (30% HESABU)";
            return $"CH {StoryChapter}: {roleTag}  |  BANK: KES {BankKes:N0}  |  BUS: {Mathf.RoundToInt(BusCondition * 100f)}%  |  [B] OFFICE";
        }

        public void StartChapter1MigoriStory()
        {
            var loop = StreetGameLoop.Instance;
            if (loop == null) return;

            CloseOffice();
            // Switch to Matatu if present in fleet for authentic Chapter 1 Migori hustle!
            int matatuIdx = FindFleetIndexByKeyword("Matatu");
            if (matatuIdx >= 0)
            {
                loop.SelectVehicleIndex(matatuIdx);
            }

            // Select Migori–Kisii–Nairobi corridor (route index 0) or jump straight to Migori Hub (#13)!
            if (loop.State == StreetGameLoop.RunState.Ready)
            {
                loop.StartRun();
            }
            loop.TeleportToTownHub(13); // Migori / Sirare Border Hub!
            // Set next GPS target north along A1 -> Awendo (#12)!
            loop.SetActiveStageByIndex(12);
            loop.hud?.ShowMilestoneBanner(
                "CH 1: THE MIGORI HUSTLE",
                "Uncle Ochieng: 'Pick passengers in Awendo, Rongo & Kisii — pay hesabu & save for your own coach!'");
        }

        private void HandleStageBoarded(BusStageZone stage)
        {
            if (stage == null) return;
            StagesCompletedCareer++;

            int baseFare = Mathf.Max(1200, stage.rewardKes);
            int conductorBonus = HasConductor ? Mathf.RoundToInt(baseFare * 0.35f) : 0;
            int gross = baseFare + conductorBonus;
            int hesabuCut = IsOwnerOperator ? 0 : Mathf.RoundToInt(gross * 0.30f);
            int netProfit = Mathf.Max(500, gross - hesabuCut);

            BankKes += netProfit;
            LifetimeEarnedKes += netProfit;
            ReputationStars = Mathf.Clamp(ReputationStars + 0.12f, 1.0f, 5.0f);

            string breakdown = hesabuCut > 0
                ? $"FARES +KES {gross:N0}  (-KES {hesabuCut:N0} HESABU)  ->  BANK +KES {netProfit:N0}"
                : (conductorBonus > 0
                    ? $"{SaccoName}: +KES {netProfit:N0} (INCL. +KES {conductorBonus:N0} MAKANGA PARCELS!)"
                    : $"{SaccoName}: +KES {netProfit:N0} NET PROFIT!");

            StreetGameLoop.Instance?.hud?.ShowToast(breakdown);
            CheckChapterProgression();
            SaveCareer();
            if (IsOfficeOpen) RefreshOfficeUI();
        }

        private void CheckChapterProgression()
        {
            var loop = StreetGameLoop.Instance;
            int prev = StoryChapter;
            int ownedCount = CountOwnedOffers();
            int driverCount = CountHiredDrivers();

            if (StoryChapter == 1 && (IsOwnerOperator || LifetimeEarnedKes >= 9500))
            {
                StoryChapter = 2;
                BankKes += 2500;
                loop?.hud?.ShowMilestoneBanner(
                    "CHAPTER 2 UNLOCKED: YOUR OWN SACCO!",
                    "You broke free from daily hesabu! Press [B] to name your SACCO, hire a Makanga & expand to Nairobi!");
            }
            else if (StoryChapter == 2 && (OwnedOffers[2] || (HasConductor && LifetimeEarnedKes >= 24000)))
            {
                StoryChapter = 3;
                BankKes += 5000;
                loop?.hud?.ShowMilestoneBanner(
                    "CHAPTER 3 UNLOCKED: NIGHT EXPRESS & FLEET!",
                    "Rival bus lines are watching! Hire AI route drivers in [B] Office for Kisumu, Eldoret & Malaba!");
            }
            else if (StoryChapter == 3 && driverCount >= 2 && ownedCount >= 2 && LifetimeEarnedKes >= 55000)
            {
                StoryChapter = 4;
                BankKes += 12000;
                loop?.hud?.ShowMilestoneBanner(
                    "CHAPTER 4 UNLOCKED: ROAD TO THE COAST!",
                    "You built a National Kenyan Bus Empire! Conquer Mombasa, Kilifi & Malindi as CEO of " + SaccoName + "!");
            }

            if (StoryChapter != prev) SaveCareer();
        }

        private void OnClickBuyOrDriveOffer(int offerIdx)
        {
            if (offerIdx < 0 || offerIdx >= Offers.Length) return;
            var offer = Offers[offerIdx];
            var loop = StreetGameLoop.Instance;

            if (!OwnedOffers[offerIdx])
            {
                if (BankKes < offer.priceKes)
                {
                    loop?.hud?.ShowToast($"Need KES {offer.priceKes:N0} to buy {offer.title} (Have KES {BankKes:N0})");
                    return;
                }
                BankKes -= offer.priceKes;
                OwnedOffers[offerIdx] = true;
                CheckChapterProgression();
                SaveCareer();
                KenyaStreetAudio.Instance?.PlayPickupBlip();
                loop?.hud?.ShowToast($"PURCHASED {offer.title.ToUpperInvariant()} FOR {SaccoName}!");
            }

            int fleetIdx = FindFleetIndexByKeyword(offer.matchKeyword);
            if (fleetIdx >= 0 && loop != null)
            {
                loop.SelectVehicleIndex(fleetIdx);
                loop.hud?.ShowToast($"Now Driving: {loop.ActiveVehicle.displayName}");
            }
            RefreshOfficeUI();
        }

        private void OnClickHireConductor()
        {
            if (HasConductor) return;
            const int cost = 4500;
            if (BankKes < cost)
            {
                StreetGameLoop.Instance?.hud?.ShowToast($"Need KES {cost:N0} to hire a Makanga Conductor!");
                return;
            }
            BankKes -= cost;
            HasConductor = true;
            ReputationStars = Mathf.Clamp(ReputationStars + 0.3f, 1.0f, 5.0f);
            CheckChapterProgression();
            SaveCareer();
            KenyaStreetAudio.Instance?.PlayPickupBlip();
            StreetGameLoop.Instance?.hud?.ShowToast("HIRED MAKANGA CONDUCTOR! +35% FARES & PARCEL BONUS AT EVERY TOWN!");
            RefreshOfficeUI();
        }

        private void OnClickServiceBus()
        {
            const int cost = 1600;
            if (BusCondition >= 0.99f)
            {
                StreetGameLoop.Instance?.hud?.ShowToast("Your bus tires, brakes & engine are already at 100%!");
                return;
            }
            if (BankKes < cost)
            {
                StreetGameLoop.Instance?.hud?.ShowToast($"Garage full service costs KES {cost:N0}!");
                return;
            }
            BankKes -= cost;
            BusCondition = 1.0f;
            if (StreetGameLoop.Instance?.ActiveVehicle != null)
            {
                StreetGameLoop.Instance.ActiveVehicle.RefuelStep(1.0f);
            }
            SaveCareer();
            KenyaStreetAudio.Instance?.PlayPickupBlip();
            StreetGameLoop.Instance?.hud?.ShowToast("GARAGE SERVICE COMPLETE: 100% TIRES, BRAKES & FULL DIESEL!");
            RefreshOfficeUI();
        }

        private void OnClickHireDriver(int idx)
        {
            if (idx < 0 || idx >= Corridors.Length || HiredDrivers[idx]) return;
            var c = Corridors[idx];
            if (BankKes < c.hireCostKes)
            {
                StreetGameLoop.Instance?.hud?.ShowToast($"Need KES {c.hireCostKes:N0} to hire {c.name}!");
                return;
            }
            BankKes -= c.hireCostKes;
            HiredDrivers[idx] = true;
            CheckChapterProgression();
            SaveCareer();
            KenyaStreetAudio.Instance?.PlayPickupBlip();
            StreetGameLoop.Instance?.hud?.ShowToast($"HIRED {c.name.ToUpperInvariant()}! EARNS +KES {c.payoutPerCycleKes:N0} AUTOMATICALLY!");
            RefreshOfficeUI();
        }

        private void OnClickCycleSaccoName()
        {
            SaccoNameIndex = (SaccoNameIndex + 1) % SaccoPresets.Length;
            SaveCareer();
            KenyaStreetAudio.Instance?.PlayUiClick();
            RefreshOfficeUI();
        }

        private void OnClickResetCareer()
        {
            BankKes = 3500;
            LifetimeEarnedKes = 0;
            StoryChapter = 1;
            SaccoNameIndex = 0;
            HasConductor = false;
            BusCondition = 1.0f;
            ReputationStars = 3.2f;
            StagesCompletedCareer = 0;
            for (int i = 0; i < OwnedOffers.Length; i++) OwnedOffers[i] = (i == 0);
            for (int i = 0; i < HiredDrivers.Length; i++) HiredDrivers[i] = false;
            SaveCareer();
            RefreshOfficeUI();
            StreetGameLoop.Instance?.hud?.ShowToast("CAREER RESET — BACK TO CHAPTER 1: THE MIGORI HUSTLE!");
        }

        private int FindFleetIndexByKeyword(string kw)
        {
            var loop = StreetGameLoop.Instance;
            if (loop == null || loop.fleet == null) return -1;
            for (int i = 0; i < loop.fleet.Length; i++)
            {
                if (loop.fleet[i] == null) continue;
                string dn = loop.fleet[i].displayName ?? "";
                string goName = loop.fleet[i].gameObject.name ?? "";
                if (dn.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    goName.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0)
                    return i;
            }
            return 0;
        }

        private int CountOwnedOffers()
        {
            int c = 0;
            for (int i = 0; i < OwnedOffers.Length; i++) if (OwnedOffers[i]) c++;
            return c;
        }

        private int CountHiredDrivers()
        {
            int c = 0;
            for (int i = 0; i < HiredDrivers.Length; i++) if (HiredDrivers[i]) c++;
            return c;
        }

        private void SaveCareer()
        {
            PlayerPrefs.SetInt("KSS_BankKes", BankKes);
            PlayerPrefs.SetInt("KSS_LifetimeKes", LifetimeEarnedKes);
            PlayerPrefs.SetInt("KSS_Chapter", StoryChapter);
            PlayerPrefs.SetInt("KSS_SaccoIdx", SaccoNameIndex);
            PlayerPrefs.SetInt("KSS_Conductor", HasConductor ? 1 : 0);
            PlayerPrefs.SetFloat("KSS_BusCond", BusCondition);
            PlayerPrefs.SetFloat("KSS_Rep", ReputationStars);
            PlayerPrefs.SetInt("KSS_Stages", StagesCompletedCareer);
            for (int i = 0; i < OwnedOffers.Length; i++) PlayerPrefs.SetInt($"KSS_Own_{i}", OwnedOffers[i] ? 1 : 0);
            for (int i = 0; i < HiredDrivers.Length; i++) PlayerPrefs.SetInt($"KSS_Drv_{i}", HiredDrivers[i] ? 1 : 0);
            PlayerPrefs.Save();
        }

        private void LoadCareer()
        {
            BankKes = PlayerPrefs.GetInt("KSS_BankKes", 3500);
            LifetimeEarnedKes = PlayerPrefs.GetInt("KSS_LifetimeKes", 0);
            StoryChapter = Mathf.Clamp(PlayerPrefs.GetInt("KSS_Chapter", 1), 1, 4);
            SaccoNameIndex = PlayerPrefs.GetInt("KSS_SaccoIdx", 0);
            HasConductor = PlayerPrefs.GetInt("KSS_Conductor", 0) == 1;
            BusCondition = Mathf.Clamp01(PlayerPrefs.GetFloat("KSS_BusCond", 1.0f));
            ReputationStars = Mathf.Clamp(PlayerPrefs.GetFloat("KSS_Rep", 3.2f), 1f, 5f);
            StagesCompletedCareer = PlayerPrefs.GetInt("KSS_Stages", 0);
            for (int i = 0; i < OwnedOffers.Length; i++) OwnedOffers[i] = PlayerPrefs.GetInt($"KSS_Own_{i}", i == 0 ? 1 : 0) == 1;
            for (int i = 0; i < HiredDrivers.Length; i++) HiredDrivers[i] = PlayerPrefs.GetInt($"KSS_Drv_{i}", 0) == 1;
        }

        // =========================================================================
        // CLEAN uGUI KENYAN BUS BUSINESS OFFICE OVERLAY ([B] KEY!)
        // =========================================================================
        private void BuildOfficeUI()
        {
            if (officeRoot != null) return;
            solidSprite = CreateSolidSprite();

            var cGo = new GameObject("KenyaBusCareerOfficeCanvas");
            cGo.transform.SetParent(transform, false);
            officeCanvas = cGo.AddComponent<Canvas>();
            officeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            officeCanvas.sortingOrder = 140;
            var scaler = cGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;
            cGo.AddComponent<GraphicRaycaster>();

            officeRoot = new GameObject("OfficeOverlayBackdrop");
            officeRoot.transform.SetParent(cGo.transform, false);
            var bgRect = officeRoot.AddComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero; bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero; bgRect.offsetMax = Vector2.zero;
            var bgImg = officeRoot.AddComponent<Image>();
            bgImg.sprite = solidSprite;
            bgImg.color = new Color(0.03f, 0.05f, 0.09f, 0.84f);

            // Main Executive Office Card
            var card = MakePanel(officeRoot.transform, "OfficeCard", new Vector2(0.06f, 0.06f), new Vector2(0.94f, 0.94f), new Color(0.07f, 0.10f, 0.16f, 0.98f));

            // Top Header Strip
            var topBar = MakePanel(card, "TopBar", new Vector2(0f, 0.87f), new Vector2(1f, 1f), new Color(0.55f, 0.10f, 0.12f, 1f));
            headerSaccoText = MakeText(topBar, "SaccoHeader", SaccoName, new Vector2(0.02f, 0.15f), new Vector2(0.52f, 0.88f), 26, FontStyles.Bold, Color.white, TextAlignmentOptions.MidlineLeft);
            MakeButton(topBar, "RenameBtn", "CHANGE SACCO NAME", new Vector2(0.53f, 0.22f), new Vector2(0.70f, 0.78f), new Color(0.14f, 0.20f, 0.30f, 1f), OnClickCycleSaccoName);
            MakeButton(topBar, "StartMigoriBtn", "START CH 1 IN MIGORI", new Vector2(0.71f, 0.22f), new Vector2(0.87f, 0.78f), new Color(0.10f, 0.54f, 0.28f, 1f), StartChapter1MigoriStory);
            MakeButton(topBar, "CloseBtn", "CLOSE [B]", new Vector2(0.88f, 0.22f), new Vector2(0.98f, 0.78f), new Color(0.22f, 0.24f, 0.30f, 1f), CloseOffice);

            // Story Chapter Banner
            var storyBox = MakePanel(card, "StoryBox", new Vector2(0.02f, 0.67f), new Vector2(0.98f, 0.85f), new Color(0.10f, 0.15f, 0.23f, 1f));
            chapterTitleText = MakeText(storyBox, "ChTitle", "", new Vector2(0.02f, 0.56f), new Vector2(0.65f, 0.94f), 20, FontStyles.Bold, Palette.UiAccentAmber, TextAlignmentOptions.MidlineLeft);
            chapterStoryText = MakeText(storyBox, "ChStory", "", new Vector2(0.02f, 0.08f), new Vector2(0.65f, 0.54f), 14, FontStyles.Normal, new Color(0.88f, 0.92f, 0.96f), TextAlignmentOptions.MidlineLeft);
            statsSummaryText = MakeText(storyBox, "StatsSummary", "", new Vector2(0.67f, 0.08f), new Vector2(0.98f, 0.92f), 15, FontStyles.Bold, Palette.UiAccentGreen, TextAlignmentOptions.MidlineRight);

            // LEFT COLUMN: FLEET YARD (Buy & Drive Matatu -> Parcel Lorry -> 51-Seater Coach -> VIP Escort)
            var fleetBox = MakePanel(card, "FleetYard", new Vector2(0.02f, 0.03f), new Vector2(0.51f, 0.65f), new Color(0.09f, 0.12f, 0.19f, 1f));
            MakeText(fleetBox, "FleetHdr", "KENYA BUS YARD — BUY & DRIVE FLEET", new Vector2(0.03f, 0.88f), new Vector2(0.97f, 0.98f), 16, FontStyles.Bold, Palette.UiAccentAmber, TextAlignmentOptions.MidlineLeft);
            offerBtnLabels = new TextMeshProUGUI[Offers.Length];
            for (int i = 0; i < Offers.Length; i++)
            {
                int idx = i;
                float yTop = 0.85f - i * 0.21f;
                float yBot = yTop - 0.185f;
                var row = MakePanel(fleetBox, $"OfferRow_{i}", new Vector2(0.03f, yBot), new Vector2(0.97f, yTop), new Color(0.12f, 0.16f, 0.25f, 1f));
                MakeText(row, "Title", Offers[i].title, new Vector2(0.03f, 0.52f), new Vector2(0.66f, 0.94f), 15, FontStyles.Bold, Color.white, TextAlignmentOptions.MidlineLeft);
                MakeText(row, "Sub",   Offers[i].subtitle, new Vector2(0.03f, 0.06f), new Vector2(0.66f, 0.50f), 11.5f, FontStyles.Normal, new Color(0.75f, 0.82f, 0.90f), TextAlignmentOptions.MidlineLeft);
                offerBtnLabels[i] = MakeButton(row, $"BuyBtn_{i}", "BUY", new Vector2(0.68f, 0.18f), new Vector2(0.97f, 0.82f), new Color(0.12f, 0.50f, 0.28f, 1f), () => OnClickBuyOrDriveOffer(idx));
            }

            // RIGHT COLUMN: STAFF, GARAGE & AI ROUTE DRIVERS
            var opsBox = MakePanel(card, "OperationsBox", new Vector2(0.53f, 0.03f), new Vector2(0.98f, 0.65f), new Color(0.09f, 0.12f, 0.19f, 1f));
            MakeText(opsBox, "OpsHdr", "SACCO CREW, GARAGE & AI ROUTE DRIVERS", new Vector2(0.03f, 0.88f), new Vector2(0.97f, 0.98f), 16, FontStyles.Bold, Palette.UiAccentAmber, TextAlignmentOptions.MidlineLeft);

            // Top row inside right column: Makanga Conductor + Garage Service
            conductorBtnLabel = MakeButton(opsBox, "ConductorBtn", "HIRE MAKANGA (KES 4,500)", new Vector2(0.03f, 0.72f), new Vector2(0.49f, 0.86f), new Color(0.16f, 0.42f, 0.68f, 1f), OnClickHireConductor);
            serviceBtnLabel   = MakeButton(opsBox, "ServiceBtn",   "GARAGE SERVICE (KES 1,600)", new Vector2(0.51f, 0.72f), new Vector2(0.97f, 0.86f), new Color(0.68f, 0.44f, 0.10f, 1f), OnClickServiceBus);

            // 4 AI Route Driver corridors
            driverBtnLabels = new TextMeshProUGUI[Corridors.Length];
            for (int i = 0; i < Corridors.Length; i++)
            {
                int idx = i;
                float yTop = 0.68f - i * 0.145f;
                float yBot = yTop - 0.125f;
                var row = MakePanel(opsBox, $"DrvRow_{i}", new Vector2(0.03f, yBot), new Vector2(0.97f, yTop), new Color(0.12f, 0.16f, 0.25f, 1f));
                MakeText(row, "DrvName", $"{Corridors[i].name}  •  {Corridors[i].routeLabel}", new Vector2(0.03f, 0.10f), new Vector2(0.62f, 0.90f), 12.5f, FontStyles.Bold, Color.white, TextAlignmentOptions.MidlineLeft);
                driverBtnLabels[i] = MakeButton(row, $"DrvBtn_{i}", "HIRE", new Vector2(0.64f, 0.16f), new Vector2(0.97f, 0.84f), new Color(0.10f, 0.48f, 0.34f, 1f), () => OnClickHireDriver(idx));
            }

            MakeButton(opsBox, "ResetBtn", "RESET STORY CAREER", new Vector2(0.64f, 0.02f), new Vector2(0.97f, 0.09f), new Color(0.45f, 0.14f, 0.16f, 1f), OnClickResetCareer);
            officeRoot.SetActive(false);
        }

        private void RefreshOfficeUI()
        {
            if (officeRoot == null) return;
            headerSaccoText.text = $"SAVANNA LINE STORY  •  {SaccoName}";

            string chTitle, chDesc;
            switch (StoryChapter)
            {
                case 1:
                    chTitle = "CHAPTER 1: THE MIGORI HUSTLE (HIRED DRIVER)";
                    chDesc = "Uncle Ochieng loaned you a 14-seater Matatu in Migori. 30% of every stage fare goes to daily hesabu. Complete runs through Awendo, Rongo & Kisii until you buy your own vehicle (KES 9,500)!";
                    break;
                case 2:
                    chTitle = "CHAPTER 2: OWNER-OPERATOR & YOUR OWN KENYAN SACCO";
                    chDesc = "You keep 100% of fares now! Hire a Makanga Conductor (+35% boarding & parcel bonuses), build your SACCO reputation, and save KES 24,000 for your 51-seater Executive Coach!";
                    break;
                case 3:
                    chTitle = "CHAPTER 3: NIGHT EXPRESS & HIGHWAY RIVALS";
                    chDesc = "Rival operators own the Rift Valley! Hire AI Route Drivers across Nyanza, Rift, Western & Coast corridors to earn passive income while you run flagship routes to Eldoret & Malaba!";
                    break;
                default:
                    chTitle = "CHAPTER 4: NATIONAL FLEET EMPIRE — ROAD TO THE COAST!";
                    chDesc = "You are CEO of " + SaccoName + "! Your coaches run across all 41 Kenyan towns. Lead the prestige highway run from Nairobi through Tsavo to Mombasa, Kilifi & Malindi!";
                    break;
            }
            chapterTitleText.text = chTitle;
            chapterStoryText.text = chDesc;

            int activeDrivers = CountHiredDrivers();
            int passiveSum = 0;
            for (int i = 0; i < Corridors.Length; i++) if (HiredDrivers[i]) passiveSum += Corridors[i].payoutPerCycleKes;
            statsSummaryText.text =
                $"BANK: KES {BankKes:N0}\n" +
                $"LIFETIME: KES {LifetimeEarnedKes:N0}  |  {ReputationStars:0.0} ★\n" +
                $"BUS HEALTH: {Mathf.RoundToInt(BusCondition * 100f)}%  |  FLEET: +{passiveSum:N0}/25s";

            for (int i = 0; i < Offers.Length; i++)
            {
                offerBtnLabels[i].text = OwnedOffers[i] ? "OWNED • DRIVE" : $"BUY KES {Offers[i].priceKes:N0}";
            }

            conductorBtnLabel.text = HasConductor ? "MAKANGA HIRED (+35%)" : "HIRE MAKANGA (KES 4,500)";
            serviceBtnLabel.text = $"SERVICE {Mathf.RoundToInt(BusCondition * 100f)}% (KES 1,600)";

            for (int i = 0; i < Corridors.Length; i++)
            {
                driverBtnLabels[i].text = HiredDrivers[i]
                    ? $"ACTIVE (+{Corridors[i].payoutPerCycleKes:N0})"
                    : $"HIRE KES {Corridors[i].hireCostKes:N0}";
            }
        }

        private RectTransform MakePanel(Transform parent, string name, Vector2 aMin, Vector2 aMax, Color col)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = aMin; rt.anchorMax = aMax;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var img = go.AddComponent<Image>();
            img.sprite = solidSprite;
            img.color = col;
            return rt;
        }

        private TextMeshProUGUI MakeText(Transform parent, string name, string text, Vector2 aMin, Vector2 aMax, float size, FontStyles style, Color col, TextAlignmentOptions align)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = aMin; rt.anchorMax = aMax;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.fontStyle = style;
            tmp.color = col;
            tmp.alignment = align;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            return tmp;
        }

        private TextMeshProUGUI MakeButton(Transform parent, string name, string label, Vector2 aMin, Vector2 aMax, Color col, Action onClick)
        {
            var rt = MakePanel(parent, name, aMin, aMax, col);
            var btn = rt.gameObject.AddComponent<Button>();
            btn.onClick.AddListener(() => onClick?.Invoke());
            return MakeText(rt, "Label", label, new Vector2(0.04f, 0.04f), new Vector2(0.96f, 0.96f), 12.5f, FontStyles.Bold, Color.white, TextAlignmentOptions.Center);
        }

        private static Sprite CreateSolidSprite()
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f));
        }

        private static bool WasOfficeHotkeyPressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.bKey.wasPressedThisFrame) return true;
#endif
            return false;
        }
    }
}
