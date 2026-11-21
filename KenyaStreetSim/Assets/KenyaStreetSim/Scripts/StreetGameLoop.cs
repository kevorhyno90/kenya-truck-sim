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

        public static StreetGameLoop Instance { get; private set; }

        [Header("Fleet & Stages")]
        public KenyaStreetVehicle[] fleet;
        public BusStageZone[] stages;
        public KenyaStreetCamera streetCamera;
        public KenyaStreetHUD hud;
        public KenyaRouteMapUI routeMap;
        public Vector3 startPosition = new Vector3(-3.5f, 0.28f, -18f);

        public RunState State { get; private set; } = RunState.Ready;
        public int ActiveVehicleIndex { get; private set; }
        public KenyaStreetVehicle ActiveVehicle =>
            (fleet != null && fleet.Length > 0) ? fleet[Mathf.Clamp(ActiveVehicleIndex, 0, fleet.Length - 1)] : null;

        public int CurrentStageIndex { get; private set; }
        public int CompletedStageCount { get; private set; }
        public int TotalKesEarned { get; private set; }
        public int PassengersBoarded { get; private set; }
        public int SafetyRating { get; private set; } = 100;
        public float ElapsedRunSeconds { get; private set; }
        public string EndReason { get; private set; } = string.Empty;

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
            Quaternion approxRot = Quaternion.Euler(0f, hub.roadYawDeg, 0f);
            Vector3 approxFwd = approxRot * Vector3.forward;

            // Snap cleanly onto the actual curved tarmac highway 30m ahead of the selected town's ENA Coach Terminal
            Vector3 probePos = new Vector3(hub.xz.x, 0f, hub.xz.y) - approxFwd * 30f;
            Vector3 roadPt = probePos;
            Vector3 roadFwd = approxFwd;
            if (KenyaNetworkData.FindNearestRoadPoint(probePos, out Vector3 snappedPt, out Vector3 snappedFwd, out float d) && d < 28f)
            {
                roadPt = snappedPt;
                roadFwd = Vector3.Dot(snappedFwd, approxFwd) >= 0f ? snappedFwd : -snappedFwd;
            }

            Vector3 left = Vector3.Cross(roadFwd, Vector3.up).normalized;
            Vector3 spawn = roadPt + left * 3.4f + Vector3.up * 0.32f;
            Quaternion rot = Quaternion.LookRotation(roadFwd, Vector3.up);

            ActiveVehicle.ResetToPose(spawn, rot);

            if (streetCamera != null)
            {
                streetCamera.SnapToTarget();
            }

            SetActiveStageByIndex(hubIndex);
            hud?.ShowToast($"Arrived at {hub.name} ({hub.corridorTag})");
        }

        public void StartRun()
        {
            if (State != RunState.Ready) return;

            TotalKesEarned = 0;
            PassengersBoarded = 0;
            CompletedStageCount = 0;
            CurrentStageIndex = 0;
            SafetyRating = 100;
            ElapsedRunSeconds = 0f;
            EndReason = string.Empty;

            ResetStagesForNewRun();
            if (ActiveVehicle != null)
            {
                ActiveVehicle.ResetToPose(startPosition, Quaternion.identity);
            }

            Time.timeScale = 1f;
            if (KenyaStreetAudio.Instance != null)
            {
                KenyaStreetAudio.Instance.SetPausedDuck(false);
                KenyaStreetAudio.Instance.PlayUiClick();
            }

            SetState(RunState.Running);
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
            Quaternion carryRot = Quaternion.identity;
            if (!resetPose && ActiveVehicle != null)
            {
                carryPos = ActiveVehicle.transform.position;
                carryPos.y = Mathf.Max(0.28f, carryPos.y);
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
        }

        private void ResetStagesForNewRun()
        {
            if (stages == null) return;
            for (int i = 0; i < stages.Length; i++)
            {
                if (stages[i] != null)
                {
                    stages[i].ResetStage(active: i == 0);
                }
            }
        }

        public void OnStageCompleted(BusStageZone stage)
        {
            if (State != RunState.Running) return;

            CompletedStageCount++;
            TotalKesEarned += stage.rewardKes;
            PassengersBoarded += stage.passengerCount;

            if (KenyaStreetAudio.Instance != null)
            {
                KenyaStreetAudio.Instance.PlayPickupBlip();
                KenyaStreetAudio.Instance.PlayMilestoneSting();
            }

            OnStageBoarded?.Invoke(stage);

            // Advance to next uncompleted ENA Coach town stage across Kenya!
            int nextIdx = -1;
            if (stages != null)
            {
                for (int step = 1; step <= stages.Length; step++)
                {
                    int cand = (CurrentStageIndex + step) % stages.Length;
                    if (stages[cand] != null && !stages[cand].IsCompleted)
                    {
                        nextIdx = cand;
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
                TriggerWin();
            }
        }

        public void RegisterSafetyPenalty(int amount, string reason, float shakeStrength = 0.6f)
        {
            if (State != RunState.Running) return;

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
            EndReason = "All Kenyan Town Bus Stages Completed!";
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
