using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace KenyaStreetSim
{
    /// <summary>
    /// Tuned Unity 6 WheelCollider controller for Kenyan commercial vehicles:
    /// ENA Coach KDK 057M (8-wheel twin-steer coach) and EPH Isuzu FRR 90 (6-wheel dual-rear lorry).
    /// Features smooth street steering, automatic 6-speed gearbox, anti-roll bars, RHD rotating steering wheel,
    /// New Input System support, and zero speed-bump hacks.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class KenyaStreetVehicle : MonoBehaviour
    {
        [System.Serializable]
        public class WheelEntry
        {
            public WheelCollider collider;
            public Transform visualMesh;
            public bool isSteer;
            public bool isDrive;
            public bool isLeft;
        }

        [Header("Vehicle Identity")]
        public string displayName = "ENA Coach KDK 057M";
        public string plateNumber = "KDK 057M";
        public bool isCoach = true;

        [Header("Powertrain & Handling")]
        public float motorTorque = 2400f;
        public float reverseTorque = 1600f;
        public float brakeTorque = 4800f;
        public float handbrakeTorque = 7500f;
        public float maxSteerAngle = 32f;
        public float highSpeedSteerAngle = 12f;
        public float maxSpeedKmh = 78f;
        public float antiRollStiffness = 8500f;
        public Vector3 centerOfMassOffset = new Vector3(0f, -0.55f, 0.1f);

        [Header("References")]
        public WheelEntry[] wheels;
        public Transform cockpitCameraAnchor;
        public Transform axleCameraAnchor;
        public Transform steeringWheelVisual;
        public Renderer[] brakeLightRenderers;

        public Rigidbody Rb { get; private set; }
        public float SpeedKmh { get; private set; }
        public float ForwardSpeedMs { get; private set; }
        public string GearLabel { get; private set; } = "N";
        public float ThrottleInput { get; private set; }
        public float SteerInput { get; private set; }
        public bool IsBraking { get; private set; }

        private float currentSteerAngle;
        private float lastCollisionTime = -10f;
        private MaterialPropertyBlock brakeMpb;
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        private void Awake()
        {
            Rb = GetComponent<Rigidbody>();
            Rb.centerOfMass = centerOfMassOffset;
            Rb.interpolation = RigidbodyInterpolation.Interpolate;
            Rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            brakeMpb = new MaterialPropertyBlock();
        }

        public void ResetToPose(Vector3 position, Quaternion rotation)
        {
            if (Rb == null) Rb = GetComponent<Rigidbody>();
#if UNITY_6000_0_OR_NEWER
            Rb.linearVelocity = Vector3.zero;
#else
            Rb.velocity = Vector3.zero;
#endif
            Rb.angularVelocity = Vector3.zero;
            transform.SetPositionAndRotation(position, rotation);
            currentSteerAngle = 0f;
            ThrottleInput = 0f;
            SteerInput = 0f;
        }

        private void Update()
        {
            bool canDrive = StreetGameLoop.Instance != null && StreetGameLoop.Instance.State == StreetGameLoop.RunState.Running;

            if (!canDrive)
            {
                ThrottleInput = 0f;
                SteerInput = 0f;
                IsBraking = true;
                UpdateSteeringWheelVisual();
                return;
            }

            ReadInputs(out float accel, out float steer, out bool handbrake, out bool hornPressed, out bool resetPressed);
            ThrottleInput = accel;
            SteerInput = steer;

            if (hornPressed && KenyaStreetAudio.Instance != null)
            {
                KenyaStreetAudio.Instance.PlayHorn(isCoach);
            }

            if (resetPressed)
            {
                Vector3 uprightPos = new Vector3(Mathf.Clamp(transform.position.x, -5.2f, 5.2f), 0.35f, transform.position.z);
                ResetToPose(uprightPos, Quaternion.Euler(0f, transform.eulerAngles.y, 0f));
            }

            IsBraking = handbrake || (accel < -0.1f && ForwardSpeedMs > 0.8f) || (accel > 0.1f && ForwardSpeedMs < -0.8f);
            UpdateSteeringWheelVisual();
            UpdateBrakeLights();

            if (KenyaStreetAudio.Instance != null)
            {
                KenyaStreetAudio.Instance.UpdateEngine(SpeedKmh, Mathf.Abs(ThrottleInput), true);
            }
        }

        private void FixedUpdate()
        {
            if (Rb == null || wheels == null || wheels.Length == 0) return;

#if UNITY_6000_0_OR_NEWER
            Vector3 vel = Rb.linearVelocity;
#else
            Vector3 vel = Rb.velocity;
#endif
            ForwardSpeedMs = Vector3.Dot(vel, transform.forward);
            SpeedKmh = vel.magnitude * 3.6f;

            bool canDrive = StreetGameLoop.Instance != null && StreetGameLoop.Instance.State == StreetGameLoop.RunState.Running;
            float speedFactor = Mathf.Clamp01(SpeedKmh / maxSpeedKmh);
            float allowedSteer = Mathf.Lerp(maxSteerAngle, highSpeedSteerAngle, speedFactor);
            float targetSteer = canDrive ? SteerInput * allowedSteer : 0f;
            currentSteerAngle = Mathf.MoveTowards(currentSteerAngle, targetSteer, 130f * Time.fixedDeltaTime);

            int driveCount = 0;
            for (int i = 0; i < wheels.Length; i++)
            {
                if (wheels[i] != null && wheels[i].isDrive) driveCount++;
            }
            float perDriveShare = driveCount > 0 ? 1f / driveCount : 0.25f;

            float appliedMotor = 0f;
            float appliedBrake = 0f;

            if (!canDrive)
            {
                appliedBrake = handbrakeTorque;
            }
            else if (IsHandbrakeHeld())
            {
                appliedBrake = handbrakeTorque;
            }
            else if (ThrottleInput > 0.05f)
            {
                if (ForwardSpeedMs < -0.5f)
                {
                    appliedBrake = brakeTorque;
                }
                else if (SpeedKmh < maxSpeedKmh)
                {
                    appliedMotor = ThrottleInput * motorTorque * perDriveShare * 2.2f;
                }
            }
            else if (ThrottleInput < -0.05f)
            {
                if (ForwardSpeedMs > 0.5f)
                {
                    appliedBrake = brakeTorque;
                }
                else if (SpeedKmh < 24f)
                {
                    appliedMotor = ThrottleInput * reverseTorque * perDriveShare * 2.0f;
                }
            }
            else
            {
                // Gentle engine braking / coast stop when idle at very low speed
                appliedBrake = SpeedKmh < 2.5f ? brakeTorque * 0.45f : 120f;
            }

            for (int i = 0; i < wheels.Length; i++)
            {
                var w = wheels[i];
                if (w == null || w.collider == null) continue;

                w.collider.steerAngle = w.isSteer ? currentSteerAngle : 0f;
                w.collider.motorTorque = w.isDrive ? appliedMotor : 0f;
                w.collider.brakeTorque = appliedBrake;

                if (w.visualMesh != null)
                {
                    w.collider.GetWorldPose(out Vector3 pos, out Quaternion rot);
                    w.visualMesh.SetPositionAndRotation(pos, rot);
                }
            }

            ApplyAntiRoll();
            UpdateGearLabel();
        }

        private void ApplyAntiRoll()
        {
            // Pair consecutive left/right wheels to stabilize tall bus body
            for (int i = 0; i + 1 < wheels.Length; i += 2)
            {
                var wL = wheels[i]?.collider;
                var wR = wheels[i + 1]?.collider;
                if (wL == null || wR == null) continue;

                float travelL = 1f;
                float travelR = 1f;
                bool groundedL = wL.GetGroundHit(out WheelHit hitL);
                bool groundedR = wR.GetGroundHit(out WheelHit hitR);

                if (groundedL)
                    travelL = (-wL.transform.InverseTransformPoint(hitL.point).y - wL.radius) / Mathf.Max(0.05f, wL.suspensionDistance);
                if (groundedR)
                    travelR = (-wR.transform.InverseTransformPoint(hitR.point).y - wR.radius) / Mathf.Max(0.05f, wR.suspensionDistance);

                float antiRollForce = (travelL - travelR) * antiRollStiffness;
                if (groundedL)
                    Rb.AddForceAtPosition(wL.transform.up * -antiRollForce, wL.transform.position);
                if (groundedR)
                    Rb.AddForceAtPosition(wR.transform.up * antiRollForce, wR.transform.position);
            }
        }

        private void UpdateGearLabel()
        {
            if (ForwardSpeedMs < -0.4f) { GearLabel = "R"; return; }
            if (SpeedKmh < 1.2f) { GearLabel = "N"; return; }
            if (SpeedKmh < 16f) GearLabel = "1";
            else if (SpeedKmh < 30f) GearLabel = "2";
            else if (SpeedKmh < 44f) GearLabel = "3";
            else if (SpeedKmh < 58f) GearLabel = "4";
            else if (SpeedKmh < 70f) GearLabel = "5";
            else GearLabel = "6";
        }

        private void UpdateSteeringWheelVisual()
        {
            if (steeringWheelVisual == null) return;
            float wheelTurn = -currentSteerAngle * 5.5f;
            steeringWheelVisual.localRotation = Quaternion.Euler(22f, 0f, wheelTurn);
        }

        private void UpdateBrakeLights()
        {
            if (brakeLightRenderers == null) return;
            Color emissive = IsBraking ? Palette.TaillightRed * 3.0f : Palette.TaillightRed * 0.45f;
            for (int i = 0; i < brakeLightRenderers.Length; i++)
            {
                var r = brakeLightRenderers[i];
                if (r == null) continue;
                r.GetPropertyBlock(brakeMpb);
                brakeMpb.SetColor(EmissionColorId, emissive);
                r.SetPropertyBlock(brakeMpb);
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (StreetGameLoop.Instance == null || StreetGameLoop.Instance.State != StreetGameLoop.RunState.Running)
                return;

            if (Time.time - lastCollisionTime < 1.35f) return;

            float impact = collision.relativeVelocity.magnitude;
            if (impact < 2.2f) return;

            bool hitTraffic = collision.collider.GetComponentInParent<KenyaTrafficVehicle>() != null;
            bool hitPedestrian = collision.collider.GetComponentInParent<KenyaPedestrianAI>() != null;

            if (hitTraffic || hitPedestrian || impact > 6.5f)
            {
                lastCollisionTime = Time.time;
                int penalty = hitPedestrian ? 25 : (hitTraffic ? 18 : 10);
                string reason = hitPedestrian ? "Pedestrian Incident!" : (hitTraffic ? "Traffic Collision!" : "Hard Curb / Building Impact!");
                StreetGameLoop.Instance.RegisterSafetyPenalty(penalty, reason, Mathf.Clamp01(impact / 12f));
            }
        }

        private static bool IsHandbrakeHeld()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.spaceKey.isPressed;
#else
            return Input.GetKey(KeyCode.Space);
#endif
        }

        private static void ReadInputs(out float accel, out float steer, out bool handbrake, out bool horn, out bool reset)
        {
            accel = 0f;
            steer = 0f;
            handbrake = false;
            horn = false;
            reset = false;

#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) accel += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) accel -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) steer += 1f;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) steer -= 1f;
                handbrake = kb.spaceKey.isPressed;
                horn = kb.hKey.wasPressedThisFrame;
                reset = kb.rKey.wasPressedThisFrame;
            }
#else
            accel = Input.GetAxis("Vertical");
            steer = Input.GetAxis("Horizontal");
            handbrake = Input.GetKey(KeyCode.Space);
            horn = Input.GetKeyDown(KeyCode.H);
            reset = Input.GetKeyDown(KeyCode.R);
#endif
        }
    }
}
