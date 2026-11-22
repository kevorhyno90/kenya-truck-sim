using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace KenyaStreetSim
{
    /// <summary>
    /// ETS2 / RK 254 KE 4-mode camera rig:
    /// 1) Chase Orbit (cinematic follow + RMB orbit/zoom)
    /// 2) Realistic RHD Driver Cockpit (inside cab looking past steering wheel, A-pillars & mirrors)
    /// 3) Bumper / Dashcam (classic RK 254 KE highway YouTube driving perspective)
    /// 4) Axle / Wheel Street Cam
    /// </summary>
    public class KenyaStreetCamera : MonoBehaviour
    {
        public enum CameraMode { ChaseOrbit, CockpitRHD, DashcamBumper, AxleStreet }

        [Header("Chase Rig")]
        public float chaseDistance = 14.6f;
        public float chaseHeight = 4.25f;
        public float chaseLookHeight = 2.15f;
        public float positionSmooth = 8.0f;
        public float rotationSmooth = 9.0f;

        [Header("Title Backdrop Orbit")]
        public float titleOrbitRadius = 14.5f;
        public float titleOrbitHeight = 3.9f;
        public float titleOrbitSpeedDeg = 4.2f;

        public CameraMode CurrentMode { get; private set; } = CameraMode.ChaseOrbit;

        private Camera cam;
        private KenyaStreetVehicle targetVehicle;
        private float orbitYawOffset;
        private float orbitPitchOffset = 5f;
        private float titleAnglePhase;
        private float shakeMagnitude;
        private float shakeTimer;

        private void Awake()
        {
            cam = GetComponent<Camera>();
        }

        public void SetTarget(KenyaStreetVehicle vehicle)
        {
            targetVehicle = vehicle;
            if (vehicle != null)
            {
                if (vehicle.isCoach)
                {
                    chaseDistance = 14.6f;
                    chaseHeight = 4.25f;
                    chaseLookHeight = 2.15f;
                }
                else if (vehicle.plateNumber == "KCP 380D" || (vehicle.displayName != null && vehicle.displayName.IndexOf("Police", System.StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    chaseDistance = 8.4f;
                    chaseHeight = 2.65f;
                    chaseLookHeight = 1.40f;
                }
                else
                {
                    chaseDistance = 11.2f;
                    chaseHeight = 3.45f;
                    chaseLookHeight = 1.80f;
                }
            }
        }

        public void CycleMode()
        {
            CurrentMode = (CameraMode)(((int)CurrentMode + 1) % 4);
            orbitYawOffset = 0f;
            KenyaStreetAudio.Instance?.PlayUiClick();
            if (StreetGameLoop.Instance != null && StreetGameLoop.Instance.hud != null)
            {
                string label = CurrentMode switch
                {
                    CameraMode.ChaseOrbit => "Chase Orbit Camera",
                    CameraMode.CockpitRHD => "RHD Driver Cockpit (ETS2 View)",
                    CameraMode.DashcamBumper => "Highway Dashcam View",
                    _ => "Wheel & Street View"
                };
                StreetGameLoop.Instance.hud.ShowToast($"Camera: {label}");
            }
        }

        public void SetMode(CameraMode mode)
        {
            CurrentMode = mode;
            orbitYawOffset = 0f;
        }

        public void SnapToTarget()
        {
            if (targetVehicle == null && StreetGameLoop.Instance != null)
            {
                targetVehicle = StreetGameLoop.Instance.ActiveVehicle;
            }
            if (targetVehicle == null) return;

            orbitYawOffset = 0f;
            switch (CurrentMode)
            {
                case CameraMode.ChaseOrbit:
                    Transform t = targetVehicle.transform;
                    Quaternion orbitRot = Quaternion.Euler(orbitPitchOffset, t.eulerAngles.y, 0f);
                    Vector3 desiredPos = t.position + Vector3.up * chaseHeight - (orbitRot * Vector3.forward) * chaseDistance;
                    desiredPos.y = Mathf.Max(1.4f, desiredPos.y);
                    transform.position = desiredPos;
                    Vector3 lookTarget = t.position + Vector3.up * chaseLookHeight + t.forward * 5.0f;
                    transform.rotation = Quaternion.LookRotation(lookTarget - transform.position, Vector3.up);
                    if (cam != null) cam.fieldOfView = 55f;
                    break;
                case CameraMode.CockpitRHD:
                    UpdateAnchorCamera(targetVehicle.cockpitCameraAnchor, 85f);
                    break;
                case CameraMode.DashcamBumper:
                    UpdateAnchorCamera(targetVehicle.bumperCameraAnchor, 64f);
                    break;
                case CameraMode.AxleStreet:
                    UpdateAnchorCamera(targetVehicle.axleCameraAnchor, 56f);
                    break;
            }
        }

        public void TriggerImpactShake(float intensity)
        {
            shakeMagnitude = Mathf.Max(shakeMagnitude, Mathf.Clamp(intensity * 0.45f, 0.12f, 0.55f));
            shakeTimer = 0.28f;
        }

        private void LateUpdate()
        {
            if (targetVehicle == null && StreetGameLoop.Instance != null)
            {
                targetVehicle = StreetGameLoop.Instance.ActiveVehicle;
            }
            if (targetVehicle == null) return;

            var state = StreetGameLoop.Instance != null
                ? StreetGameLoop.Instance.State
                : StreetGameLoop.RunState.Running;

            if (state == StreetGameLoop.RunState.Ready)
            {
                UpdateTitleOrbit();
                return;
            }

            if (state == StreetGameLoop.RunState.Running)
            {
                HandleCameraHotkeysAndOrbit();
            }

            switch (CurrentMode)
            {
                case CameraMode.ChaseOrbit:
                    UpdateChaseCamera();
                    break;
                case CameraMode.CockpitRHD:
                    UpdateAnchorCamera(targetVehicle.cockpitCameraAnchor, 85f);
                    break;
                case CameraMode.DashcamBumper:
                    UpdateAnchorCamera(targetVehicle.bumperCameraAnchor, 64f);
                    break;
                case CameraMode.AxleStreet:
                    UpdateAnchorCamera(targetVehicle.axleCameraAnchor, 56f);
                    break;
            }

            ApplyShake();
        }

        private void UpdateTitleOrbit()
        {
            // Cinematic hero sweep looking NORTH (+Z) down the sunlit Kenyan town boulevard & Rift Valley hills
            titleAnglePhase += (titleOrbitSpeedDeg * Mathf.Deg2Rad) * Time.unscaledDeltaTime;
            float yawDeg = 152f + Mathf.Sin(titleAnglePhase) * 20f;
            float rad = yawDeg * Mathf.Deg2Rad;
            Vector3 pivot = targetVehicle.transform.position + Vector3.up * 2.1f + targetVehicle.transform.forward * 2.4f;
            Vector3 offset = new Vector3(Mathf.Sin(rad) * titleOrbitRadius, titleOrbitHeight, Mathf.Cos(rad) * titleOrbitRadius);
            transform.position = pivot + offset;
            transform.LookAt(targetVehicle.transform.position + new Vector3(1.8f, 2.6f, 28f));
            if (cam != null) cam.fieldOfView = 52f;
        }

        private void HandleCameraHotkeysAndOrbit()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null && kb.cKey.wasPressedThisFrame)
            {
                CycleMode();
            }
            var mouse = Mouse.current;
            if (mouse != null && CurrentMode == CameraMode.ChaseOrbit)
            {
                if (mouse.rightButton.isPressed)
                {
                    Vector2 delta = mouse.delta.ReadValue();
                    orbitYawOffset += delta.x * 0.28f;
                    orbitPitchOffset = Mathf.Clamp(orbitPitchOffset - delta.y * 0.18f, -4f, 34f);
                }
                else
                {
                    orbitYawOffset = Mathf.Lerp(orbitYawOffset, 0f, Time.deltaTime * 2.0f);
                }

                float scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f)
                {
                    chaseDistance = Mathf.Clamp(chaseDistance - scroll * 0.015f, 8.2f, 20f);
                }
            }
#else
            if (Input.GetKeyDown(KeyCode.C)) CycleMode();
            if (CurrentMode == CameraMode.ChaseOrbit && Input.GetMouseButton(1))
            {
                orbitYawOffset += Input.GetAxis("Mouse X") * 3f;
                orbitPitchOffset = Mathf.Clamp(orbitPitchOffset - Input.GetAxis("Mouse Y") * 2f, -4f, 34f);
            }
#endif
        }

        private void UpdateChaseCamera()
        {
            Transform t = targetVehicle.transform;
            float baseYaw = t.eulerAngles.y + orbitYawOffset;
            Quaternion orbitRot = Quaternion.Euler(orbitPitchOffset, baseYaw, 0f);
            Vector3 desiredPos = t.position + Vector3.up * chaseHeight - (orbitRot * Vector3.forward) * chaseDistance;
            desiredPos.y = Mathf.Max(1.4f, desiredPos.y);

            transform.position = Vector3.Lerp(transform.position, desiredPos, 1f - Mathf.Exp(-positionSmooth * Time.deltaTime));

            Vector3 lookTarget = t.position + Vector3.up * chaseLookHeight + t.forward * 5.0f;
            Quaternion desiredLook = Quaternion.LookRotation(lookTarget - transform.position, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredLook, 1f - Mathf.Exp(-rotationSmooth * Time.deltaTime));

            if (cam != null)
            {
                float targetFov = Mathf.Lerp(54f, 64f, Mathf.Clamp01(targetVehicle.SpeedKmh / 78f));
                cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFov, Time.deltaTime * 5f);
            }
        }

        private void UpdateAnchorCamera(Transform anchor, float fov)
        {
            if (anchor == null)
            {
                UpdateChaseCamera();
                return;
            }
            transform.position = anchor.position;
            transform.rotation = anchor.rotation;
            if (cam != null) cam.fieldOfView = fov;
        }

        private void ApplyShake()
        {
            if (shakeTimer <= 0f) return;
            shakeTimer -= Time.unscaledDeltaTime;
            float damp = Mathf.Clamp01(shakeTimer / 0.28f);
            Vector3 jitter = Random.insideUnitSphere * (shakeMagnitude * damp);
            transform.position += jitter;
            if (shakeTimer <= 0f) shakeMagnitude = 0f;
        }
    }
}
