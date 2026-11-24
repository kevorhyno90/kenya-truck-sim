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

        public enum VehicleJobRole
        {
            BusPassenger,
            PolicePatrol,
            CargoParcel
        }

        [Header("Vehicle Identity")]
        public string displayName = "ENA Coach KDK 057M";
        public string plateNumber = "KDK 057M";
        public bool isCoach = true;
        public VehicleJobRole jobRole = VehicleJobRole.BusPassenger;

        public bool IsPoliceSirenActive
        {
            get
            {
                if (policeEmergency == null) policeEmergency = GetComponent<KenyaPoliceEmergencySystem>();
                return policeEmergency != null && policeEmergency.SirenActive;
            }
        }
        private KenyaPoliceEmergencySystem policeEmergency;

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

        // ETS2-Class Cruise Control ([X], [-]/[=]) & Exhaust Retarder / Jake Brake ([N])!
        public bool CruiseControlOn { get; private set; }
        public float CruiseTargetKmh { get; private set; } = 80f;
        public int RetarderStage { get; private set; } // 0 = OFF, 1 = STAGE 1, 2 = STAGE 2

        private float baseMotorTorque;
        private float baseBrakeTorque;
        private float baseFuelBurnPerKm;
        private bool savedBaseSpecs;

        [Header("Diesel Fuel System")]
        [Range(0f, 1f)] public float fuelNormalized = 0.88f;
        public float fuelBurnPerKm = 0.045f; // Real highway burn across long Kenyan corridors -> refuel at Petrol Stations!
        public float FuelPercent => Mathf.Round(Mathf.Clamp01(fuelNormalized) * 100f);
        public float EstimatedRangeKm => Mathf.Round(Mathf.Clamp01(fuelNormalized) / Mathf.Max(0.005f, fuelBurnPerKm) * 16f);

        public void ApplyPerformanceUpgrades(bool turboEngine, bool heavyBrakes, bool longRangeTank)
        {
            if (!savedBaseSpecs)
            {
                baseMotorTorque = motorTorque;
                baseBrakeTorque = brakeTorque;
                baseFuelBurnPerKm = fuelBurnPerKm;
                savedBaseSpecs = true;
            }
            motorTorque = baseMotorTorque * (turboEngine ? 1.24f : 1.0f);
            brakeTorque = baseBrakeTorque * (heavyBrakes ? 1.26f : 1.0f);
            fuelBurnPerKm = baseFuelBurnPerKm * (longRangeTank ? 0.62f : 1.0f);
        }

        public void ApplySaccoPaintLivery(Color primaryPaint)
        {
            var mpb = new MaterialPropertyBlock();
            int baseColorId = Shader.PropertyToID("_BaseColor");
            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                if (r == null || r.sharedMaterial == null) continue;
                string mn = r.sharedMaterial.name.ToLowerInvariant();
                string on = r.gameObject.name.ToLowerInvariant();
                // Repaint main body skirt / cab paint panels without overwriting glass, chrome, lights, plates or tires!
                if (mn.Contains("red") || mn.Contains("skirt") || mn.Contains("body") || mn.Contains("paint") ||
                    mn.Contains("blue") || on.Contains("skirt") || on.Contains("cab") || on.Contains("stripe"))
                {
                    if (mn.Contains("glass") || mn.Contains("tire") || mn.Contains("chrome") || mn.Contains("tail") ||
                        mn.Contains("head") || mn.Contains("plate") || mn.Contains("led") || on.Contains("glass") ||
                        on.Contains("mirror") || on.Contains("wheel") || on.Contains("tail") || on.Contains("ind"))
                        continue;
                    r.GetPropertyBlock(mpb);
                    mpb.SetColor(baseColorId, primaryPaint);
                    r.SetPropertyBlock(mpb);
                }
            }
        }

        public void ToggleCruiseControl()
        {
            if (CruiseControlOn)
            {
                CruiseControlOn = false;
                StreetGameLoop.Instance?.hud?.ShowToast("CRUISE CONTROL: OFF");
            }
            else
            {
                CruiseTargetKmh = Mathf.Clamp(Mathf.Round(Mathf.Max(40f, SpeedKmh) / 5f) * 5f, 40f, Mathf.Min(100f, maxSpeedKmh));
                CruiseControlOn = true;
                StreetGameLoop.Instance?.hud?.ShowToast($"ETS2 CRUISE CONTROL: {CruiseTargetKmh:0} KM/H  ([-] / [=] ADJUST)");
            }
            KenyaStreetAudio.Instance?.PlayUiClick();
        }

        public void AdjustCruiseSpeed(float deltaKmh)
        {
            CruiseTargetKmh = Mathf.Clamp(CruiseTargetKmh + deltaKmh, 30f, Mathf.Min(105f, maxSpeedKmh));
            if (CruiseControlOn)
            {
                StreetGameLoop.Instance?.hud?.ShowToast($"CRUISE TARGET: {CruiseTargetKmh:0} KM/H");
            }
        }

        public void CycleRetarderStage()
        {
            RetarderStage = (RetarderStage + 1) % 3;
            string lbl = RetarderStage == 0 ? "OFF" : (RetarderStage == 1 ? "STAGE 1 (GENTLE HILL HOLD)" : "STAGE 2 (MAX ESCARPMENT BRAKE)");
            StreetGameLoop.Instance?.hud?.ShowToast($"EXHAUST RETARDER: {lbl}");
            KenyaStreetAudio.Instance?.PlayAirBrakeHiss();
        }

        public void RefuelStep(float amount01)
        {
            fuelNormalized = Mathf.Clamp01(fuelNormalized + Mathf.Max(0f, amount01));
        }

        public void RefuelFull()
        {
            fuelNormalized = 1f;
        }

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
            policeEmergency = GetComponent<KenyaPoliceEmergencySystem>();
            // Ensure each vehicle strictly knows its real Kenyan job role!
            if (plateNumber == "KCP 380D" || (displayName != null && displayName.IndexOf("Police", System.StringComparison.OrdinalIgnoreCase) >= 0))
                jobRole = VehicleJobRole.PolicePatrol;
            else if (isCoach)
                jobRole = VehicleJobRole.BusPassenger;
            else
                jobRole = VehicleJobRole.CargoParcel;
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
                out bool leftSig, out bool rightSig, out bool hazardSig, out bool lightToggle,
                out bool cruiseToggle, out float cruiseDelta, out bool retarderCycle);

            if (cruiseToggle) ToggleCruiseControl();
            if (Mathf.Abs(cruiseDelta) > 0.1f) AdjustCruiseSpeed(cruiseDelta);
            if (retarderCycle) CycleRetarderStage();

            // Tapping brake or handbrake cancels Cruise Control cleanly just like ETS2!
            if (CruiseControlOn && (accel < -0.1f || handbrake))
            {
                CruiseControlOn = false;
            }
            if (CruiseControlOn && accel < 0.05f)
            {
                float err = CruiseTargetKmh - SpeedKmh;
                accel = Mathf.Clamp(err * 0.12f, -0.18f, 0.88f);
            }

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
                    Vector3 spawnPos = roadPt + left * 3.4f;
                    spawnPos.y = KenyaNetworkData.GetElevationAt(spawnPos.x, spawnPos.z) + 0.35f;
                    ResetToPose(spawnPos, Quaternion.LookRotation(roadFwd, Vector3.up));
                }
                else
                {
                    float groundY = KenyaNetworkData.GetElevationAt(transform.position.x, transform.position.z);
                    Vector3 uprightPos = new Vector3(transform.position.x, groundY + 0.35f, transform.position.z);
                    ResetToPose(uprightPos, Quaternion.Euler(0f, transform.eulerAngles.y, 0f));
                }
            }

            // Gentle realistic diesel burn while driving along Kenya's highways (refuel at major town Petrol Stations!)
            if (SpeedKmh > 1.5f)
            {
                float kmStep = (SpeedKmh / 3600f) * Time.deltaTime;
                fuelNormalized = Mathf.Max(0.05f, fuelNormalized - kmStep * fuelBurnPerKm);
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
                float retarderTorque = (RetarderStage == 2 && SpeedKmh > 12f) ? 1850f
                                     : (RetarderStage == 1 && SpeedKmh > 12f) ? 880f : 0f;
                appliedBrake = SpeedKmh < 2.5f ? brakeTorque * 0.45f : (120f + retarderTorque);
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
            out bool leftSig, out bool rightSig, out bool hazardSig, out bool lightToggle,
            out bool cruiseToggle, out float cruiseDelta, out bool retarderCycle)
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
            cruiseToggle = false;
            cruiseDelta = 0f;
            retarderCycle = false;

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
                cruiseToggle = kb.xKey.wasPressedThisFrame;
                if (kb.equalsKey.wasPressedThisFrame || kb.pageUpKey.wasPressedThisFrame) cruiseDelta = 5f;
                if (kb.minusKey.wasPressedThisFrame || kb.pageDownKey.wasPressedThisFrame) cruiseDelta = -5f;
                retarderCycle = kb.nKey.wasPressedThisFrame;
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
            cruiseToggle = Input.GetKeyDown(KeyCode.X);
            if (Input.GetKeyDown(KeyCode.Equals) || Input.GetKeyDown(KeyCode.PageUp)) cruiseDelta = 5f;
            if (Input.GetKeyDown(KeyCode.Minus) || Input.GetKeyDown(KeyCode.PageDown)) cruiseDelta = -5f;
            retarderCycle = Input.GetKeyDown(KeyCode.N);
#endif
        }
    }
}
