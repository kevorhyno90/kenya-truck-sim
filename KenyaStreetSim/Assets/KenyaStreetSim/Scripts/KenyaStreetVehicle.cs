using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace KenyaStreetSim
{
    /// <summary>
    /// ETS2 / RK 254 KE style tuned Unity 6 WheelCollider controller for Kenyan commercial vehicles:
    /// ENA Coach KDK 057M (8-wheel twin-steer coach) and EPH Isuzu FRR 90 (6-wheel dual-rear lorry).
    /// Includes Left/Right/Hazard blinking turn indicators (Q/E/Z), working SpotLight Headlights (L),
    /// RPM simulation, pneumatic air-brake hiss, anti-roll bars, and rotating RHD steering wheel.
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
        public float maxSpeedKmh = 82f;
        public float antiRollStiffness = 9500f;
        public Vector3 centerOfMassOffset = new Vector3(0f, -0.58f, 0.15f);

        [Header("References")]
        public WheelEntry[] wheels;
        public Transform cockpitCameraAnchor;
        public Transform bumperCameraAnchor;
        public Transform axleCameraAnchor;
        public Transform steeringWheelVisual;
        public Vector3 steeringWheelSpinAxis = Vector3.forward;
        public Renderer[] brakeLightRenderers;
        public Renderer[] headlightRenderers;
        public Light[] headlightSpots;
        public Renderer[] leftIndicatorRenderers;
        public Renderer[] rightIndicatorRenderers;

        public Rigidbody Rb { get; private set; }
        public float SpeedKmh { get; private set; }
        public float ForwardSpeedMs { get; private set; }
        public float EngineRpm { get; private set; } = 680f;
        public float RpmNormalized => Mathf.InverseLerp(650f, 2400f, EngineRpm);
        public string GearLabel { get; private set; } = "N";
        public float ThrottleInput { get; private set; }
        public float SteerInput { get; private set; }
        public bool IsBraking { get; private set; }

        public bool HeadlightsOn { get; private set; }
        public bool LeftSignalOn { get; private set; }
        public bool RightSignalOn { get; private set; }
        public bool HazardsOn { get; private set; }
        public bool BlinkPhaseOn { get; private set; }

        private float currentSteerAngle;
        private float lastCollisionTime = -10f;
        private float blinkTimer;
        private float prevSpeedKmh;
        private Quaternion steeringWheelRestRot = Quaternion.identity;
        private bool hasSteerRest;
        private MaterialPropertyBlock lightMpb;
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        private void Awake()
        {
            Rb = GetComponent<Rigidbody>();
            Rb.centerOfMass = centerOfMassOffset;
            Rb.interpolation = RigidbodyInterpolation.Interpolate;
            Rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            lightMpb = new MaterialPropertyBlock();
            SetHeadlights(false);
        }

        public void ResetToPose(Vector3 position, Quaternion rotation)
        {
            if (Rb == null) Rb = GetComponent<Rigidbody>();
            var prevInterp = Rb.interpolation;
            Rb.interpolation = RigidbodyInterpolation.None;

            if (!Rb.isKinematic)
            {
#if UNITY_6000_0_OR_NEWER
                Rb.linearVelocity = Vector3.zero;
#else
                Rb.velocity = Vector3.zero;
#endif
                Rb.angularVelocity = Vector3.zero;
            }

            // Flush PhysX body + interpolation state so long-distance Kenya fast-travel never snaps back
            Rb.isKinematic = true;
            transform.SetPositionAndRotation(position, rotation);
            Rb.position = position;
            Rb.rotation = rotation;
            Physics.SyncTransforms();
            Rb.isKinematic = false;

#if UNITY_6000_0_OR_NEWER
            Rb.linearVelocity = Vector3.zero;
#else
            Rb.velocity = Vector3.zero;
#endif
            Rb.angularVelocity = Vector3.zero;
            Rb.Sleep();
            Rb.WakeUp();
            Rb.interpolation = prevInterp;

            if (wheels != null)
            {
                for (int i = 0; i < wheels.Length; i++)
                {
                    if (wheels[i] != null && wheels[i].collider != null)
                    {
                        wheels[i].collider.steerAngle = 0f;
                        wheels[i].collider.motorTorque = 0f;
                        wheels[i].collider.brakeTorque = handbrakeTorque;
                        if (wheels[i].visualMesh != null)
                        {
                            wheels[i].collider.GetWorldPose(out Vector3 wp, out Quaternion wr);
                            wheels[i].visualMesh.SetPositionAndRotation(wp, wr);
                        }
                    }
                }
            }

            currentSteerAngle = 0f;
            ThrottleInput = 0f;
            SteerInput = 0f;
            prevSpeedKmh = 0f;
            SpeedKmh = 0f;
            ForwardSpeedMs = 0f;
        }

        public void SetHeadlights(bool on)
        {
            HeadlightsOn = on;
            if (headlightSpots != null)
            {
                for (int i = 0; i < headlightSpots.Length; i++)
                {
                    if (headlightSpots[i] != null) headlightSpots[i].enabled = on;
                }
            }
            Color emissive = on ? Palette.HeadlightWarm * 3.8f : Palette.HeadlightWarm * 0.45f;
            ApplyEmission(headlightRenderers, emissive);
        }

        public void ToggleHeadlights()
        {
            SetHeadlights(!HeadlightsOn);
            KenyaStreetAudio.Instance?.PlayUiClick();
        }

        public void ToggleLeftSignal()
        {
            HazardsOn = false;
            LeftSignalOn = !LeftSignalOn;
            if (LeftSignalOn) RightSignalOn = false;
            blinkTimer = 0f;
            BlinkPhaseOn = LeftSignalOn;
            UpdateIndicatorsVisual();
        }

        public void ToggleRightSignal()
        {
            HazardsOn = false;
            RightSignalOn = !RightSignalOn;
            if (RightSignalOn) LeftSignalOn = false;
            blinkTimer = 0f;
            BlinkPhaseOn = RightSignalOn;
            UpdateIndicatorsVisual();
        }

        public void ToggleHazards()
        {
            HazardsOn = !HazardsOn;
            LeftSignalOn = HazardsOn;
            RightSignalOn = HazardsOn;
            blinkTimer = 0f;
            BlinkPhaseOn = HazardsOn;
            UpdateIndicatorsVisual();
        }

        private void Update()
        {
            bool canDrive = StreetGameLoop.Instance != null && StreetGameLoop.Instance.State == StreetGameLoop.RunState.Running;

            if (!canDrive)
            {
                ThrottleInput = 0f;
                SteerInput = 0f;
                IsBraking = true;
                EngineRpm = Mathf.Lerp(EngineRpm, 680f, Time.unscaledDeltaTime * 5f);
                UpdateSteeringWheelVisual();
                return;
            }

            ReadInputs(
                out float accel, out float steer, out bool handbrake,
                out bool hornPressed, out bool resetPressed,
                out bool leftSig, out bool rightSig, out bool hazardSig, out bool lightToggle);

            ThrottleInput = accel;
            SteerInput = steer;

            if (hornPressed) KenyaStreetAudio.Instance?.PlayHorn(isCoach);
            if (leftSig) ToggleLeftSignal();
            if (rightSig) ToggleRightSignal();
            if (hazardSig) ToggleHazards();
            if (lightToggle) ToggleHeadlights();

            if (resetPressed)
            {
                if (KenyaNetworkData.FindNearestRoadPoint(transform.position, out Vector3 roadPt, out Vector3 roadFwd, out _))
                {
                    Vector3 left = Vector3.Cross(roadFwd, Vector3.up).normalized;
                    Vector3 spawnPos = roadPt + left * 3.4f + Vector3.up * 0.35f;
                    ResetToPose(spawnPos, Quaternion.LookRotation(roadFwd, Vector3.up));
                }
                else
                {
                    Vector3 uprightPos = new Vector3(transform.position.x, 0.35f, transform.position.z);
                    ResetToPose(uprightPos, Quaternion.Euler(0f, transform.eulerAngles.y, 0f));
                }
            }

            IsBraking = handbrake || (accel < -0.1f && ForwardSpeedMs > 0.8f) || (accel > 0.1f && ForwardSpeedMs < -0.8f);

            // Pneumatic air-brake hiss when bringing heavy Kenyan coach/truck to a stop from speed
            if (IsBraking && prevSpeedKmh > 14f && SpeedKmh <= 6f)
            {
                KenyaStreetAudio.Instance?.PlayAirBrakeHiss();
            }
            prevSpeedKmh = Mathf.Lerp(prevSpeedKmh, SpeedKmh, Time.deltaTime * 1.4f);

            UpdateBlinkers();
            UpdateSteeringWheelVisual();
            UpdateBrakeLights();
            UpdateRpmAndAudio();
        }

        private void UpdateBlinkers()
        {
            bool anySignal = LeftSignalOn || RightSignalOn || HazardsOn;
            if (!anySignal)
            {
                if (BlinkPhaseOn)
                {
                    BlinkPhaseOn = false;
                    UpdateIndicatorsVisual();
                }
                return;
            }

            blinkTimer += Time.deltaTime;
            if (blinkTimer >= 0.42f)
            {
                blinkTimer = 0f;
                BlinkPhaseOn = !BlinkPhaseOn;
                UpdateIndicatorsVisual();
                KenyaStreetAudio.Instance?.PlayIndicatorTick(BlinkPhaseOn);
            }
        }

        private void UpdateIndicatorsVisual()
        {
            Color onCol = Palette.UiAccentAmber * 3.8f;
            ApplyEmission(leftIndicatorRenderers, (BlinkPhaseOn && (LeftSignalOn || HazardsOn)) ? onCol : Color.black);
            ApplyEmission(rightIndicatorRenderers, (BlinkPhaseOn && (RightSignalOn || HazardsOn)) ? onCol : Color.black);
        }

        private void UpdateRpmAndAudio()
        {
            float gearBand = SpeedKmh < 16f ? SpeedKmh / 16f
                           : SpeedKmh < 30f ? (SpeedKmh - 10f) / 20f
                           : SpeedKmh < 44f ? (SpeedKmh - 22f) / 22f
                           : SpeedKmh < 58f ? (SpeedKmh - 36f) / 22f
                           : SpeedKmh < 70f ? (SpeedKmh - 48f) / 22f
                           : (SpeedKmh - 56f) / 26f;

            float targetRpm = Mathf.Lerp(700f, 2250f, Mathf.Clamp01(gearBand * 0.72f + Mathf.Abs(ThrottleInput) * 0.38f));
            EngineRpm = Mathf.Lerp(EngineRpm, targetRpm, Time.deltaTime * 6f);

            KenyaStreetAudio.Instance?.UpdateEngine(SpeedKmh, Mathf.Abs(ThrottleInput), true, RpmNormalized);
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
                    appliedMotor = ThrottleInput * motorTorque * perDriveShare * 2.25f;
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
            if (!hasSteerRest)
            {
                steeringWheelRestRot = steeringWheelVisual.localRotation;
                hasSteerRest = true;
            }
            float wheelTurn = -currentSteerAngle * 5.8f;
            Vector3 axis = steeringWheelSpinAxis.sqrMagnitude > 0.01f ? steeringWheelSpinAxis.normalized : Vector3.forward;
            steeringWheelVisual.localRotation = steeringWheelRestRot * Quaternion.AngleAxis(wheelTurn, axis);
        }

        private void UpdateBrakeLights()
        {
            Color emissive = IsBraking
                ? Palette.TaillightRed * 3.4f
                : (HeadlightsOn ? Palette.TaillightRed * 1.15f : Palette.TaillightRed * 0.35f);
            ApplyEmission(brakeLightRenderers, emissive);
        }

        private void ApplyEmission(Renderer[] renderers, Color emissive)
        {
            if (renderers == null || lightMpb == null) return;
            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null) continue;
                r.GetPropertyBlock(lightMpb);
                lightMpb.SetColor(EmissionColorId, emissive);
                r.SetPropertyBlock(lightMpb);
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (StreetGameLoop.Instance == null || StreetGameLoop.Instance.State != StreetGameLoop.RunState.Running)
                return;

            if (Time.time - lastCollisionTime < 1.35f) return;

            float impact = collision.relativeVelocity.magnitude;
            if (impact < 2.2f) return;

            bool hitTraffic = collision.collider.GetComponentInParent<KenyaTrafficVehicle>() != null
                           || collision.collider.GetComponentInParent<KenyaSplineTraffic>() != null;
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

        private static void ReadInputs(
            out float accel, out float steer, out bool handbrake,
            out bool horn, out bool reset,
            out bool leftSig, out bool rightSig, out bool hazardSig, out bool lightToggle)
        {
            accel = 0f;
            steer = 0f;
            handbrake = false;
            horn = false;
            reset = false;
            leftSig = false;
            rightSig = false;
            hazardSig = false;
            lightToggle = false;

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
                reset = kb.backspaceKey.wasPressedThisFrame;
                leftSig = kb.qKey.wasPressedThisFrame;
                rightSig = kb.eKey.wasPressedThisFrame;
                hazardSig = kb.zKey.wasPressedThisFrame;
                lightToggle = kb.lKey.wasPressedThisFrame;
            }
#else
            accel = Input.GetAxis("Vertical");
            steer = Input.GetAxis("Horizontal");
            handbrake = Input.GetKey(KeyCode.Space);
            horn = Input.GetKeyDown(KeyCode.H);
            reset = Input.GetKeyDown(KeyCode.Backspace);
            leftSig = Input.GetKeyDown(KeyCode.Q);
            rightSig = Input.GetKeyDown(KeyCode.E);
            hazardSig = Input.GetKeyDown(KeyCode.Z);
            lightToggle = Input.GetKeyDown(KeyCode.L);
#endif
        }
    }
}
