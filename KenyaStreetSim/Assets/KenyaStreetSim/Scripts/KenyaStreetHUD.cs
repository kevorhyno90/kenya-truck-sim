using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace KenyaStreetSim
{
    /// <summary>
    /// Full palette-styled uGUI Canvas Shell & Gameplay HUD (zero OnGUI, zero default Unity gray skin).
    /// Owns Title Screen, Gameplay HUD, Boarding Bar, Animated KES/Stage/Safety counters,
    /// Milestone Banner + Floating Pickup Text + Accent Flash, Pause + Settings Sliders, End Screen, and 0.3s Fades.
    /// </summary>
    public class KenyaStreetHUD : MonoBehaviour
    {
        private Canvas rootCanvas;
        private Sprite solidSprite;

        // Screens
        private GameObject titleScreen;
        private GameObject gameplayHud;
        private GameObject pauseScreen;
        private GameObject endScreen;

        // Title controls: 4 Selectable Game Modes + 5 Playable Kenyan Mod Vehicles
        private TextMeshProUGUI titlePromptText;
        private TextMeshProUGUI modeDescText;
        private Button[] modeSelectButtons;
        private Button[] vehicleSelectButtons;

        // Gameplay HUD widgets
        private TextMeshProUGUI stageLabelText;
        private TextMeshProUGUI nextStageDistText;
        private Image routeFillBar;
        private TextMeshProUGUI safetyText;
        private Image safetyFillBar;
        private TextMeshProUGUI kesValueText;
        private RectTransform kesBadgeRect;
        private TextMeshProUGUI passengerValueText;
        private TextMeshProUGUI vehicleBadgeText;
        private TextMeshProUGUI speedValueText;
        private TextMeshProUGUI gearValueText;
        private Image rpmFillBar;
        private TextMeshProUGUI leftSignalIcon;
        private TextMeshProUGUI rightSignalIcon;
        private TextMeshProUGUI lightsTodStatusText;
        private GameObject boardingPanel;
        private Image boardingFillBar;
        private TextMeshProUGUI boardingLabelText;
        private TextMeshProUGUI toastText;

        // Game Feel & Transition Overlays
        private Image flashOverlay;
        private Image fadeCurtain;
        private RectTransform milestoneBannerRect;
        private TextMeshProUGUI milestoneTitleText;
        private TextMeshProUGUI milestoneSubText;
        private RectTransform floatingLayer;

        // Pause & Settings
        private Slider musicSlider;
        private Slider sfxSlider;
        private TextMeshProUGUI musicPctText;
        private TextMeshProUGUI sfxPctText;

        // End Screen
        private TextMeshProUGUI endHeadlineText;
        private TextMeshProUGUI endSubtitleText;
        private TextMeshProUGUI endStatsText;

        // Animated counters & tweens
        private float displayedKes;
        private float targetKes;
        private float displayedRouteFill;
        private float targetRouteFill;
        private float displayedSafetyFill = 1f;
        private float targetSafetyFill = 1f;
        private Coroutine routeBarRoutine;
        private Coroutine safetyBarRoutine;
        private Coroutine milestoneRoutine;
        private Coroutine toastRoutine;
        private Coroutine fadeRoutine;

        public Image RouteFillBar => routeFillBar;
        public RectTransform KesBadgeRect => kesBadgeRect;

        private void Awake()
        {
            solidSprite = CreateSolidSprite();
            EnsureEventSystem();
            BuildCanvasHierarchy();
        }

        private void Start()
        {
            if (StreetGameLoop.Instance != null)
            {
                StreetGameLoop.Instance.OnStageBoarded += HandleStageBoarded;
                StreetGameLoop.Instance.OnSafetyPenalty += HandleSafetyPenalty;
                ApplyRunState(StreetGameLoop.Instance.State);
            }
        }

        private void OnDestroy()
        {
            if (StreetGameLoop.Instance != null)
            {
                StreetGameLoop.Instance.OnStageBoarded -= HandleStageBoarded;
                StreetGameLoop.Instance.OnSafetyPenalty -= HandleSafetyPenalty;
            }
        }

        private void Update()
        {
            var loop = StreetGameLoop.Instance;
            if (loop == null) return;

            if (loop.State == StreetGameLoop.RunState.Ready && titlePromptText != null)
            {
                float alpha = Mathf.Lerp(0.45f, 1f, 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * (Mathf.PI * 2f / 1.6f)));
                titlePromptText.color = Palette.WithAlpha(Palette.UiTextPrimary, alpha);
            }

            if (loop.State == StreetGameLoop.RunState.Running)
            {
                UpdateLiveTelemetry(loop);
            }

            // Smoothly count up KES display
            if (!Mathf.Approximately(displayedKes, targetKes))
            {
                displayedKes = Mathf.MoveTowards(displayedKes, targetKes, Mathf.Max(1500f, Mathf.Abs(targetKes - displayedKes) * 4f) * Time.unscaledDeltaTime);
                if (kesValueText != null)
                {
                    kesValueText.text = $"KES {Mathf.RoundToInt(displayedKes):N0}";
                }
            }
        }

        public void ApplyRunState(StreetGameLoop.RunState state)
        {
            if (titleScreen != null) titleScreen.SetActive(state == StreetGameLoop.RunState.Ready);
            if (gameplayHud != null) gameplayHud.SetActive(state == StreetGameLoop.RunState.Running || state == StreetGameLoop.RunState.Paused);
            if (pauseScreen != null) pauseScreen.SetActive(state == StreetGameLoop.RunState.Paused);
            if (endScreen != null) endScreen.SetActive(state == StreetGameLoop.RunState.Won || state == StreetGameLoop.RunState.Lost);

            if (state == StreetGameLoop.RunState.Ready)
            {
                displayedKes = 0f;
                targetKes = 0f;
                displayedRouteFill = 0f;
                targetRouteFill = 0f;
                displayedSafetyFill = 1f;
                targetSafetyFill = 1f;
                if (routeFillBar != null) routeFillBar.fillAmount = 0f;
                if (safetyFillBar != null) safetyFillBar.fillAmount = 1f;
                if (kesValueText != null) kesValueText.text = "KES 0";
                RefreshVehicleSelectorVisuals();
            }
            else if (state == StreetGameLoop.RunState.Paused)
            {
                SyncAudioSliders();
            }
            else if (state == StreetGameLoop.RunState.Won || state == StreetGameLoop.RunState.Lost)
            {
                PopulateEndScreen(state == StreetGameLoop.RunState.Won);
            }

            PlayFadeInTransition();
        }

        private void UpdateLiveTelemetry(StreetGameLoop loop)
        {
            var veh = loop.ActiveVehicle;
            if (veh != null)
            {
                if (speedValueText != null)
                    speedValueText.text = $"{Mathf.RoundToInt(veh.SpeedKmh)}";
                if (gearValueText != null)
                    gearValueText.text = veh.GearLabel;
                if (rpmFillBar != null)
                    rpmFillBar.fillAmount = veh.RpmNormalized;
                if (leftSignalIcon != null)
                    leftSignalIcon.color = (veh.BlinkPhaseOn && (veh.LeftSignalOn || veh.HazardsOn))
                        ? Palette.UiAccentGreen
                        : Palette.WithAlpha(Palette.UiTextMuted, 0.28f);
                if (rightSignalIcon != null)
                    rightSignalIcon.color = (veh.BlinkPhaseOn && (veh.RightSignalOn || veh.HazardsOn))
                        ? Palette.UiAccentGreen
                        : Palette.WithAlpha(Palette.UiTextMuted, 0.28f);
                if (lightsTodStatusText != null)
                {
                    string tod = KenyaTimeOfDay.Instance != null
                        ? KenyaTimeOfDay.Instance.CurrentPreset.ToString().ToUpperInvariant()
                        : "DAY";
                    string beam = veh.HeadlightsOn ? "BEAMS ON" : "BEAMS OFF";
                    lightsTodStatusText.text = $"{beam} [L]   •   {tod} [T]";
                }
                if (vehicleBadgeText != null)
                    vehicleBadgeText.text = $"{veh.displayName}  •  [V] Switch";
            }

            int totalStages = loop.stages != null ? loop.stages.Length : 16;
            if (stageLabelText != null)
            {
                stageLabelText.text = $"{loop.ModeObjectiveNoun}  {loop.CompletedStageCount} / {totalStages}";
            }
            if (passengerValueText != null)
            {
                passengerValueText.text = $"{loop.PassengersBoarded} {loop.ModeCounterNoun}";
            }
            if (safetyText != null)
            {
                safetyText.text = loop.ActiveMode == StreetGameLoop.GameMode.FreeRoamKenya
                    ? "FREE ROAM  •  NO PENALTIES"
                    : $"SAFETY  {loop.SafetyRating}%";
            }

            var activeStage = loop.ActiveStage;
            if (activeStage != null && veh != null)
            {
                float dist = Vector3.Distance(veh.transform.position, activeStage.transform.position);
                string distStr = dist >= 950f ? $"{dist / 1000f:0.2} km" : $"{Mathf.RoundToInt(dist)}m";
                string prefix = loop.ActiveMode switch
                {
                    StreetGameLoop.GameMode.KenyaPolicePatrol => "DISPATCH",
                    StreetGameLoop.GameMode.IsuzuCargoHaul => "DELIVERY",
                    StreetGameLoop.GameMode.FreeRoamKenya => "WAYPOINT",
                    _ => "NEXT"
                };
                if (nextStageDistText != null)
                {
                    nextStageDistText.text = $"{prefix}: {activeStage.stageName.ToUpperInvariant()}  ({distStr})";
                }

                bool showBoarding = activeStage.PlayerInside || activeStage.BoardingProgress > 0.01f;
                if (boardingPanel != null) boardingPanel.SetActive(showBoarding);
                if (showBoarding)
                {
                    if (boardingFillBar != null) boardingFillBar.fillAmount = activeStage.BoardingProgress;
                    if (boardingLabelText != null)
                    {
                        string actionVerb = loop.ActiveMode switch
                        {
                            StreetGameLoop.GameMode.KenyaPolicePatrol => "SECURING POLICE CHECKPOINT",
                            StreetGameLoop.GameMode.IsuzuCargoHaul => "UNLOADING KENYA FREIGHT",
                            StreetGameLoop.GameMode.FreeRoamKenya => "CHECKING IN AT HUB",
                            _ => "BOARDING PASSENGERS"
                        };
                        boardingLabelText.text = veh.SpeedKmh > 12f
                            ? $"SLOW DOWN AT {activeStage.stageName.ToUpperInvariant()}"
                            : $"{actionVerb} — {activeStage.stageName.ToUpperInvariant()}  ({Mathf.RoundToInt(activeStage.BoardingProgress * 100f)}%)";
                    }
                }
            }
            else
            {
                if (boardingPanel != null) boardingPanel.SetActive(false);
            }
        }

        private void HandleStageBoarded(BusStageZone stage)
        {
            var loop = StreetGameLoop.Instance;
            if (loop == null) return;

            targetKes = loop.TotalKesEarned;
            int totalStages = loop.stages != null && loop.stages.Length > 0 ? loop.stages.Length : 4;
            targetRouteFill = Mathf.Clamp01((float)loop.CompletedStageCount / totalStages);

            if (routeBarRoutine != null) StopCoroutine(routeBarRoutine);
            routeBarRoutine = StartCoroutine(TweenBarRoutine(routeFillBar, displayedRouteFill, targetRouteFill, v => displayedRouteFill = v));

            if (kesBadgeRect != null)
            {
                StartCoroutine(PopRectRoutine(kesBadgeRect));
            }

            SpawnFloatingText($"+KES {stage.rewardKes:N0}   +{stage.passengerCount} PAX", Palette.UiAccentAmber);
            TriggerMilestoneMoment($"STAGE COMPLETE!", $"{stage.stageName}  •  +KES {stage.rewardKes:N0}", Palette.UiAccentAmber);
        }

        private void HandleSafetyPenalty(int penalty, string reason)
        {
            var loop = StreetGameLoop.Instance;
            if (loop == null) return;

            targetSafetyFill = Mathf.Clamp01(loop.SafetyRating / 100f);
            if (safetyBarRoutine != null) StopCoroutine(safetyBarRoutine);
            safetyBarRoutine = StartCoroutine(TweenBarRoutine(safetyFillBar, displayedSafetyFill, targetSafetyFill, v => displayedSafetyFill = v));

            StartCoroutine(FlashOverlayRoutine(Palette.UiAccentRed, 0.45f, 0.22f));
            SpawnFloatingText($"-{penalty}% SAFETY  •  {reason}", Palette.UiAccentRed);
            ShowToast($"{reason} (-{penalty}% Safety)");
        }

        public void TriggerMilestoneMoment(string headline, string subline, Color accentColor)
        {
            if (milestoneRoutine != null) StopCoroutine(milestoneRoutine);
            milestoneRoutine = StartCoroutine(MilestoneRoutine(headline, subline, accentColor));
        }

        private IEnumerator MilestoneRoutine(string headline, string subline, Color accentColor)
        {
            // 0ms: Brief accent flash
            StartCoroutine(FlashOverlayRoutine(accentColor, 0.42f, 0.14f));
            yield return new WaitForSecondsRealtime(0.05f);

            if (milestoneBannerRect != null)
            {
                milestoneBannerRect.gameObject.SetActive(true);
                if (milestoneTitleText != null)
                {
                    milestoneTitleText.text = headline;
                    milestoneTitleText.color = accentColor;
                }
                if (milestoneSubText != null) milestoneSubText.text = subline;

                // 50ms -> 400ms: Scale 0 -> 1 OutBack overshoot
                yield return Ease.Tween(0.35f, t =>
                {
                    milestoneBannerRect.localScale = Vector3.one * Mathf.Max(0.001f, t);
                }, Ease.OutBack, unscaled: true);

                // Hold ~1.1s
                yield return new WaitForSecondsRealtime(1.1f);

                // Scale 1 -> 0 OutCubic
                yield return Ease.Tween(0.22f, t =>
                {
                    milestoneBannerRect.localScale = Vector3.one * Mathf.Max(0.001f, 1f - t);
                }, Ease.OutCubic, unscaled: true);

                milestoneBannerRect.gameObject.SetActive(false);
            }
        }

        public void SpawnFloatingText(string message, Color color)
        {
            if (floatingLayer == null) return;
            var go = new GameObject("FloatingFeedbackText", typeof(RectTransform));
            go.transform.SetParent(floatingLayer, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.56f);
            rt.anchorMax = new Vector2(0.5f, 0.56f);
            rt.sizeDelta = new Vector2(600f, 52f);
            rt.anchoredPosition = Vector2.zero;

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = message;
            tmp.fontSize = 30f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = color;
            tmp.raycastTarget = false;

            StartCoroutine(FloatAndDestroyRoutine(rt, tmp));
        }

        private IEnumerator FloatAndDestroyRoutine(RectTransform rt, TextMeshProUGUI tmp)
        {
            Color startColor = tmp.color;
            yield return Ease.Tween(0.85f, t =>
            {
                if (rt == null || tmp == null) return;
                rt.anchoredPosition = new Vector2(0f, t * 92f);
                tmp.color = Palette.WithAlpha(startColor, 1f - t * t);
            }, Ease.OutCubic, unscaled: true);

            if (rt != null) Destroy(rt.gameObject);
        }

        public void ShowToast(string message)
        {
            if (toastText == null) return;
            if (toastRoutine != null) StopCoroutine(toastRoutine);
            toastRoutine = StartCoroutine(ToastRoutine(message));
        }

        private IEnumerator ToastRoutine(string message)
        {
            toastText.gameObject.SetActive(true);
            toastText.text = message;
            toastText.color = Palette.UiTextPrimary;
            yield return new WaitForSecondsRealtime(2.2f);
            yield return Ease.Tween(0.35f, t =>
            {
                toastText.color = Palette.WithAlpha(Palette.UiTextPrimary, 1f - t);
            }, Ease.OutCubic, unscaled: true);
            toastText.gameObject.SetActive(false);
        }

        private IEnumerator TweenBarRoutine(Image bar, float from, float to, System.Action<float> setter)
        {
            if (bar == null) yield break;
            yield return Ease.Tween(0.32f, t =>
            {
                float v = Mathf.Lerp(from, to, t);
                bar.fillAmount = v;
                setter?.Invoke(v);
            }, Ease.OutCubic, unscaled: true);
        }

        private IEnumerator PopRectRoutine(RectTransform rect)
        {
            Vector3 baseline = Vector3.one;
            yield return Ease.Tween(0.15f, t =>
            {
                if (rect != null) rect.localScale = baseline * Mathf.Lerp(1f, 1.24f, t);
            }, Ease.OutBack, unscaled: true);
            yield return Ease.Tween(0.15f, t =>
            {
                if (rect != null) rect.localScale = baseline * Mathf.Lerp(1.24f, 1f, t);
            }, Ease.OutCubic, unscaled: true);
            if (rect != null) rect.localScale = baseline;
        }

        private IEnumerator FlashOverlayRoutine(Color color, float peakAlpha, float duration)
        {
            if (flashOverlay == null) yield break;
            flashOverlay.gameObject.SetActive(true);
            yield return Ease.Tween(duration, t =>
            {
                flashOverlay.color = Palette.WithAlpha(color, Mathf.Lerp(peakAlpha, 0f, t));
            }, Ease.OutCubic, unscaled: true);
            flashOverlay.gameObject.SetActive(false);
        }

        private void PlayFadeInTransition()
        {
            if (fadeCurtain == null) return;
            if (fadeRoutine != null) StopCoroutine(fadeRoutine);
            fadeRoutine = StartCoroutine(FadeInRoutine());
        }

        private IEnumerator FadeInRoutine()
        {
            fadeCurtain.gameObject.SetActive(true);
            yield return Ease.Tween(0.30f, t =>
            {
                fadeCurtain.color = Palette.WithAlpha(Palette.UiFadeCurtain, Mathf.Lerp(0.85f, 0f, t));
            }, Ease.OutCubic, unscaled: true);
            fadeCurtain.gameObject.SetActive(false);
        }

        private void PopulateEndScreen(bool won)
        {
            var loop = StreetGameLoop.Instance;
            if (loop == null) return;

            if (endHeadlineText != null)
            {
                endHeadlineText.text = won ? "ROUTE COMPLETE!" : "SHIFT ENDED";
                endHeadlineText.color = won ? Palette.UiAccentGreen : Palette.UiAccentRed;
            }
            if (endSubtitleText != null)
            {
                endSubtitleText.text = loop.EndReason;
            }
            if (endStatsText != null)
            {
                int mins = Mathf.FloorToInt(loop.ElapsedRunSeconds / 60f);
                int secs = Mathf.FloorToInt(loop.ElapsedRunSeconds % 60f);
                int totalStages = loop.stages != null ? loop.stages.Length : 4;
                endStatsText.text =
                    $"TOTAL FARE EARNED:  KES {loop.TotalKesEarned:N0}\n" +
                    $"STAGES COMPLETED:  {loop.CompletedStageCount} / {totalStages}\n" +
                    $"PASSENGERS BOARDED:  {loop.PassengersBoarded}\n" +
                    $"SAFETY RATING:  {loop.SafetyRating}%\n" +
                    $"ROUTE TIME:  {mins:00}:{secs:00}";
            }

            if (won)
            {
                TriggerMilestoneMoment("KENYA ROUTE COMPLETE!", $"Earned KES {loop.TotalKesEarned:N0}", Palette.UiAccentGreen);
            }
        }

        private void RefreshVehicleSelectorVisuals()
        {
            var loop = StreetGameLoop.Instance;
            int vehIdx = loop != null ? loop.ActiveVehicleIndex : 0;
            int modeIdx = loop != null ? (int)loop.ActiveMode : 0;

            if (modeSelectButtons != null)
            {
                for (int i = 0; i < modeSelectButtons.Length; i++)
                {
                    SetSelectorHighlight(modeSelectButtons[i], i == modeIdx, isPoliceBlue: i == 1);
                }
            }
            if (vehicleSelectButtons != null)
            {
                for (int i = 0; i < vehicleSelectButtons.Length; i++)
                {
                    SetSelectorHighlight(vehicleSelectButtons[i], i == vehIdx, isPoliceBlue: i == 1);
                }
            }
            if (modeDescText != null && loop != null)
            {
                modeDescText.text = loop.ActiveMode switch
                {
                    StreetGameLoop.GameMode.EnaCoachCareer => "ENA COACH CAREER: Pick up commuters across 16 Kenyan towns, obey traffic & earn KES fares!",
                    StreetGameLoop.GameMode.KenyaPolicePatrol => "KENYA POLICE PATROL: Drive KCP 380D Land Cruiser 79, activate [G] Flashing Lightbar & Siren, respond to 16 highway checkpoints!",
                    StreetGameLoop.GameMode.IsuzuCargoHaul => "ISUZU CARGO HAUL: Transport Kenyan freight between regional depots in the Real 3D Kenyan Isuzu FRR & Isuzu TX Lorry!",
                    _ => "FREE ROAM KENYA: Open-world drive across all 16 Kenyan towns & 8 highways with ZERO safety penalties — switch vehicles anytime [V]!"
                };
            }
        }

        private void SetSelectorHighlight(Button btn, bool selected, bool isPoliceBlue = false)
        {
            if (btn == null) return;
            var img = btn.GetComponent<Image>();
            if (img != null)
            {
                img.color = selected
                    ? (isPoliceBlue ? Palette.Hex("#2468E0") : Palette.UiAccentAmber)
                    : Palette.UiButtonNormal;
            }
            var lbl = btn.GetComponentInChildren<TextMeshProUGUI>();
            if (lbl != null)
            {
                lbl.color = selected
                    ? (isPoliceBlue ? Palette.UiTextPrimary : Palette.UiFadeCurtain)
                    : Palette.UiTextPrimary;
            }
        }

        private void SyncAudioSliders()
        {
            var audio = KenyaStreetAudio.Instance;
            if (audio == null) return;
            if (musicSlider != null) musicSlider.SetValueWithoutNotify(audio.musicVolume);
            if (sfxSlider != null) sfxSlider.SetValueWithoutNotify(audio.sfxVolume);
            if (musicPctText != null) musicPctText.text = $"{Mathf.RoundToInt(audio.musicVolume * 100f)}%";
            if (sfxPctText != null) sfxPctText.text = $"{Mathf.RoundToInt(audio.sfxVolume * 100f)}%";
        }

        private void BuildCanvasHierarchy()
        {
            var canvasGo = new GameObject("KenyaStreetCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);

            rootCanvas = canvasGo.GetComponent<Canvas>();
            rootCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            rootCanvas.sortingOrder = 100;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var root = canvasGo.GetComponent<RectTransform>();

            BuildTitleScreen(root);
            BuildGameplayHud(root);
            BuildPauseScreen(root);
            BuildEndScreen(root);
            BuildFeedbackOverlays(root);
        }

        private void BuildTitleScreen(RectTransform root)
        {
            titleScreen = CreateFullPanel("TitleScreen", root, Palette.WithAlpha(Palette.UiFadeCurtain, 0.22f));
            var tr = titleScreen.GetComponent<RectTransform>();

            // Crisp 100% opaque dark charcoal title banner at top (leaving middle open to showcase the live 3D vehicle!)
            var titleCard = CreateBorderedAnchoredPanel("TitleCard", tr, new Vector2(0.5f, 0.86f), new Vector2(1060f, 146f), Palette.Hex("#161D28"), Palette.UiAccentAmber, 3f);

            var titleTmp = CreateText("GameTitle", titleCard, "KENYA STREET SIM", 66f, FontStyles.Bold, Palette.UiAccentAmber, TextAlignmentOptions.Center);
            StretchRect(titleTmp.rectTransform, 20f, 44f, -20f, -10f);

            var subTmp = CreateText("GameSubtitle", titleCard, "16 KENYAN TOWNS  •  SELECTABLE MODES & REAL 3D KENYAN FLEET  •  ZERO SPEED BUMPS", 18.5f, FontStyles.Normal, Palette.UiTextPrimary, TextAlignmentOptions.Center);
            subTmp.textWrappingMode = TextWrappingModes.NoWrap;
            StretchRect(subTmp.rectTransform, 20f, 12f, -20f, -96f);

            // Row 1: 4 Selectable Game Modes Panel
            var modeCard = CreateBorderedAnchoredPanel("GameModeSelector", tr, new Vector2(0.5f, 0.35f), new Vector2(1180f, 114f), Palette.Hex("#161D28"), Palette.UiPanelBorder, 2f);
            var modeHeader = CreateText("ModeHeader", modeCard, "SELECT GAME MODE", 15f, FontStyles.Bold, Palette.UiAccentAmber, TextAlignmentOptions.Left);
            SetBox(modeHeader.rectTransform, 18f, -8f, 300f, 20f, new Vector2(0f, 1f));

            modeDescText = CreateText("ModeDesc", modeCard, "", 14.5f, FontStyles.Normal, Palette.UiTextMuted, TextAlignmentOptions.Right);
            modeDescText.textWrappingMode = TextWrappingModes.NoWrap;
            modeDescText.overflowMode = TextOverflowModes.Truncate;
            SetBox(modeDescText.rectTransform, -18f, -8f, 860f, 20f, new Vector2(1f, 1f));

            string[] modeLabels = { "ENA COACH CAREER", "KENYA POLICE PATROL", "ISUZU CARGO HAUL", "FREE ROAM KENYA" };
            modeSelectButtons = new Button[modeLabels.Length];
            for (int m = 0; m < modeLabels.Length; m++)
            {
                int capturedMode = m;
                float xPos = -426f + m * 284f;
                modeSelectButtons[m] = CreateButton($"ModeBtn_{m}", modeCard, modeLabels[m], new Vector2(xPos, -12f), new Vector2(268f, 52f), Palette.UiButtonNormal, Palette.UiTextPrimary, () =>
                {
                    StreetGameLoop.Instance?.SelectGameMode((StreetGameLoop.GameMode)capturedMode, autoSelectRecommendedVehicle: true);
                    RefreshVehicleSelectorVisuals();
                });
                var lbl = modeSelectButtons[m].GetComponentInChildren<TextMeshProUGUI>();
                if (lbl != null) lbl.fontSize = 17.5f;
            }

            // Row 2: 5 Selectable Real 3D Kenyan Mod Vehicles Garage
            var fleetRow = CreateBorderedAnchoredPanel("FleetSelector", tr, new Vector2(0.5f, 0.21f), new Vector2(1180f, 92f), Palette.Hex("#161D28"), Palette.UiPanelBorder, 2f);
            var fleetHeader = CreateText("FleetHeader", fleetRow, "SELECT VEHICLE / MOD  (OR PRESS [V] ANYTIME IN-GAME)", 14.5f, FontStyles.Bold, Palette.UiAccentAmber, TextAlignmentOptions.Left);
            SetBox(fleetHeader.rectTransform, 18f, -6f, 620f, 18f, new Vector2(0f, 1f));

            string[] vehLabels = { "SCANIA 2026 (ENA)", "KENYA POLICE LC79", "ISUZU FRR (KENYA)", "ISUZU TX LORRY", "MARCOPOLO G7" };
            vehicleSelectButtons = new Button[vehLabels.Length];
            for (int v = 0; v < vehLabels.Length; v++)
            {
                int capturedVeh = v;
                float xPos = -456f + v * 228f;
                vehicleSelectButtons[v] = CreateButton($"SelectVehBtn_{v}", fleetRow, vehLabels[v], new Vector2(xPos, -10f), new Vector2(216f, 48f), Palette.UiButtonNormal, Palette.UiTextPrimary, () =>
                {
                    StreetGameLoop.Instance?.SelectVehicleIndex(capturedVeh);
                    RefreshVehicleSelectorVisuals();
                });
                var lbl = vehicleSelectButtons[v].GetComponentInChildren<TextMeshProUGUI>();
                if (lbl != null) lbl.fontSize = 15.5f;
            }

            // Start button & pulsing prompt cleanly at bottom
            CreateButton("StartDriveBtn", tr, "START DRIVING", new Vector2(0f, -432f), new Vector2(340f, 58f), Palette.UiAccentRed, Palette.UiTextPrimary, () =>
            {
                StreetGameLoop.Instance?.StartRun();
            });

            titlePromptText = CreateText("PromptLine", tr, "PRESS  ENTER  OR  SPACE  TO  HIT  THE  KENYAN  HIGHWAY", 18.5f, FontStyles.Bold, Palette.UiTextPrimary, TextAlignmentOptions.Center);
            titlePromptText.characterSpacing = 3.5f;
            var prt = titlePromptText.rectTransform;
            prt.anchorMin = new Vector2(0.5f, 0.035f);
            prt.anchorMax = new Vector2(0.5f, 0.035f);
            prt.sizeDelta = new Vector2(960f, 32f);
            prt.anchoredPosition = Vector2.zero;
        }

        private void BuildGameplayHud(RectTransform root)
        {
            gameplayHud = new GameObject("GameplayHUD", typeof(RectTransform));
            gameplayHud.transform.SetParent(root, false);
            StretchRect(gameplayHud.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);
            var gr = gameplayHud.GetComponent<RectTransform>();

            Color panelFill = Palette.Hex("#171E29");

            // Top-Left: Route Stage & Safety Card
            var topLeft = CreateBorderedCornerPanel("RouteSafetyCard", gr, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -24f), new Vector2(520f, 158f), panelFill, Palette.UiPanelBorder, 2f);

            stageLabelText = CreateText("StageLabel", topLeft, "STAGES  0 / 10", 23f, FontStyles.Bold, Palette.UiAccentAmber, TextAlignmentOptions.Left);
            SetBox(stageLabelText.rectTransform, 18f, -14f, 260f, 30f, new Vector2(0f, 1f));

            nextStageDistText = CreateText("NextStageText", topLeft, "NEXT: NAIROBI CBD ENA TERMINAL (77m)", 18f, FontStyles.Normal, Palette.UiTextPrimary, TextAlignmentOptions.Left);
            nextStageDistText.textWrappingMode = TextWrappingModes.NoWrap;
            nextStageDistText.overflowMode = TextOverflowModes.Truncate;
            SetBox(nextStageDistText.rectTransform, 18f, -45f, 484f, 26f, new Vector2(0f, 1f));

            routeFillBar = CreateProgressBar("RouteProgressBar", topLeft, 18f, -78f, 480f, 14f, Palette.UiAccentAmber);

            safetyText = CreateText("SafetyText", topLeft, "SAFETY  100%", 19f, FontStyles.Bold, Palette.UiAccentGreen, TextAlignmentOptions.Left);
            SetBox(safetyText.rectTransform, 18f, -102f, 260f, 24f, new Vector2(0f, 1f));

            safetyFillBar = CreateProgressBar("SafetyBar", topLeft, 18f, -130f, 480f, 12f, Palette.UiAccentGreen);
            safetyFillBar.fillAmount = 1f;

            // Top-Right: KES Earnings & Fleet Card
            var topRight = CreateBorderedCornerPanel("EarningsCard", gr, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-28f, -24f), new Vector2(376f, 132f), panelFill, Palette.UiPanelBorder, 2f);
            kesBadgeRect = topRight;

            kesValueText = CreateText("KesValue", topRight, "KES 0", 42f, FontStyles.Bold, Palette.UiAccentAmber, TextAlignmentOptions.Right);
            SetBox(kesValueText.rectTransform, -18f, -12f, 340f, 50f, new Vector2(1f, 1f));

            passengerValueText = CreateText("PassengerCount", topRight, "0 BOARDED", 20f, FontStyles.Normal, Palette.UiTextPrimary, TextAlignmentOptions.Right);
            SetBox(passengerValueText.rectTransform, -18f, -66f, 340f, 26f, new Vector2(1f, 1f));

            vehicleBadgeText = CreateText("VehicleBadge", topRight, "ENA Coach KDK 057M  •  [V] Switch", 17f, FontStyles.Normal, Palette.UiTextMuted, TextAlignmentOptions.Right);
            SetBox(vehicleBadgeText.rectTransform, -18f, -96f, 340f, 24f, new Vector2(1f, 1f));

            // Bottom-Right: ETS2-Style Speedometer, RPM Bar, Indicators & Gear
            var botRight = CreateBorderedCornerPanel("SpeedoCard", gr, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-28f, 26f), new Vector2(310f, 152f), panelFill, Palette.UiAccentRed, 2.5f);

            leftSignalIcon = CreateText("LeftSigIcon", botRight, "◄", 24f, FontStyles.Bold, Palette.WithAlpha(Palette.UiTextMuted, 0.28f), TextAlignmentOptions.Left);
            SetBox(leftSignalIcon.rectTransform, 16f, -10f, 36f, 28f, new Vector2(0f, 1f));

            lightsTodStatusText = CreateText("LightsTodStatus", botRight, "BEAMS OFF [L]  •  DAY [T]", 14.5f, FontStyles.Normal, Palette.UiTextMuted, TextAlignmentOptions.Center);
            SetBox(lightsTodStatusText.rectTransform, 0f, -13f, 224f, 24f, new Vector2(0.5f, 1f));

            rightSignalIcon = CreateText("RightSigIcon", botRight, "►", 24f, FontStyles.Bold, Palette.WithAlpha(Palette.UiTextMuted, 0.28f), TextAlignmentOptions.Right);
            SetBox(rightSignalIcon.rectTransform, -16f, -10f, 36f, 28f, new Vector2(1f, 1f));

            speedValueText = CreateText("SpeedVal", botRight, "0", 60f, FontStyles.Bold, Palette.UiTextPrimary, TextAlignmentOptions.Right);
            SetBox(speedValueText.rectTransform, -98f, 44f, 185f, 68f, new Vector2(1f, 0f));

            var kmhLbl = CreateText("KmhLbl", botRight, "KM/H", 16f, FontStyles.Bold, Palette.UiTextMuted, TextAlignmentOptions.Right);
            SetBox(kmhLbl.rectTransform, -98f, 26f, 185f, 20f, new Vector2(1f, 0f));

            rpmFillBar = CreateProgressBar("RpmBar", botRight, 16f, -130f, 276f, 9f, Palette.UiAccentAmber);

            var gearPill = CreateCornerPanel("GearPill", botRight, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-18f, -2f), new Vector2(64f, 76f), Palette.UiAccentRed);
            gearValueText = CreateText("GearVal", gearPill, "N", 38f, FontStyles.Bold, Palette.UiTextPrimary, TextAlignmentOptions.Center);
            StretchRect(gearValueText.rectTransform, 0f, 0f, 0f, 0f);

            // Bottom-Left: Single-Line Compact Controls Pill
            var botLeft = CreateBorderedCornerPanel("ControlsPill", gr, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(28f, 26f), new Vector2(1140f, 44f), panelFill, Palette.UiPanelBorder, 1.5f);
            var ctrlTxt = CreateText("ControlsTxt", botLeft, "WASD Drive  •  M Kenya Map  •  V Switch Mod  •  G Police Siren  •  Q/E/Z Signals  •  L Beams  •  T Time  •  C Cockpit  •  H Horn  •  ESC", 15f, FontStyles.Normal, Palette.UiTextMuted, TextAlignmentOptions.Center);
            ctrlTxt.textWrappingMode = TextWrappingModes.NoWrap;
            StretchRect(ctrlTxt.rectTransform, 14f, 4f, -14f, -4f);

            // Center-Bottom: Bus Stage Boarding Progress Panel
            var boardRt = CreateBorderedAnchoredPanel("BoardingPanel", gr, new Vector2(0.5f, 0.16f), new Vector2(560f, 88f), panelFill, Palette.UiAccentAmber, 2.5f);
            boardingPanel = boardRt.gameObject;

            boardingLabelText = CreateText("BoardingLbl", boardRt, "BOARDING PASSENGERS — CBD STAGE", 20f, FontStyles.Bold, Palette.UiAccentAmber, TextAlignmentOptions.Center);
            SetBox(boardingLabelText.rectTransform, 0f, -12f, 520f, 30f, new Vector2(0.5f, 1f));

            boardingFillBar = CreateProgressBar("BoardingBar", boardRt, 26f, -52f, 504f, 18f, Palette.UiAccentGreen);
            boardingPanel.SetActive(false);

            // Top-Center Toast
            toastText = CreateText("ToastText", gr, "", 22f, FontStyles.Bold, Palette.UiTextPrimary, TextAlignmentOptions.Center);
            var trt = toastText.rectTransform;
            trt.anchorMin = new Vector2(0.5f, 0.89f);
            trt.anchorMax = new Vector2(0.5f, 0.89f);
            trt.sizeDelta = new Vector2(720f, 38f);
            trt.anchoredPosition = Vector2.zero;
            toastText.gameObject.SetActive(false);
        }

        private void BuildPauseScreen(RectTransform root)
        {
            pauseScreen = CreateFullPanel("PauseScreen", root, Palette.UiBackdropScrim);
            var pr = pauseScreen.GetComponent<RectTransform>();

            var modal = CreateBorderedAnchoredPanel("PauseModal", pr, new Vector2(0.5f, 0.5f), new Vector2(560f, 520f), Palette.WithAlpha(Palette.Hex("#171E29"), 0.96f), Palette.UiAccentAmber, 3f);

            var heading = CreateText("PauseTitle", modal, "PAUSED", 44f, FontStyles.Bold, Palette.UiAccentAmber, TextAlignmentOptions.Center);
            SetBox(heading.rectTransform, 0f, -26f, 500f, 54f, new Vector2(0.5f, 1f));

            // Audio Settings Sliders (MusicVol & SFXVol bound live to gain layer)
            BuildSliderRow(modal, "AMBIENT / MUSIC", -108f, out musicSlider, out musicPctText, val =>
            {
                KenyaStreetAudio.Instance?.SetMusicVolume(val);
                if (musicPctText != null) musicPctText.text = $"{Mathf.RoundToInt(val * 100f)}%";
            });

            BuildSliderRow(modal, "ENGINE & SFX", -168f, out sfxSlider, out sfxPctText, val =>
            {
                KenyaStreetAudio.Instance?.SetSfxVolume(val);
                if (sfxPctText != null) sfxPctText.text = $"{Mathf.RoundToInt(val * 100f)}%";
            });

            CreateButton("ResumeBtn", modal, "RESUME DRIVE", new Vector2(0f, -24f), new Vector2(360f, 56f), Palette.UiAccentGreen, Palette.UiFadeCurtain, () =>
            {
                StreetGameLoop.Instance?.ResumeRun();
            });
            CreateButton("RestartBtn", modal, "RESTART ROUTE", new Vector2(0f, -96f), new Vector2(360f, 54f), Palette.UiButtonNormal, Palette.UiTextPrimary, () =>
            {
                StreetGameLoop.Instance?.RestartRun();
            });
            CreateButton("TitleBtn", modal, "MAIN TITLE", new Vector2(0f, -164f), new Vector2(360f, 50f), Palette.UiButtonNormal, Palette.UiTextMuted, () =>
            {
                StreetGameLoop.Instance?.ReturnToTitle();
            });
        }

        private void BuildEndScreen(RectTransform root)
        {
            endScreen = CreateFullPanel("EndScreen", root, Palette.UiBackdropScrim);
            var er = endScreen.GetComponent<RectTransform>();

            var modal = CreateBorderedAnchoredPanel("EndModal", er, new Vector2(0.5f, 0.5f), new Vector2(660f, 520f), Palette.WithAlpha(Palette.Hex("#171E29"), 0.96f), Palette.UiAccentAmber, 3f);

            endHeadlineText = CreateText("EndHeadline", modal, "ROUTE COMPLETE!", 48f, FontStyles.Bold, Palette.UiAccentGreen, TextAlignmentOptions.Center);
            SetBox(endHeadlineText.rectTransform, 0f, -28f, 600f, 60f, new Vector2(0.5f, 1f));

            endSubtitleText = CreateText("EndSubtitle", modal, "All Kenyan Town Bus Stages Completed!", 22f, FontStyles.Normal, Palette.UiTextPrimary, TextAlignmentOptions.Center);
            SetBox(endSubtitleText.rectTransform, 0f, -94f, 600f, 34f, new Vector2(0.5f, 1f));

            endStatsText = CreateText("EndStats", modal, "", 24f, FontStyles.Normal, Palette.UiTextPrimary, TextAlignmentOptions.Center);
            SetBox(endStatsText.rectTransform, 0f, -148f, 580f, 195f, new Vector2(0.5f, 1f));

            CreateButton("PlayAgainBtn", modal, "PLAY AGAIN", new Vector2(-128f, -182f), new Vector2(228f, 58f), Palette.UiAccentAmber, Palette.UiFadeCurtain, () =>
            {
                StreetGameLoop.Instance?.RestartRun();
            });
            CreateButton("EndTitleBtn", modal, "MAIN TITLE", new Vector2(128f, -182f), new Vector2(228f, 58f), Palette.UiButtonNormal, Palette.UiTextPrimary, () =>
            {
                StreetGameLoop.Instance?.ReturnToTitle();
            });
        }

        private void BuildFeedbackOverlays(RectTransform root)
        {
            // Floating pickup text container
            var floatGo = new GameObject("FloatingFeedbackLayer", typeof(RectTransform));
            floatGo.transform.SetParent(root, false);
            floatingLayer = floatGo.GetComponent<RectTransform>();
            StretchRect(floatingLayer, 0f, 0f, 0f, 0f);

            // Milestone Banner (scales 0 -> 1 OutBack)
            milestoneBannerRect = CreateBorderedAnchoredPanel("MilestoneBanner", root, new Vector2(0.5f, 0.74f), new Vector2(640f, 128f), Palette.WithAlpha(Palette.Hex("#171E29"), 0.96f), Palette.UiAccentAmber, 3f);

            milestoneTitleText = CreateText("MilestoneTitle", milestoneBannerRect, "STAGE COMPLETE!", 42f, FontStyles.Bold, Palette.UiAccentAmber, TextAlignmentOptions.Center);
            SetBox(milestoneTitleText.rectTransform, 0f, -18f, 600f, 52f, new Vector2(0.5f, 1f));

            milestoneSubText = CreateText("MilestoneSub", milestoneBannerRect, "+KES 1,200", 22f, FontStyles.Normal, Palette.UiTextPrimary, TextAlignmentOptions.Center);
            SetBox(milestoneSubText.rectTransform, 0f, -74f, 600f, 34f, new Vector2(0.5f, 1f));
            milestoneBannerRect.gameObject.SetActive(false);

            // Accent Flash Overlay
            var flashGo = CreateFullPanel("FlashOverlay", root, Color.clear);
            flashOverlay = flashGo.GetComponent<Image>();
            flashOverlay.raycastTarget = false;
            flashGo.SetActive(false);

            // Transition Fade Curtain
            var fadeGo = CreateFullPanel("FadeCurtain", root, Color.clear);
            fadeCurtain = fadeGo.GetComponent<Image>();
            fadeCurtain.raycastTarget = false;
            fadeGo.SetActive(false);
        }

        private void BuildSliderRow(RectTransform parent, string label, float yOffset, out Slider slider, out TextMeshProUGUI pctText, UnityEngine.Events.UnityAction<float> onChanged)
        {
            var lbl = CreateText(label + "_Lbl", parent, label, 18f, FontStyles.Normal, Palette.UiTextMuted, TextAlignmentOptions.Left);
            SetBox(lbl.rectTransform, 38f, yOffset, 200f, 30f, new Vector2(0f, 1f));

            pctText = CreateText(label + "_Pct", parent, "80%", 19f, FontStyles.Bold, Palette.UiTextPrimary, TextAlignmentOptions.Right);
            SetBox(pctText.rectTransform, -38f, yOffset, 72f, 30f, new Vector2(1f, 1f));

            var sliderGo = new GameObject(label + "_Slider", typeof(RectTransform), typeof(Slider));
            sliderGo.transform.SetParent(parent, false);
            var srt = sliderGo.GetComponent<RectTransform>();
            srt.anchorMin = new Vector2(0f, 1f);
            srt.anchorMax = new Vector2(0f, 1f);
            srt.pivot = new Vector2(0f, 1f);
            srt.anchoredPosition = new Vector2(246f, yOffset - 4f);
            srt.sizeDelta = new Vector2(195f, 22f);

            var bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bg.transform.SetParent(sliderGo.transform, false);
            var bgImg = bg.GetComponent<Image>();
            bgImg.sprite = solidSprite;
            bgImg.color = Palette.UiBarTrack;
            StretchRect(bg.GetComponent<RectTransform>(), 0f, 6f, 0f, -6f);

            var fillArea = new GameObject("FillArea", typeof(RectTransform));
            fillArea.transform.SetParent(sliderGo.transform, false);
            StretchRect(fillArea.GetComponent<RectTransform>(), 0f, 6f, 0f, -6f);

            var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(fillArea.transform, false);
            var fillImg = fill.GetComponent<Image>();
            fillImg.sprite = solidSprite;
            fillImg.color = Palette.UiAccentAmber;
            StretchRect(fill.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);

            var handleArea = new GameObject("HandleArea", typeof(RectTransform));
            handleArea.transform.SetParent(sliderGo.transform, false);
            StretchRect(handleArea.GetComponent<RectTransform>(), 6f, 0f, -6f, 0f);

            var handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            handle.transform.SetParent(handleArea.transform, false);
            var hImg = handle.GetComponent<Image>();
            hImg.sprite = solidSprite;
            hImg.color = Palette.UiTextPrimary;
            handle.GetComponent<RectTransform>().sizeDelta = new Vector2(18f, 22f);

            slider = sliderGo.GetComponent<Slider>();
            slider.fillRect = fill.GetComponent<RectTransform>();
            slider.handleRect = handle.GetComponent<RectTransform>();
            slider.targetGraphic = hImg;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 0.8f;
            slider.onValueChanged.AddListener(onChanged);
        }

        private Image CreateProgressBar(string name, RectTransform parent, float x, float y, float width, float height, Color fillColor)
        {
            var track = new GameObject(name + "_Track", typeof(RectTransform), typeof(Image));
            track.transform.SetParent(parent, false);
            var trt = track.GetComponent<RectTransform>();
            trt.anchorMin = new Vector2(0f, 1f);
            trt.anchorMax = new Vector2(0f, 1f);
            trt.pivot = new Vector2(0f, 1f);
            trt.anchoredPosition = new Vector2(x, y);
            trt.sizeDelta = new Vector2(width, height);
            var tImg = track.GetComponent<Image>();
            tImg.sprite = solidSprite;
            tImg.color = Palette.UiBarTrack;

            var fill = new GameObject(name + "_Fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(track.transform, false);
            StretchRect(fill.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);
            var fImg = fill.GetComponent<Image>();
            fImg.sprite = solidSprite; // Required so Image.Type.Filled animates properly!
            fImg.type = Image.Type.Filled;
            fImg.fillMethod = Image.FillMethod.Horizontal;
            fImg.fillOrigin = (int)Image.OriginHorizontal.Left;
            fImg.fillAmount = 0f;
            fImg.color = fillColor;
            return fImg;
        }

        private Button CreateButton(string name, RectTransform parent, string label, Vector2 anchoredPos, Vector2 size, Color bgColor, Color textColor, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;

            var img = go.GetComponent<Image>();
            img.sprite = solidSprite;
            img.color = bgColor;

            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1f);
            colors.pressedColor = new Color(0.84f, 0.84f, 0.84f, 1f);
            btn.colors = colors;
            btn.onClick.AddListener(onClick);

            var tmp = CreateText("Label", rt, label, 22f, FontStyles.Bold, textColor, TextAlignmentOptions.Center);
            StretchRect(tmp.rectTransform, 8f, 4f, -8f, -4f);
            return btn;
        }

        private GameObject CreateFullPanel(string name, RectTransform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            StretchRect(go.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);
            var img = go.GetComponent<Image>();
            img.sprite = solidSprite;
            img.color = color;
            return go;
        }

        private RectTransform CreateAnchoredPanel(string name, RectTransform parent, Vector2 anchor, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = Vector2.zero;
            var img = go.GetComponent<Image>();
            img.sprite = solidSprite;
            img.color = color;
            return rt;
        }

        private RectTransform CreateCornerPanel(string name, RectTransform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var img = go.GetComponent<Image>();
            img.sprite = solidSprite;
            img.color = color;
            return rt;
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

        private RectTransform CreateBorderedAnchoredPanel(string name, RectTransform parent, Vector2 anchor, Vector2 size, Color fillColor, Color borderColor, float borderPx)
        {
            var outer = CreateAnchoredPanel(name, parent, anchor, size, borderColor);
            var inner = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            inner.transform.SetParent(outer, false);
            var irt = inner.GetComponent<RectTransform>();
            StretchRect(irt, borderPx, borderPx, -borderPx, -borderPx);
            var img = inner.GetComponent<Image>();
            img.sprite = solidSprite;
            img.color = fillColor;
            return outer;
        }

        private RectTransform CreateBorderedCornerPanel(string name, RectTransform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, Color fillColor, Color borderColor, float borderPx)
        {
            var outer = CreateCornerPanel(name, parent, anchor, pivot, pos, size, borderColor);
            var inner = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            inner.transform.SetParent(outer, false);
            var irt = inner.GetComponent<RectTransform>();
            StretchRect(irt, borderPx, borderPx, -borderPx, -borderPx);
            var img = inner.GetComponent<Image>();
            img.sprite = solidSprite;
            img.color = fillColor;
            return outer;
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

        private static Sprite CreateSolidSprite()
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            var px = new[] { Color.white, Color.white, Color.white, Color.white };
            tex.SetPixels(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f));
        }

        private static void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null) return;
            var es = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
            es.AddComponent<InputSystemUIInputModule>();
#else
            es.AddComponent<StandaloneInputModule>();
#endif
        }
    }
}
