using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace KenyaStreetSim
{
    /// <summary>
    /// Smooth 3-mode camera rig (Chase Orbit, Kenyan RHD Cockpit, Axle/Street View)
    /// plus a slow 5 deg/s Title backdrop orbit and unscaled impact shake.
    /// </summary>
    public class KenyaStreetCamera : MonoBehaviour
    {
        public enum CameraMode { ChaseOrbit, CockpitRHD, AxleStreet }

        [Header("Chase Rig")]
        public float chaseDistance = 12.8f;
        public float chaseHeight = 4.6f;
        public float chaseLookHeight = 2.0f;
        public float positionSmooth = 7.5f;
        public float rotationSmooth = 8.5f;

        [Header("Title Backdrop Orbit")]
        public float titleOrbitRadius = 14.5f;
        public float titleOrbitHeight = 4.2f;
        public float titleOrbitSpeedDeg = 4.5f;

        public CameraMode CurrentMode { get; private set; } = CameraMode.ChaseOrbit;

        private Camera cam;
        private KenyaStreetVehicle targetVehicle;
        private float orbitYawOffset;
        private float orbitPitchOffset = 8f;
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
        }

        public void CycleMode()
        {
            CurrentMode = (CameraMode)(((int)CurrentMode + 1) % 3);
            orbitYawOffset = 0f;
            if (KenyaStreetAudio.Instance != null)
            {
                KenyaStreetAudio.Instance.PlayUiClick();
            }
        }

        public void SetMode(CameraMode mode)
        {
            CurrentMode = mode;
            orbitYawOffset = 0f;
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
                    UpdateAnchorCamera(targetVehicle.cockpitCameraAnchor, 68f);
                    break;
                case CameraMode.AxleStreet:
                    UpdateAnchorCamera(targetVehicle.axleCameraAnchor, 58f);
                    break;
            }

            ApplyShake();
        }

        private void UpdateTitleOrbit()
        {
            // Gently sweep behind-quarter of the parked ENA Coach looking NORTH (+Z) down the sunlit bustling avenue
            titleAnglePhase += (titleOrbitSpeedDeg * Mathf.Deg2Rad) * Time.unscaledDeltaTime;
            float yawDeg = 158f + Mathf.Sin(titleAnglePhase) * 22f; // 136..180 deg = behind-right/behind-left looking +Z
            float rad = yawDeg * Mathf.Deg2Rad;
            Vector3 pivot = targetVehicle.transform.position + Vector3.up * 2.1f + targetVehicle.transform.forward * 2.5f;
            Vector3 offset = new Vector3(Mathf.Sin(rad) * titleOrbitRadius, titleOrbitHeight, Mathf.Cos(rad) * titleOrbitRadius);
            transform.position = pivot + offset;
            // Aim slightly ahead & right so bus sits cleanly in the lower-left third while the avenue stretches ahead
            transform.LookAt(targetVehicle.transform.position + new Vector3(1.8f, 2.5f, 24f));
            if (cam != null) cam.fieldOfView = 54f;
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
                    chaseDistance = Mathf.Clamp(chaseDistance - scroll * 0.015f, 8.5f, 20f);
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

            Vector3 lookTarget = t.position + Vector3.up * chaseLookHeight + t.forward * 4.0f;
            Quaternion desiredLook = Quaternion.LookRotation(lookTarget - transform.position, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredLook, 1f - Mathf.Exp(-rotationSmooth * Time.deltaTime));

            if (cam != null)
            {
                float targetFov = Mathf.Lerp(56f, 65f, Mathf.Clamp01(targetVehicle.SpeedKmh / 75f));
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
