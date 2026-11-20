using System;
using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace KenyaBusSim
{
    public enum KenyanVehicleClass
    {
        EnaCoachBus,
        EphIsuzuTruck
    }

    [Serializable]
    public class AxleConfig
    {
        public string axleName = "Axle";
        public WheelCollider leftCollider;
        public WheelCollider rightCollider;
        public Transform leftVisual;
        public Transform rightVisual;
        [Range(-1f, 1f)] public float steerMultiplier = 0f; // 1.0 = front steer 1, 0.72 = twin steer 2
        public bool isMotor = false;
        public bool isBraking = true;
        public float antiRollForce = 12000f;
    }

    /// <summary>
    /// Realistic Unity 6 WheelCollider multi-axle commercial vehicle controller
    /// built for Kenyan highway buses (8-wheel twin-steer) and Isuzu FRR 6-wheel cargo trucks.
    /// Compatible with both Unity 6 New Input System and Legacy Input Manager.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class KenyaVehicleController : MonoBehaviour
    {
        [Header("Vehicle Identity")]
        public KenyanVehicleClass vehicleClass = KenyanVehicleClass.EnaCoachBus;
        public string displayName = "ENA Coach KDK 057M (MAN Master Apollo)";
        public string companyTag = "ENA COACH";
        public string routeLabel = "Nairobi ➔ Nakuru ➔ Kericho ➔ Kisii ➔ Kisumu";

        [Header("Multi-Axle Setup")]
        public List<AxleConfig> axles = new List<AxleConfig>();

        [Header("Engine & Handling Specs")]
        public float maxMotorTorque = 4200f;
        public float maxBrakeTorque = 9500f;
        public float handbrakeTorque = 16000f;
        public float maxSteerAngleLowSpeed = 34f;
        public float maxSteerAngleHighSpeed = 13f;
        public float maxHighwaySpeedKmh = 95f;
        public float ntsaGovernorKmh = 80f;
        public bool ntsaSpeedGovernorEnabled = false;
        public Vector3 centerOfMassOffset = new Vector3(0f, -0.55f, 0.25f);

        [Header("Cockpit & Camera Anchors")]
        public Transform cockpitCameraAnchor;
        public Transform chaseCameraTarget;
        public Transform wheelCamAnchor;
        public Transform steeringWheelTransform;
        public float steeringWheelMaxDegrees = 450f;

        [Header("Gameplay Condition (Comfort / Parcel Integrity)")]
        [Range(0f, 100f)] public float conditionScore = 100f;
        public float bumpImpactThreshold = 2.6f;

        // Public Telemetry for HUD & Camera
        public float SpeedKmh { get; private set; }
        public float ForwardSpeedMs { get; private set; }
        public float CurrentRpm { get; private set; } = 750f;
        public string CurrentGear { get; private set; } = "N";
        public float ThrottleInput { get; private set; }
        public float SteerInput { get; private set; }
        public bool HandbrakeActive { get; private set; }
        public string ActiveAlertMessage { get; private set; } = "";
        public float AlertTimer { get; private set; }
        public bool NearSpeedBump { get; set; }
        public bool IsPlayerControlled { get; set; } = true;

        private Rigidbody rb;
        private KenyaVehicleAudio vehicleAudio;
        private Quaternion steeringWheelBaseRot = Quaternion.identity;
        private bool steeringWheelBaseCaptured;
        private float smoothedSteer;
        private float lastVerticalVelocity;
        private float bumpCooldown;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            rb.centerOfMass = centerOfMassOffset;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            vehicleAudio = GetComponent<KenyaVehicleAudio>();

            if (steeringWheelTransform != null)
            {
                steeringWheelBaseRot = steeringWheelTransform.localRotation;
                steeringWheelBaseCaptured = true;
            }
        }

        private void Update()
        {
            if (AlertTimer > 0f)
            {
                AlertTimer -= Time.deltaTime;
                if (AlertTimer <= 0f) ActiveAlertMessage = "";
            }

            if (bumpCooldown > 0f)
            {
                bumpCooldown -= Time.deltaTime;
            }

            if (!IsPlayerControlled)
            {
                ThrottleInput = 0f;
                SteerInput = 0f;
                HandbrakeActive = true;
                return;
            }

            ReadInputs();
            UpdateSteeringWheelVisual();
        }

        private void FixedUpdate()
        {
            UpdateTelemetry();
            ApplyVehiclePhysics();
            ApplyAntiRollBars();
            DetectSuspensionJolt();
            SyncWheelVisuals();
        }

        private void ReadInputs()
        {
            float throttle = 0f;
            float steer = 0f;
            bool handbrake = false;
            bool hornPressed = false;
            bool toggleGovernor = false;

#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) throttle += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) throttle -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) steer += 1f;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) steer -= 1f;
                handbrake = kb.spaceKey.isPressed;
                hornPressed = kb.hKey.wasPressedThisFrame;
                toggleGovernor = kb.gKey.wasPressedThisFrame;
            }
#else
            throttle = Input.GetAxis("Vertical");
            steer = Input.GetAxis("Horizontal");
            handbrake = Input.GetKey(KeyCode.Space);
            hornPressed = Input.GetKeyDown(KeyCode.H);
            toggleGovernor = Input.GetKeyDown(KeyCode.G);
#endif

            ThrottleInput = throttle;
            SteerInput = steer;
            HandbrakeActive = handbrake;

            if (hornPressed && vehicleAudio != null)
            {
                vehicleAudio.PlayHorn();
            }

            if (toggleGovernor)
            {
                ntsaSpeedGovernorEnabled = !ntsaSpeedGovernorEnabled;
                ShowAlert(ntsaSpeedGovernorEnabled
                    ? "NTSA Speed Governor ON (80 km/h limit)"
                    : "NTSA Speed Governor OFF (Highway mode)", 2.2f);
            }
        }

        private void UpdateTelemetry()
        {
#if UNITY_6000_0_OR_NEWER
            Vector3 vel = rb.linearVelocity;
#else
            Vector3 vel = rb.velocity;
#endif
            ForwardSpeedMs = Vector3.Dot(vel, transform.forward);
            SpeedKmh = Mathf.Abs(ForwardSpeedMs) * 3.6f;

            // Automatic commercial 6-speed gearbox readout
            if (ForwardSpeedMs < -0.4f)
            {
                CurrentGear = "R";
                CurrentRpm = Mathf.Lerp(850f, 1850f, Mathf.Clamp01(SpeedKmh / 22f));
            }
            else if (SpeedKmh < 1.2f && Mathf.Abs(ThrottleInput) < 0.05f)
            {
                CurrentGear = "N";
                CurrentRpm = Mathf.Lerp(CurrentRpm, 720f, Time.fixedDeltaTime * 5f);
            }
            else
            {
                int gearNum;
                float gearMin, gearMax;
                if (SpeedKmh < 16f) { gearNum = 1; gearMin = 0f; gearMax = 18f; }
                else if (SpeedKmh < 30f) { gearNum = 2; gearMin = 12f; gearMax = 32f; }
                else if (SpeedKmh < 46f) { gearNum = 3; gearMin = 26f; gearMax = 48f; }
                else if (SpeedKmh < 64f) { gearNum = 4; gearMin = 42f; gearMax = 66f; }
                else if (SpeedKmh < 80f) { gearNum = 5; gearMin = 60f; gearMax = 84f; }
                else { gearNum = 6; gearMin = 76f; gearMax = 105f; }

                CurrentGear = "D" + gearNum;
                float gearT = Mathf.InverseLerp(gearMin, gearMax, SpeedKmh);
                float targetRpm = Mathf.Lerp(950f, 2250f, gearT) + Mathf.Max(0f, ThrottleInput) * 180f;
                CurrentRpm = Mathf.Lerp(CurrentRpm, targetRpm, Time.fixedDeltaTime * 6f);
            }
        }

        private void ApplyVehiclePhysics()
        {
            smoothedSteer = Mathf.Lerp(smoothedSteer, SteerInput, Time.fixedDeltaTime * 7.5f);

            float speedFactor = Mathf.Clamp01(SpeedKmh / 85f);
            float activeMaxSteer = Mathf.Lerp(maxSteerAngleLowSpeed, maxSteerAngleHighSpeed, speedFactor);
            float activeSpeedCap = ntsaSpeedGovernorEnabled ? ntsaGovernorKmh : maxHighwaySpeedKmh;

            bool braking = false;
            float motor = 0f;

            if (ThrottleInput > 0.05f)
            {
                if (ForwardSpeedMs < -0.5f)
                {
                    braking = true;
                }
                else if (SpeedKmh < activeSpeedCap)
                {
                    motor = ThrottleInput * maxMotorTorque;
                }
            }
            else if (ThrottleInput < -0.05f)
            {
                if (ForwardSpeedMs > 0.5f)
                {
                    braking = true;
                }
                else if (SpeedKmh < 24f)
                {
                    motor = ThrottleInput * maxMotorTorque * 0.65f;
                }
            }

            foreach (AxleConfig axle in axles)
            {
                if (axle.leftCollider == null || axle.rightCollider == null) continue;

                // Multi-axle steering (Ackermann ratio via steerMultiplier)
                float angle = smoothedSteer * activeMaxSteer * axle.steerMultiplier;
                axle.leftCollider.steerAngle = angle;
                axle.rightCollider.steerAngle = angle;

                // Motor torque
                float appliedMotor = (axle.isMotor && !HandbrakeActive) ? motor : 0f;
                axle.leftCollider.motorTorque = appliedMotor;
                axle.rightCollider.motorTorque = appliedMotor;

                // Air braking & handbrake
                float appliedBrake = 0f;
                if (HandbrakeActive)
                {
                    appliedBrake = handbrakeTorque;
                }
                else if (braking && axle.isBraking)
                {
                    appliedBrake = maxBrakeTorque;
                }
                else if (Mathf.Abs(ThrottleInput) < 0.05f)
                {
                    // Light diesel engine braking when coasting
                    appliedBrake = 160f;
                }

                axle.leftCollider.brakeTorque = appliedBrake;
                axle.rightCollider.brakeTorque = appliedBrake;
            }
        }

        private void ApplyAntiRollBars()
        {
            foreach (AxleConfig axle in axles)
            {
                if (axle.leftCollider == null || axle.rightCollider == null) continue;
                if (axle.antiRollForce <= 0f) continue;

                float travelL = 1f;
                float travelR = 1f;

                bool groundedL = axle.leftCollider.GetGroundHit(out WheelHit hitL);
                if (groundedL)
                {
                    travelL = (-axle.leftCollider.transform.InverseTransformPoint(hitL.point).y - axle.leftCollider.radius)
                              / axle.leftCollider.suspensionDistance;
                }

                bool groundedR = axle.rightCollider.GetGroundHit(out WheelHit hitR);
                if (groundedR)
                {
                    travelR = (-axle.rightCollider.transform.InverseTransformPoint(hitR.point).y - axle.rightCollider.radius)
                              / axle.rightCollider.suspensionDistance;
                }

                float antiRoll = (travelL - travelR) * axle.antiRollForce;
                if (groundedL) rb.AddForceAtPosition(axle.leftCollider.transform.up * -antiRoll, axle.leftCollider.transform.position);
                if (groundedR) rb.AddForceAtPosition(axle.rightCollider.transform.up * antiRoll, axle.rightCollider.transform.position);
            }
        }

        private void DetectSuspensionJolt()
        {
#if UNITY_6000_0_OR_NEWER
            float currentVy = rb.linearVelocity.y;
#else
            float currentVy = rb.velocity.y;
#endif
            float deltaVy = Mathf.Abs(currentVy - lastVerticalVelocity);
            lastVerticalVelocity = currentVy;

            if (deltaVy > bumpImpactThreshold && SpeedKmh > 24f && bumpCooldown <= 0f)
            {
                bumpCooldown = 1.1f;
                float severity = Mathf.Clamp01((deltaVy - bumpImpactThreshold) / 4.5f + (SpeedKmh / 110f));
                float penalty = Mathf.Lerp(5f, 20f, severity);
                conditionScore = Mathf.Max(0f, conditionScore - penalty);

                if (vehicleAudio != null)
                {
                    vehicleAudio.PlayBumpThud(severity);
                }

                if (vehicleClass == KenyanVehicleClass.EnaCoachBus)
                {
                    ShowAlert("Passenger: \"Dereva, pole pole! Hii gari sio ndege!\" (Slow down on bumps!)", 3.0f);
                }
                else
                {
                    ShowAlert("EPH Logistics: Fragile parcel cargo jolted! Slow down below 25 km/h.", 3.0f);
                }
            }
        }

        private void SyncWheelVisuals()
        {
            foreach (AxleConfig axle in axles)
            {
                SyncSingleWheel(axle.leftCollider, axle.leftVisual);
                SyncSingleWheel(axle.rightCollider, axle.rightVisual);
            }
        }

        private static void SyncSingleWheel(WheelCollider col, Transform visual)
        {
            if (col == null || visual == null) return;
            col.GetWorldPose(out Vector3 pos, out Quaternion rot);
            visual.position = pos;
            visual.rotation = rot;
        }

        private void UpdateSteeringWheelVisual()
        {
            if (steeringWheelTransform == null) return;
            if (!steeringWheelBaseCaptured)
            {
                steeringWheelBaseRot = steeringWheelTransform.localRotation;
                steeringWheelBaseCaptured = true;
            }
            float targetZ = -smoothedSteer * steeringWheelMaxDegrees;
            steeringWheelTransform.localRotation = steeringWheelBaseRot * Quaternion.Euler(0f, 0f, targetZ);
        }

        public void ResetCondition()
        {
            conditionScore = 100f;
        }

        public void ShowAlert(string message, float duration = 2.5f)
        {
            ActiveAlertMessage = message;
            AlertTimer = duration;
        }
    }
}
