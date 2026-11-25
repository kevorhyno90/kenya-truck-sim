using System;
using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace KenyaStreetSim
{
    /// <summary>
    /// Single authoritative run-state & shell owner for KenyaStreetSim.
    /// States: Ready (Title), Running (Playing), Paused, Won, Lost.
    /// Owns bus-stage progression, KES earnings, safety rating, vehicle switching, and clean restarts.
    /// </summary>
    public class StreetGameLoop : MonoBehaviour
    {
        public enum RunState { Ready, Running, Paused, Won, Lost }
        public enum GameMode { EnaCoachCareer, KenyaPolicePatrol, IsuzuCargoHaul, FreeRoamKenya }

        public static StreetGameLoop Instance { get; private set; }

        [Header("Fleet & Stages")]
        public KenyaStreetVehicle[] fleet;
        public BusStageZone[] stages;
        public KenyaStreetCamera streetCamera;
        public KenyaStreetHUD hud;
        public KenyaRouteMapUI routeMap;
        public Vector3 startPosition = new Vector3(-3.5f, 0.28f, -18f);

        public RunState State { get; private set; } = RunState.Ready;
        public GameMode ActiveMode { get; private set; } = GameMode.EnaCoachCareer;
        public int ActiveVehicleIndex { get; private set; }
        public KenyaStreetVehicle ActiveVehicle =>
            (fleet != null && fleet.Length > 0) ? fleet[Mathf.Clamp(ActiveVehicleIndex, 0, fleet.Length - 1)] : null;

        public string ModeDisplayName => ActiveMode switch
        {
            GameMode.EnaCoachCareer => "ENA COACH CAREER",
            GameMode.KenyaPolicePatrol => "KENYA POLICE PATROL",
            GameMode.IsuzuCargoHaul => "ISUZU CARGO HAUL",
            _ => "FREE ROAM KENYA"
        };

        public KenyaStreetVehicle.VehicleJobRole ActiveJobRole =>
            ActiveVehicle != null ? ActiveVehicle.jobRole : KenyaStreetVehicle.VehicleJobRole.BusPassenger;

        public string ModeObjectiveNoun => ActiveJobRole switch
        {
            KenyaStreetVehicle.VehicleJobRole.PolicePatrol => "PATROLS",
            KenyaStreetVehicle.VehicleJobRole.CargoParcel => "DELIVERIES",
            _ => "STAGES"
        };

        public string ModeCounterNoun => ActiveJobRole switch
        {
            KenyaStreetVehicle.VehicleJobRole.PolicePatrol => "SECURED",
            KenyaStreetVehicle.VehicleJobRole.CargoParcel => "PARCELS",
            _ => "BOARDED"
        };

        public int ActiveRouteIndex { get; private set; }
        public KenyaRouteCatalog.RouteDef ActiveRoute => KenyaRouteCatalog.GetRoute(ActiveRouteIndex);
        public int CurrentRouteStopStep { get; private set; }
        public int RouteTotalStops => ActiveRoute.stopHubIndices != null && ActiveRoute.stopHubIndices.Length > 0
            ? ActiveRoute.stopHubIndices.Length
            : (stages != null ? stages.Length : 16);

        public int CurrentStageIndex { get; private set; }
        public int CompletedStageCount { get; private set; }
        public int TotalKesEarned { get; private set; }
        public int PassengersBoarded { get; private set; }
        public int SafetyRating { get; private set; } = 100;
        public float ElapsedRunSeconds { get; private set; }
        public string EndReason { get; private set; } = string.Empty;

        public event Action<int> OnRouteSelected;

        public BusStageZone ActiveStage =>
            (stages != null && CurrentStageIndex >= 0 && CurrentStageIndex < stages.Length)
                ? stages[CurrentStageIndex]
                : null;

        public event Action<RunState> OnStateChanged;
        public event Action<BusStageZone> OnStageBoarded;
        public event Action<int, string> OnSafetyPenalty;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            Time.timeScale = 1f;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            Time.timeScale = 1f;
        }

        private void Start()
        {
            if (streetCamera == null && Camera.main != null)
            {
                streetCamera = Camera.main.GetComponent<KenyaStreetCamera>();
            }
            if (hud == null)
            {
                hud = FindAnyObjectByType<KenyaStreetHUD>();
            }
            if (routeMap == null)
            {
                routeMap = FindAnyObjectByType<KenyaRouteMapUI>();
            }

            // Ensure runtime world streaming optimizer and visible 3D bus passenger cabins are always active!
            if (KenyaWorldPerformanceManager.Instance == null && GetComponent<KenyaWorldPerformanceManager>() == null)
            {
                gameObject.AddComponent<KenyaWorldPerformanceManager>();
            }
            if (fleet != null)
            {
                for (int i = 0; i < fleet.Length; i++)
                {
                    if (fleet[i] != null &&
                        fleet[i].jobRole == KenyaStreetVehicle.VehicleJobRole.BusPassenger &&
                        fleet[i].GetComponent<KenyaBusPassengerCabin>() == null)
                    {
                        fleet[i].gameObject.AddComponent<KenyaBusPassengerCabin>();
                    }
                }
            }

            // Initialize fleet: ENA Coach active at start position, Isuzu FRR standby
            ActivateVehicle(0, resetPose: true);
            ResetStagesForNewRun();
            SetState(RunState.Ready);
        }

        private void Update()
        {
            if (State == RunState.Running)
            {
                ElapsedRunSeconds += Time.deltaTime;
                HandleRunningHotkeys();
            }
            else if (State == RunState.Paused)
            {
                if (WasEscapePressed())
                {
                    ResumeRun();
                }
            }
            else if (State == RunState.Ready)
            {
                if (KenyaStreetAudio.Instance != null)
                {
                    KenyaStreetAudio.Instance.UpdateEngine(0f, 0f, false);
                }
                if (WasAnyStartKeyPressed())
                {
                    StartRun();
                }
            }
        }

        private void HandleRunningHotkeys()
        {
            if (routeMap != null && routeMap.IsFullMapOpen) return;

            if (WasEscapePressed())
            {
                PauseRun();
                return;
            }

            if (WasSwitchVehiclePressed())
            {
                SwitchVehicle();
            }
        }

        public void SetActiveStageByIndex(int hubIndex)
        {
            if (stages == null || stages.Length == 0) return;
            int idx = Mathf.Clamp(hubIndex, 0, stages.Length - 1);
            CurrentStageIndex = idx;
            for (int i = 0; i < stages.Length; i++)
            {
                if (stages[i] == null) continue;
                stages[i].SetStageState(active: i == idx && !stages[i].IsCompleted, completed: stages[i].IsCompleted);
            }
            if (stages[idx] != null && stages[idx].IsCompleted)
            {
                stages[idx].ResetStage(active: true);
            }
            hud?.ShowToast($"GPS Route Set: {stages[idx].stageName}");
        }

        public void TeleportToTownHub(int hubIndex)
        {
            if (hubIndex < 0 || hubIndex >= KenyaNetworkData.Hubs.Length || ActiveVehicle == null) return;
            var hub = KenyaNetworkData.Hubs[hubIndex];

            Vector3 spawn;
            Quaternion rot;
            // Always place the player cleanly on the straight/curved highway approaching this town's ACTUAL
            // verified Bus Stage Terminal (never inside a roundabout island!)
            if (stages != null && hubIndex < stages.Length && stages[hubIndex] != null)
            {
                Transform st = stages[hubIndex].transform;
                Vector3 stageFwd = st.forward; stageFwd.y = 0f;
                if (stageFwd.sqrMagnitude < 0.01f) stageFwd = Vector3.forward;
                stageFwd.Normalize();

                Vector3 probePos = st.position - stageFwd * 24f;
                if (KenyaNetworkData.FindNearestRoadPoint(probePos, out Vector3 snappedPt, out Vector3 snappedFwd, out float d) &&
                    d < 22f && KenyaNetworkData.IsClearOfAllRoundabouts(new Vector2(snappedPt.x, snappedPt.z), 12f))
                {
                    Vector3 dir = Vector3.Dot(snappedFwd, stageFwd) >= 0f ? snappedFwd : -snappedFwd;
                    Vector3 left = Vector3.Cross(dir, Vector3.up).normalized;
                    spawn = snappedPt + left * 3.5f;
                    rot = Quaternion.LookRotation(dir, Vector3.up);
                }
                else
                {
                    spawn = st.position - stageFwd * 20f;
                    rot = Quaternion.LookRotation(stageFwd, Vector3.up);
                }
            }
            else
            {
                Quaternion approxRot = Quaternion.Euler(0f, hub.roadYawDeg, 0f);
                Vector3 approxFwd = approxRot * Vector3.forward;
                Vector3 probePos = new Vector3(hub.xz.x, 0f, hub.xz.y) - approxFwd * 58f;
                Vector3 roadPt = probePos;
                Vector3 roadFwd = approxFwd;
                if (KenyaNetworkData.FindNearestRoadPoint(probePos, out Vector3 snappedPt, out Vector3 snappedFwd, out float d) && d < 28f)
                {
                    roadPt = snappedPt;
                    roadFwd = Vector3.Dot(snappedFwd, approxFwd) >= 0f ? snappedFwd : -snappedFwd;
                }
                Vector3 left = Vector3.Cross(roadFwd, Vector3.up).normalized;
                spawn = roadPt + left * 3.5f;
                rot = Quaternion.LookRotation(roadFwd, Vector3.up);
            }

            spawn.y = KenyaNetworkData.GetElevationAt(spawn.x, spawn.z) + 0.35f;
            ActiveVehicle.ResetToPose(spawn, rot);

            if (streetCamera != null)
            {
                streetCamera.SnapToTarget();
            }

            SetActiveStageByIndex(hubIndex);
            hud?.ShowToast($"Arrived at {hub.name} ({hub.corridorTag})");
        }

        public void SelectRoute(int routeIndex, bool moveToStartIfRunning = false)
        {
            int count = KenyaRouteCatalog.Routes.Length;
            ActiveRouteIndex = ((routeIndex % count) + count) % count;
            var route = ActiveRoute;

            // Keep mode + vehicle nicely aligned when browsing routes on the Title Screen
            if (State == RunState.Ready)
            {
                if (route.kind == KenyaRouteCatalog.RouteKind.FrrParcelCargo && ActiveMode != GameMode.IsuzuCargoHaul)
                {
                    SelectGameMode(GameMode.IsuzuCargoHaul, autoSelectRecommendedVehicle: true);
                }
                else if (route.kind == KenyaRouteCatalog.RouteKind.BusPassenger && ActiveMode == GameMode.IsuzuCargoHaul)
                {
                    SelectGameMode(GameMode.EnaCoachCareer, autoSelectRecommendedVehicle: true);
                }
            }

            CurrentRouteStopStep = 0;
            if (State == RunState.Running)
            {
                CompletedStageCount = 0;
                ResetStagesForNewRun();
                int firstHub = (route.stopHubIndices != null && route.stopHubIndices.Length > 0) ? route.stopHubIndices[0] : 0;
                if (moveToStartIfRunning)
                {
                    TeleportToTownHub(firstHub);
                }
                else
                {
                    SetActiveStageByIndex(firstHub);
                }
            }
            else
            {
                ResetStagesForNewRun();
            }

            OnRouteSelected?.Invoke(ActiveRouteIndex);
            KenyaStreetAudio.Instance?.PlayUiClick();
            if (State == RunState.Running)
            {
                hud?.ShowToast($"ROUTE: {route.shortName}  ({route.stopHubIndices.Length} STOPS  •  FOLLOW CYAN GPS)");
            }
        }

        public void SelectGameMode(GameMode mode, bool autoSelectRecommendedVehicle = true)
        {
            ActiveMode = mode;
            if (mode == GameMode.IsuzuCargoHaul && ActiveRoute.kind != KenyaRouteCatalog.RouteKind.FrrParcelCargo)
            {
                ActiveRouteIndex = 6; // PARCEL: NAIROBI – ELDORET – MALABA
                OnRouteSelected?.Invoke(ActiveRouteIndex);
            }
            else if (mode == GameMode.EnaCoachCareer && ActiveRoute.kind == KenyaRouteCatalog.RouteKind.FrrParcelCargo)
            {
                ActiveRouteIndex = 0; // NAIROBI – KISII – MIGORI
                OnRouteSelected?.Invoke(ActiveRouteIndex);
            }

            if (autoSelectRecommendedVehicle && fleet != null && fleet.Length > 0)
            {
                int recIdx = mode switch
                {
                    GameMode.EnaCoachCareer => 0,       // Scania Touring 2026 (ENA Coach)
                    GameMode.KenyaPolicePatrol => 1,    // Kenya Police Land Cruiser 79 (KCP 380D)
                    GameMode.IsuzuCargoHaul => 2,       // Kenyan Isuzu FRR
                    _ => ActiveVehicleIndex
                };
                if (recIdx < fleet.Length)
                {
                    ActivateVehicle(recIdx, resetPose: State == RunState.Ready);
                }
            }
            KenyaStreetAudio.Instance?.PlayUiClick();
        }

        public void StartRun()
        {
            if (State != RunState.Ready) return;

            TotalKesEarned = 0;
            PassengersBoarded = 0;
            CompletedStageCount = 0;
            CurrentRouteStopStep = 0;
            SafetyRating = 100;
            ElapsedRunSeconds = 0f;
            EndReason = string.Empty;

            var route = ActiveRoute;
            int firstHub = (route.stopHubIndices != null && route.stopHubIndices.Length > 0) ? route.stopHubIndices[0] : 0;
            CurrentStageIndex = firstHub;

            ResetStagesForNewRun();
            if (ActiveVehicle != null)
            {
                if (firstHub == 0)
                {
                    ActiveVehicle.ResetToPose(startPosition, Quaternion.identity);
                }
                else
                {
                    TeleportToTownHub(firstHub);
                }
                ApplyVehicleAudioProfile(ActiveVehicle);
                var policeSys = ActiveVehicle.GetComponent<KenyaPoliceEmergencySystem>();
                if (policeSys != null)
                {
                    policeSys.SetSirenActive(ActiveMode == GameMode.KenyaPolicePatrol, silent: true);
                }
            }
            streetCamera?.SnapToTarget();

            Time.timeScale = 1f;
            if (KenyaStreetAudio.Instance != null)
            {
                KenyaStreetAudio.Instance.SetPausedDuck(false);
                KenyaStreetAudio.Instance.PlayUiClick();
            }

            SetState(RunState.Running);
            hud?.ShowToast($"{ModeDisplayName}  •  {route.shortName}");
        }

        public void PauseRun()
        {
            if (State != RunState.Running) return;
            Time.timeScale = 0f;
            if (KenyaStreetAudio.Instance != null)
            {
                KenyaStreetAudio.Instance.SetPausedDuck(true);
                KenyaStreetAudio.Instance.PlayUiClick();
            }
            SetState(RunState.Paused);
        }

        public void ResumeRun()
        {
            if (State != RunState.Paused) return;
            Time.timeScale = 1f;
            if (KenyaStreetAudio.Instance != null)
            {
                KenyaStreetAudio.Instance.SetPausedDuck(false);
                KenyaStreetAudio.Instance.PlayUiClick();
            }
            SetState(RunState.Running);
        }

        public void SelectVehicleIndex(int index)
        {
            if (fleet == null || fleet.Length == 0) return;
            int clamped = ((index % fleet.Length) + fleet.Length) % fleet.Length;
            ActivateVehicle(clamped, resetPose: State == RunState.Ready);
            if (KenyaStreetAudio.Instance != null)
            {
                KenyaStreetAudio.Instance.PlayUiClick();
            }
        }

        public void SwitchVehicle()
        {
            if (fleet == null || fleet.Length < 2) return;
            int next = (ActiveVehicleIndex + 1) % fleet.Length;
            ActivateVehicle(next, resetPose: false);
            if (KenyaStreetAudio.Instance != null)
            {
                KenyaStreetAudio.Instance.PlayPickupBlip();
            }
            if (hud != null)
            {
                hud.ShowToast($"Switched to {ActiveVehicle.displayName}");
            }
        }

        private void ActivateVehicle(int index, bool resetPose)
        {
            if (fleet == null || fleet.Length == 0) return;

            Vector3 carryPos = startPosition;
            carryPos.y = Mathf.Max(carryPos.y, KenyaNetworkData.GetElevationAt(carryPos.x, carryPos.z) + 0.35f);
            Quaternion carryRot = Quaternion.identity;
            if (!resetPose && ActiveVehicle != null)
            {
                carryPos = ActiveVehicle.transform.position;
                carryPos.y = Mathf.Max(KenyaNetworkData.GetElevationAt(carryPos.x, carryPos.z) + 0.35f, carryPos.y);
                carryRot = Quaternion.Euler(0f, ActiveVehicle.transform.eulerAngles.y, 0f);
            }

            ActiveVehicleIndex = index;
            for (int i = 0; i < fleet.Length; i++)
            {
                if (fleet[i] == null) continue;
                bool active = (i == ActiveVehicleIndex);
                fleet[i].gameObject.SetActive(active);
                if (active)
                {
                    fleet[i].ResetToPose(carryPos, carryRot);
                }
            }

            if (streetCamera != null && ActiveVehicle != null)
            {
                streetCamera.SetTarget(ActiveVehicle);
            }
            if (ActiveVehicle != null)
            {
                ApplyVehicleAudioProfile(ActiveVehicle);
            }
        }

        private static void ApplyVehicleAudioProfile(KenyaStreetVehicle veh)
        {
            if (veh == null || KenyaStreetAudio.Instance == null) return;
            if (veh.plateNumber == "KCP 380D" || (veh.displayName != null && veh.displayName.IndexOf("Police", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                KenyaStreetAudio.Instance.SetVehicleAudioProfile(KenyaStreetAudio.EngineProfile.PoliceLandCruiserV8);
            }
            else if (veh.isCoach)
            {
                KenyaStreetAudio.Instance.SetVehicleAudioProfile(KenyaStreetAudio.EngineProfile.HeavyCoachDiesel);
            }
            else
            {
                KenyaStreetAudio.Instance.SetVehicleAudioProfile(KenyaStreetAudio.EngineProfile.IsuzuCargoDiesel);
            }
        }

        private void ResetStagesForNewRun()
        {
            if (stages == null) return;
            var route = ActiveRoute;
            int firstHub = (route.stopHubIndices != null && route.stopHubIndices.Length > 0) ? route.stopHubIndices[0] : 0;
            CurrentStageIndex = Mathf.Clamp(firstHub, 0, Mathf.Max(0, stages.Length - 1));
            for (int i = 0; i < stages.Length; i++)
            {
                if (stages[i] != null)
                {
                    stages[i].ResetStage(active: i == CurrentStageIndex);
                }
            }
        }

        public void OnStageCompleted(BusStageZone stage)
        {
            if (State != RunState.Running) return;

            CompletedStageCount++;
            TotalKesEarned += stage.rewardKes;
            // Police Patrol increments secured checkpoints (+1); Bus boards passengers (+N); Isuzu delivers parcels (+N)
            PassengersBoarded += (ActiveJobRole == KenyaStreetVehicle.VehicleJobRole.PolicePatrol) ? 1 : stage.passengerCount;

            if (KenyaStreetAudio.Instance != null)
            {
                KenyaStreetAudio.Instance.PlayPickupBlip();
                KenyaStreetAudio.Instance.PlayMilestoneSting();
            }

            OnStageBoarded?.Invoke(stage);

            // Advance along the player's SELECTED Kenyan Bus / FRR Parcel Route!
            var route = ActiveRoute;
            int nextIdx = -1;
            if (stages != null && route.stopHubIndices != null && route.stopHubIndices.Length > 0)
            {
                for (int s = 0; s < route.stopHubIndices.Length; s++)
                {
                    int hub = route.stopHubIndices[s];
                    if (hub >= 0 && hub < stages.Length && stages[hub] != null && !stages[hub].IsCompleted)
                    {
                        nextIdx = hub;
                        CurrentRouteStopStep = s;
                        break;
                    }
                }
            }

            if (nextIdx >= 0)
            {
                CurrentStageIndex = nextIdx;
                stages[CurrentStageIndex].SetStageState(active: true, completed: false);
            }
            else
            {
                TotalKesEarned += route.bonusKes;
                EndReason = $"{route.shortName} Complete! (+KES {route.bonusKes:N0} Route Bonus)";
                TriggerWin();
            }
        }

        public void RegisterSafetyPenalty(int amount, string reason, float shakeStrength = 0.6f)
        {
            if (State != RunState.Running) return;

            if (ActiveMode == GameMode.FreeRoamKenya)
            {
                // Free Roam mode keeps impact feedback without ending your cruise!
                KenyaStreetAudio.Instance?.PlayBump(shakeStrength * 0.7f);
                streetCamera?.TriggerImpactShake(shakeStrength * 0.55f);
                return;
            }

            SafetyRating = Mathf.Max(0, SafetyRating - amount);

            if (KenyaStreetAudio.Instance != null)
            {
                KenyaStreetAudio.Instance.PlayBump(shakeStrength);
            }
            if (streetCamera != null)
            {
                streetCamera.TriggerImpactShake(shakeStrength);
            }

            OnSafetyPenalty?.Invoke(amount, reason);

            if (SafetyRating <= 0)
            {
                TriggerLose(reason);
            }
        }

        public void TriggerWin()
        {
            if (State == RunState.Won) return;
            if (string.IsNullOrEmpty(EndReason)) EndReason = "All Kenyan Route Stops Completed!";
            if (KenyaStreetAudio.Instance != null)
            {
                KenyaStreetAudio.Instance.PlayMilestoneSting();
            }
            SetState(RunState.Won);
        }

        public void TriggerLose(string reason)
        {
            if (State == RunState.Lost) return;
            EndReason = string.IsNullOrEmpty(reason) ? "Safety Rating Depleted!" : reason;
            SetState(RunState.Lost);
        }

        public void RestartRun()
        {
            Time.timeScale = 1f;
            if (KenyaStreetAudio.Instance != null)
            {
                KenyaStreetAudio.Instance.SetPausedDuck(false);
                KenyaStreetAudio.Instance.PlayUiClick();
            }
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        public void ReturnToTitle()
        {
            Time.timeScale = 1f;
            if (KenyaStreetAudio.Instance != null)
            {
                KenyaStreetAudio.Instance.SetPausedDuck(false);
                KenyaStreetAudio.Instance.PlayUiClick();
            }
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        public void QuitGame()
        {
            if (KenyaStreetAudio.Instance != null)
            {
                KenyaStreetAudio.Instance.PlayUiClick();
            }
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void SetState(RunState next)
        {
            State = next;
            OnStateChanged?.Invoke(next);
            if (hud != null)
            {
                hud.ApplyRunState(next);
            }
        }

        private static bool WasEscapePressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.Escape);
#endif
        }

        private static bool WasSwitchVehiclePressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.vKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.V);
#endif
        }

        private static bool WasAnyStartKeyPressed()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb == null) return false;
            return kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.W);
#endif
        }
    }
}
