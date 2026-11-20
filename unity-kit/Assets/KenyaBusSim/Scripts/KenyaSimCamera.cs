using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace KenyaBusSim
{
    public enum CameraViewMode
    {
        ChaseOrbit = 0,
        RightHandDriveCockpit = 1,
        AxleWheelView = 2
    }

    /// <summary>
    /// ETS2-style camera controller supporting Chase Cam (with right-click orbit & zoom),
    /// Kenyan Right-Hand Drive Cockpit Cam (with head look), and Wheel/Axle Inspection Cam.
    /// </summary>
    public class KenyaSimCamera : MonoBehaviour
    {
        public KenyaVehicleController targetVehicle;
        public CameraViewMode viewMode = CameraViewMode.ChaseOrbit;

        [Header("Chase Camera Tuning")]
        public float busChaseDistance = 17.5f;
        public float truckChaseDistance = 13.5f;
        public float chaseHeight = 5.2f;
        public float chaseFollowSmooth = 6.5f;
        public float orbitSensitivity = 0.28f;

        private float orbitYaw;
        private float orbitPitch = 14f;
        private float zoomOffset;
        private float cockpitLookYaw;
        private float cockpitLookPitch;

        public string CurrentModeLabel
        {
            get
            {
                switch (viewMode)
                {
                    case CameraViewMode.RightHandDriveCockpit: return "RHD Cockpit";
                    case CameraViewMode.AxleWheelView: return "Axle / Wheel Cam";
                    default: return "ETS2 Chase (RMB Orbit)";
                }
            }
        }

        public void SetTarget(KenyaVehicleController vehicle)
        {
            targetVehicle = vehicle;
            orbitYaw = 0f;
            cockpitLookYaw = 0f;
            cockpitLookPitch = 0f;
        }

        public void CycleViewMode()
        {
            viewMode = (CameraViewMode)(((int)viewMode + 1) % 3);
            cockpitLookYaw = 0f;
            cockpitLookPitch = 0f;
        }

        private void Update()
        {
            bool cyclePressed = false;
            bool rightMouseHeld = false;
            Vector2 mouseDelta = Vector2.zero;
            float scrollDelta = 0f;

#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null && kb.cKey.wasPressedThisFrame) cyclePressed = true;

            var mouse = Mouse.current;
            if (mouse != null)
            {
                rightMouseHeld = mouse.rightButton.isPressed;
                mouseDelta = mouse.delta.ReadValue();
                scrollDelta = mouse.scroll.ReadValue().y * 0.01f;
            }
#else
            if (Input.GetKeyDown(KeyCode.C)) cyclePressed = true;
            rightMouseHeld = Input.GetMouseButton(1);
            mouseDelta = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")) * 12f;
            scrollDelta = Input.mouseScrollDelta.y;
#endif

            if (cyclePressed)
            {
                CycleViewMode();
            }

            if (viewMode == CameraViewMode.ChaseOrbit)
            {
                if (rightMouseHeld)
                {
                    orbitYaw += mouseDelta.x * orbitSensitivity;
                    orbitPitch = Mathf.Clamp(orbitPitch - mouseDelta.y * orbitSensitivity, 2f, 55f);
                }
                else if (targetVehicle != null && targetVehicle.SpeedKmh > 6f)
                {
                    // Gently auto-center behind vehicle while driving forward
                    orbitYaw = Mathf.Lerp(orbitYaw, 0f, Time.deltaTime * 1.8f);
                }

                zoomOffset = Mathf.Clamp(zoomOffset - scrollDelta * 1.4f, -5f, 10f);
            }
            else if (viewMode == CameraViewMode.RightHandDriveCockpit)
            {
                if (rightMouseHeld)
                {
                    cockpitLookYaw = Mathf.Clamp(cockpitLookYaw + mouseDelta.x * orbitSensitivity, -85f, 85f);
                    cockpitLookPitch = Mathf.Clamp(cockpitLookPitch - mouseDelta.y * orbitSensitivity, -25f, 25f);
                }
                else
                {
                    cockpitLookYaw = Mathf.Lerp(cockpitLookYaw, 0f, Time.deltaTime * 4f);
                    cockpitLookPitch = Mathf.Lerp(cockpitLookPitch, 0f, Time.deltaTime * 4f);
                }
            }
        }

        private void LateUpdate()
        {
            if (targetVehicle == null) return;

            if (viewMode == CameraViewMode.RightHandDriveCockpit && targetVehicle.cockpitCameraAnchor != null)
            {
                transform.position = targetVehicle.cockpitCameraAnchor.position;
                Quaternion lookOffset = Quaternion.Euler(cockpitLookPitch, cockpitLookYaw, 0f);
                transform.rotation = targetVehicle.cockpitCameraAnchor.rotation * lookOffset;
                return;
            }

            if (viewMode == CameraViewMode.AxleWheelView && targetVehicle.wheelCamAnchor != null)
            {
                transform.position = Vector3.Lerp(transform.position, targetVehicle.wheelCamAnchor.position, Time.deltaTime * 10f);
                Vector3 focus = targetVehicle.transform.position + targetVehicle.transform.up * 1.2f + targetVehicle.transform.forward * 1.5f;
                transform.LookAt(focus);
                return;
            }

            // ETS2 Chase / Orbit Cam
            Transform pivot = targetVehicle.chaseCameraTarget != null ? targetVehicle.chaseCameraTarget : targetVehicle.transform;
            float baseDist = (targetVehicle.vehicleClass == KenyanVehicleClass.EnaCoachBus ? busChaseDistance : truckChaseDistance) + zoomOffset;
            float yaw = targetVehicle.transform.eulerAngles.y + orbitYaw;

            Quaternion rot = Quaternion.Euler(orbitPitch, yaw, 0f);
            Vector3 desiredPos = pivot.position + rot * new Vector3(0f, 0f, -baseDist) + Vector3.up * (chaseHeight * 0.25f);
            desiredPos.y = Mathf.Max(desiredPos.y, targetVehicle.transform.position.y + 1.2f);

            transform.position = Vector3.Lerp(transform.position, desiredPos, Time.deltaTime * chaseFollowSmooth);
            transform.LookAt(pivot.position + Vector3.up * 1.5f);
        }
    }
}
