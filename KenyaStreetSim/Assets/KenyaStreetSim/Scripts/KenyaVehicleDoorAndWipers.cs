using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace KenyaStreetSim
{
    /// <summary>
    /// Controls realistic animated Vehicle Passenger Door ([O] key or auto-open at Bus Stages/Parcel Depots),
    /// animated dual 3D Windshield Wipers ([K] key or auto-on during Kenyan Rain),
    /// and live sweeping Analog Dashboard Speedometer & RPM Needles!
    /// </summary>
    public class KenyaVehicleDoorAndWipers : MonoBehaviour
    {
        [Header("Animated Left-Side Passenger Door")]
        public Transform passengerDoorPivot;
        public Vector3 doorClosedLocalPos;
        public Vector3 doorOpenLocalPos;
        public Vector3 doorClosedLocalEuler;
        public Vector3 doorOpenLocalEuler;
        public Light doorStepLight;
        public Transform boardingDoorPoint;

        [Header("Animated Dual 3D Windshield Wipers")]
        public Transform wiperPivotL;
        public Transform wiperPivotR;
        public float wiperSweepDeg = 46f;

        [Header("Live 3D Analog Dashboard Needles")]
        public Transform speedNeedlePivot;
        public Transform rpmNeedlePivot;

        public bool DoorOpen { get; private set; }
        public bool WipersOn { get; private set; }

        private KenyaStreetVehicle vehicle;
        private float doorAnim01;
        private float wiperPhase;
        private float lastSwishPhase;
        private bool autoOpenedAtStage;

        private void Awake()
        {
            vehicle = GetComponent<KenyaStreetVehicle>();
            if (passengerDoorPivot != null && doorClosedLocalPos == Vector3.zero && doorOpenLocalPos == Vector3.zero)
            {
                doorClosedLocalPos = passengerDoorPivot.localPosition;
                doorOpenLocalPos = doorClosedLocalPos + new Vector3(-0.24f, 0f, -0.78f);
            }
            if (doorStepLight != null) doorStepLight.enabled = false;
        }

        public void SetDoorOpen(bool open, bool silent = false)
        {
            if (DoorOpen == open) return;
            DoorOpen = open;
            if (!silent)
            {
                KenyaStreetAudio.Instance?.PlayDoorPneumatic(open);
                if (StreetGameLoop.Instance != null && StreetGameLoop.Instance.ActiveVehicle == vehicle && StreetGameLoop.Instance.hud != null)
                {
                    StreetGameLoop.Instance.hud.ShowToast(open ? "PASSENGER DOOR: OPEN [O] — BOARDING READY" : "PASSENGER DOOR: CLOSED [O]");
                }
            }
        }

        public void SetWipers(bool on, bool silent = false)
        {
            if (WipersOn == on) return;
            WipersOn = on;
            if (!silent && StreetGameLoop.Instance != null && StreetGameLoop.Instance.ActiveVehicle == vehicle && StreetGameLoop.Instance.hud != null)
            {
                StreetGameLoop.Instance.hud.ShowToast(on ? "WINDSHIELD WIPERS: ON [K]" : "WINDSHIELD WIPERS: OFF [K]");
            }
        }

        public Vector3 GetDoorWorldPoint()
        {
            if (boardingDoorPoint != null) return boardingDoorPoint.position;
            if (passengerDoorPivot != null) return passengerDoorPivot.position;
            return transform.position - transform.right * 1.4f + transform.forward * 2.4f;
        }

        private void Update()
        {
            var loop = StreetGameLoop.Instance;
            bool isActive = loop != null && loop.ActiveVehicle == vehicle && loop.State == StreetGameLoop.RunState.Running;

            if (isActive)
            {
                if (WasKeyPressed(KeyCode.O))
                {
                    SetDoorOpen(!DoorOpen);
                    autoOpenedAtStage = false;
                }
                if (WasKeyPressed(KeyCode.K))
                {
                    SetWipers(!WipersOn);
                }

                // Auto-open passenger door when stopped inside an active Bus Stage / Parcel Depot so 3D passengers walk right in!
                var stage = loop.ActiveStage;
                bool stoppedAtStage = stage != null && stage.PlayerInside && vehicle != null && vehicle.SpeedKmh < 8f;
                if (stoppedAtStage && !DoorOpen)
                {
                    SetDoorOpen(true, silent: false);
                    autoOpenedAtStage = true;
                }
                else if (autoOpenedAtStage && (!stoppedAtStage || (vehicle != null && vehicle.SpeedKmh > 14f)))
                {
                    SetDoorOpen(false, silent: false);
                    autoOpenedAtStage = false;
                }

                // Auto-enable wipers when Kenyan Rain starts!
                if (KenyaWeatherSystem.Instance != null && KenyaWeatherSystem.Instance.RainIntensity > 0.25f && !WipersOn)
                {
                    SetWipers(true, silent: true);
                }
            }

            // Smooth pneumatic passenger door slide/swing animation
            float targetDoor = DoorOpen ? 1f : 0f;
            doorAnim01 = Mathf.MoveTowards(doorAnim01, targetDoor, Time.deltaTime * 1.85f);
            if (passengerDoorPivot != null)
            {
                passengerDoorPivot.localPosition = Vector3.Lerp(doorClosedLocalPos, doorOpenLocalPos, SmoothStep(doorAnim01));
                passengerDoorPivot.localRotation = Quaternion.Euler(Vector3.Lerp(doorClosedLocalEuler, doorOpenLocalEuler, SmoothStep(doorAnim01)));
            }
            if (doorStepLight != null)
            {
                doorStepLight.enabled = doorAnim01 > 0.15f;
            }

            // Smooth dual 3D windshield wiper sweep animation
            if (WipersOn)
            {
                wiperPhase += Time.deltaTime * 3.8f;
                float sweep = (0.5f - 0.5f * Mathf.Cos(wiperPhase)) * wiperSweepDeg;
                ApplyWiperAngles(sweep);

                if (isActive && Mathf.Floor(wiperPhase / Mathf.PI) != Mathf.Floor(lastSwishPhase / Mathf.PI))
                {
                    KenyaStreetAudio.Instance?.PlayWiperSwish();
                }
                lastSwishPhase = wiperPhase;
            }
            else if (wiperPhase > 0.01f)
            {
                wiperPhase = Mathf.MoveTowards(wiperPhase, 0f, Time.deltaTime * 6f);
                ApplyWiperAngles(0f);
            }

            // Animate real 3D Analog Speedometer & Tachometer Red Needles on Dashboard!
            if (vehicle != null)
            {
                if (speedNeedlePivot != null)
                {
                    float speedNorm = Mathf.Clamp01(vehicle.SpeedKmh / 140f);
                    float ang = Mathf.Lerp(120f, -120f, speedNorm);
                    speedNeedlePivot.localRotation = Quaternion.Euler(0f, 0f, ang);
                }
                if (rpmNeedlePivot != null)
                {
                    float rpmNorm = Mathf.Clamp01(vehicle.RpmNormalized);
                    float ang = Mathf.Lerp(120f, -120f, rpmNorm);
                    rpmNeedlePivot.localRotation = Quaternion.Euler(0f, 0f, ang);
                }
            }
        }

        private void ApplyWiperAngles(float sweepDeg)
        {
            if (wiperPivotL != null) wiperPivotL.localRotation = Quaternion.Euler(0f, 0f, -18f + sweepDeg);
            if (wiperPivotR != null) wiperPivotR.localRotation = Quaternion.Euler(0f, 0f, -18f + sweepDeg);
        }

        private static float SmoothStep(float t) => t * t * (3f - 2f * t);

        private static bool WasKeyPressed(KeyCode key)
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb == null) return false;
            return key == KeyCode.O ? kb.oKey.wasPressedThisFrame : kb.kKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(key);
#endif
        }
    }
}
