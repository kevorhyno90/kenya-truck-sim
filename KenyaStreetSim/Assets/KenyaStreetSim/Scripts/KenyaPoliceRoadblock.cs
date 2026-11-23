using UnityEngine;

namespace KenyaStreetSim
{
    /// <summary>
    /// Authentic Kenyan Police Highway Roadblock Inspection System:
    /// - Movable Red/White boom barrier + STOP POLICE CHECK sign + traffic cones + parked KCP patrol vehicle
    /// - 2 Uniformed Kenyan Police Officers (Royal Navy uniform + bright reflective lime-green vest)
    /// - When the player approaches and slows down (< 14 km/h), Officer #1 LITERALLY WALKS ALL THE WAY
    ///   AROUND THE PLAYER'S VEHICLE (front -> right -> rear -> left inspection), returns to post,
    ///   waves the driver through, and raises the boom barrier ("POLICE CHECK CLEARED - SAFARI NJEMA!")!
    /// </summary>
    public class KenyaPoliceRoadblock : MonoBehaviour
    {
        public string checkpointName = "KENYA POLICE HIGHWAY CHECK";
        public Transform boomBarrierPivot;
        public Transform inspectorOfficer;
        public Transform officerRightArm;
        public Transform officerLeftLeg;
        public Transform officerRightLeg;
        public Renderer beaconLightRenderer;

        private enum CheckState { IdleWaiting, PromptSlowDown, InspectingWalkAround, ClearedWaveThrough }
        private CheckState state = CheckState.IdleWaiting;

        private Vector3 officerHomePos;
        private Quaternion officerHomeRot;
        private Vector3[] walkWaypoints;
        private int currentWp;
        private float waveTimer;
        private float barrierAngle;
        private float walkCycle;
        private float cooldownTimer;
        private MaterialPropertyBlock mpb;
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        private void Awake()
        {
            mpb = new MaterialPropertyBlock();
            if (inspectorOfficer != null)
            {
                officerHomePos = inspectorOfficer.position;
                officerHomeRot = inspectorOfficer.rotation;
            }
        }

        private void Update()
        {
            // Gently pulse roadblock amber/blue warning beacon
            if (beaconLightRenderer != null && mpb != null)
            {
                bool pulse = Mathf.Sin(Time.time * 7f) > 0f;
                beaconLightRenderer.GetPropertyBlock(mpb);
                mpb.SetColor(EmissionColorId, pulse ? new Color(0.15f, 0.55f, 1f) * 3.8f : new Color(0.95f, 0.15f, 0.15f) * 3.8f);
                beaconLightRenderer.SetPropertyBlock(mpb);
            }

            if (cooldownTimer > 0f)
            {
                cooldownTimer -= Time.deltaTime;
                if (cooldownTimer <= 0f)
                {
                    state = CheckState.IdleWaiting;
                }
            }

            var loop = StreetGameLoop.Instance;
            var veh = (loop != null && loop.State == StreetGameLoop.RunState.Running) ? loop.ActiveVehicle : null;
            float dist = veh != null ? Vector3.Distance(transform.position, veh.transform.position) : 999f;

            // Smoothly raise/lower boom barrier (0° = closed horizontal across left lane, -76° = raised open!)
            float targetBarrier = (state == CheckState.ClearedWaveThrough) ? -76f : 0f;
            barrierAngle = Mathf.MoveTowards(barrierAngle, targetBarrier, Time.deltaTime * 68f);
            if (boomBarrierPivot != null)
            {
                boomBarrierPivot.localRotation = Quaternion.Euler(0f, 0f, barrierAngle);
            }

            if (veh == null) return;

            switch (state)
            {
                case CheckState.IdleWaiting:
                    if (dist < 28f)
                    {
                        state = CheckState.PromptSlowDown;
                        loop.hud?.ShowToast($"{checkpointName} — SLOW DOWN FOR ROUTINE WALK-AROUND CHECK");
                    }
                    break;

                case CheckState.PromptSlowDown:
                    // Wave hand to signal slow down
                    AnimateOfficerWave(1.0f);
                    if (dist > 45f)
                    {
                        state = CheckState.IdleWaiting;
                    }
                    else if (dist < 17f && veh.SpeedKmh < 14f)
                    {
                        BeginWalkAroundInspection(veh);
                    }
                    break;

                case CheckState.InspectingWalkAround:
                    UpdateWalkAround(veh);
                    break;

                case CheckState.ClearedWaveThrough:
                    waveTimer += Time.deltaTime;
                    AnimateOfficerWave(2.2f);
                    if (dist > 34f || waveTimer > 12f)
                    {
                        cooldownTimer = 18f;
                        if (dist > 34f) state = CheckState.IdleWaiting;
                    }
                    break;
            }
        }

        private void BeginWalkAroundInspection(KenyaStreetVehicle veh)
        {
            state = CheckState.InspectingWalkAround;
            currentWp = 0;

            // Compute 6 smooth waypoints completely circling the player's actual vehicle!
            Transform vt = veh.transform;
            float halfW = veh.isCoach ? 2.15f : 1.75f;
            float halfL = veh.isCoach ? 6.6f : (veh.plateNumber == "KCP 380D" ? 3.2f : 4.8f);
            float y = officerHomePos.y;

            Vector3 pFrontLeft  = vt.position + vt.forward * (halfL + 0.8f) - vt.right * halfW; pFrontLeft.y = y;
            Vector3 pFrontRight = vt.position + vt.forward * (halfL + 0.8f) + vt.right * halfW; pFrontRight.y = y;
            Vector3 pRearRight  = vt.position - vt.forward * (halfL + 0.8f) + vt.right * halfW; pRearRight.y = y;
            Vector3 pRearLeft   = vt.position - vt.forward * (halfL + 0.8f) - vt.right * halfW; pRearLeft.y = y;
            Vector3 pDriverDoor = vt.position + vt.forward * (halfL * 0.4f) + vt.right * (halfW + 0.2f); pDriverDoor.y = y;

            walkWaypoints = new[] { pFrontLeft, pFrontRight, pRearRight, pRearLeft, pDriverDoor, officerHomePos };
            StreetGameLoop.Instance?.hud?.ShowToast("OFFICER INSPECTING VEHICLE — PLEASE HOLD A MOMENT...");
        }

        private void UpdateWalkAround(KenyaStreetVehicle veh)
        {
            if (inspectorOfficer == null || walkWaypoints == null || currentWp >= walkWaypoints.Length)
            {
                FinishInspection();
                return;
            }

            // If player drives away mid-check, gently open anyway without freezing
            if (Vector3.Distance(transform.position, veh.transform.position) > 32f)
            {
                inspectorOfficer.SetPositionAndRotation(officerHomePos, officerHomeRot);
                FinishInspection();
                return;
            }

            Vector3 target = walkWaypoints[currentWp];
            Vector3 to = target - inspectorOfficer.position;
            to.y = 0f;
            float d = to.magnitude;
            if (d < 0.45f)
            {
                currentWp++;
                if (currentWp >= walkWaypoints.Length)
                {
                    inspectorOfficer.rotation = officerHomeRot;
                    FinishInspection();
                }
                return;
            }

            float speed = 3.2f; // Brisk realistic officer walk-around (~5 seconds total circle!)
            inspectorOfficer.position += to.normalized * (speed * Time.deltaTime);
            Quaternion look = Quaternion.LookRotation(to.normalized, Vector3.up);
            inspectorOfficer.rotation = Quaternion.Slerp(inspectorOfficer.rotation, look, Time.deltaTime * 8f);

            walkCycle += Time.deltaTime * 9.5f;
            float legSwing = Mathf.Sin(walkCycle) * 28f;
            if (officerLeftLeg != null) officerLeftLeg.localRotation = Quaternion.Euler(legSwing, 0f, 0f);
            if (officerRightLeg != null) officerRightLeg.localRotation = Quaternion.Euler(-legSwing, 0f, 0f);
            if (officerRightArm != null) officerRightArm.localRotation = Quaternion.Euler(-legSwing * 0.6f, 0f, 0f);
        }

        private void FinishInspection()
        {
            state = CheckState.ClearedWaveThrough;
            waveTimer = 0f;
            if (officerLeftLeg != null) officerLeftLeg.localRotation = Quaternion.identity;
            if (officerRightLeg != null) officerRightLeg.localRotation = Quaternion.identity;
            KenyaStreetAudio.Instance?.PlayPickupBlip();
            StreetGameLoop.Instance?.hud?.ShowToast("ALL CLEAR — SAFARI NJEMA! BARRIER RAISED, PROCEED");
        }

        private void AnimateOfficerWave(float speed)
        {
            if (officerRightArm == null) return;
            float wave = Mathf.Sin(Time.time * 6.5f * speed) * 22f;
            officerRightArm.localRotation = Quaternion.Euler(-135f + wave, 0f, 18f);
        }
    }
}
