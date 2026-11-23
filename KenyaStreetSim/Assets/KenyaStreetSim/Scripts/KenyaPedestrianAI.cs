using UnityEngine;

namespace KenyaStreetSim
{
    /// <summary>
    /// Living Kenyan pedestrian AI supporting BOTH real rigged 3D SkinnedMesh characters
    /// (Quaternius / Mixamo GLB/FBX armatures) and fallback articulated characters.
    /// Automatically brings T-posed rigged humans into natural relaxed stance, smooth walking stride,
    /// terminal waiting idle + head look, vehicle yielding, and boarding celebration!
    /// </summary>
    public class KenyaPedestrianAI : MonoBehaviour
    {
        public enum BehaviorMode { SidewalkPatrol, ZebraCrossing, BusStageWaiter }

        [Header("Behavior")]
        public BehaviorMode mode = BehaviorMode.SidewalkPatrol;
        public Vector3 pointA;
        public Vector3 pointB;
        public float walkSpeed = 1.45f;
        public StreetTrafficLight crossingLight;
        public int stageIndex = -1;

        [Header("Articulated / Rigged Bones")]
        public bool useSkeletalRig;
        public Transform bodyPivot;
        public Transform torsoBone;
        public Transform headBone;
        public Transform leftArm;
        public Transform leftForeArm;
        public Transform rightArm;
        public Transform rightForeArm;
        public Transform leftLeg;
        public Transform leftLowerLeg;
        public Transform rightLeg;
        public Transform rightLowerLeg;

        private bool goingToB = true;
        private float walkCyclePhase;
        private float idlePhase;
        private float baseBodyY;
        private bool boarded;
        private bool walkingToDoor;
        private Vector3 doorTargetPos;
        private Vector3 initialHomePos;
        private Quaternion initialHomeRot;
        private float celebrateTimer;

        // Cached bone rest local rotations so Blender/glTF rigs animate cleanly regardless of bone roll
        private Quaternion restTorso, restHead;
        private Quaternion restArmL, restForeL, restArmR, restForeR;
        private Quaternion restLegL, restShinL, restLegR, restShinR;
        private bool cachedRest;

        private void Start()
        {
            initialHomePos = transform.position;
            initialHomeRot = transform.rotation;
            walkCyclePhase = Random.Range(0f, Mathf.PI * 2f);
            idlePhase = Random.Range(0f, Mathf.PI * 2f);
            if (bodyPivot != null)
            {
                baseBodyY = bodyPivot.localPosition.y;
            }
            CacheRestPose();
            // Apply initial idle pose on frame 0 so no character ever renders in T-pose!
            AnimateIdle(instant: true);
        }

        private void CacheRestPose()
        {
            if (cachedRest) return;
            cachedRest = true;
            if (torsoBone != null) restTorso = torsoBone.localRotation;
            if (headBone != null) restHead = headBone.localRotation;
            if (leftArm != null) restArmL = leftArm.localRotation;
            if (leftForeArm != null) restForeL = leftForeArm.localRotation;
            if (rightArm != null) restArmR = rightArm.localRotation;
            if (rightForeArm != null) restForeR = rightForeArm.localRotation;
            if (leftLeg != null) restLegL = leftLeg.localRotation;
            if (leftLowerLeg != null) restShinL = leftLowerLeg.localRotation;
            if (rightLeg != null) restLegR = rightLeg.localRotation;
            if (rightLowerLeg != null) restShinR = rightLowerLeg.localRotation;
        }

        public void BeginBoardingToDoor(Vector3 doorWorldPos)
        {
            if (boarded || !gameObject.activeSelf) return;
            doorTargetPos = new Vector3(doorWorldPos.x, transform.position.y, doorWorldPos.z);
            walkingToDoor = true;
        }

        public void TriggerBoardCelebration()
        {
            if (boarded) return;
            boarded = true;
            walkingToDoor = false;
            celebrateTimer = 0.75f;
        }

        public void ResetCommuter()
        {
            boarded = false;
            walkingToDoor = false;
            celebrateTimer = 0f;
            gameObject.SetActive(true);
            transform.localScale = Vector3.one;
            if (cachedRest)
            {
                transform.SetPositionAndRotation(initialHomePos, initialHomeRot);
                AnimateIdle(instant: true);
            }
        }

        private void Update()
        {
            if (boarded)
            {
                celebrateTimer -= Time.deltaTime;
                float jump = Mathf.Abs(Mathf.Sin(Time.time * 12f)) * 0.16f;
                if (bodyPivot != null)
                {
                    var lp = bodyPivot.localPosition;
                    lp.y = baseBodyY + jump;
                    bodyPivot.localPosition = lp;
                }
                if (useSkeletalRig)
                {
                    ApplyWorldBoneDelta(leftArm, restArmL, -52f, 0f, 0f);
                    ApplyWorldBoneDelta(rightArm, restArmR, 52f, 0f, 0f);
                }
                else
                {
                    if (leftArm != null) leftArm.localRotation = Quaternion.Euler(-140f, 0f, 12f);
                    if (rightArm != null) rightArm.localRotation = Quaternion.Euler(-140f, 0f, -12f);
                }
                if (celebrateTimer <= 0f)
                {
                    gameObject.SetActive(false);
                }
                return;
            }

            // Waiting 3D Kenyan passenger actively walking right through the open bus door!
            if (walkingToDoor)
            {
                Vector3 toDoor = doorTargetPos - transform.position;
                toDoor.y = 0f;
                float dDoor = toDoor.magnitude;
                if (dDoor < 0.85f)
                {
                    boarded = true;
                    walkingToDoor = false;
                    gameObject.SetActive(false);
                    KenyaStreetAudio.Instance?.PlayPickupBlip();
                    return;
                }
                Vector3 stepDir = toDoor.normalized;
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(stepDir, Vector3.up), Time.deltaTime * 10f);
                transform.position += stepDir * (Mathf.Max(2.2f, walkSpeed * 1.65f) * Time.deltaTime);
                AnimateWalk();
                return;
            }

            if (mode == BehaviorMode.BusStageWaiter)
            {
                AnimateIdle(instant: false);
                return;
            }

            Vector3 target = goingToB ? pointB : pointA;
            Vector3 toTarget = target - transform.position;
            toTarget.y = 0f;
            float dist = toTarget.magnitude;

            if (dist < 0.35f)
            {
                goingToB = !goingToB;
                target = goingToB ? pointB : pointA;
                toTarget = target - transform.position;
                toTarget.y = 0f;
            }

            Vector3 dir = toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized : transform.forward;
            bool shouldWait = false;

            if (mode == BehaviorMode.ZebraCrossing)
            {
                bool onRoad = Mathf.Abs(transform.position.x) < 6.4f;
                if (!onRoad && crossingLight != null && crossingLight.IsMainBoulevardGreen)
                {
                    shouldWait = true;
                }
                if (!shouldWait && IsVehicleNearAhead(dir))
                {
                    shouldWait = true;
                }
            }

            if (shouldWait)
            {
                AnimateIdle(instant: false);
                return;
            }

            Quaternion desiredRot = Quaternion.LookRotation(dir, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRot, Time.deltaTime * 8f);
            transform.position += dir * (walkSpeed * Time.deltaTime);

            AnimateWalk();
        }

        private bool IsVehicleNearAhead(Vector3 dir)
        {
            Vector3 origin = transform.position + Vector3.up * 0.9f;
            if (Physics.SphereCast(origin, 0.45f, dir, out RaycastHit hit, 3.8f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.GetComponentInParent<KenyaStreetVehicle>() != null ||
                    hit.collider.GetComponentInParent<KenyaTrafficVehicle>() != null ||
                    hit.collider.GetComponentInParent<KenyaSplineTraffic>() != null)
                {
                    return true;
                }
            }
            return false;
        }

        private void AnimateWalk()
        {
            walkCyclePhase += Time.deltaTime * walkSpeed * 5.0f;
            float s = Mathf.Sin(walkCyclePhase);
            float swing = s * 30f;
            float bob = Mathf.Abs(s) * 0.04f;

            if (useSkeletalRig)
            {
                float kneeL = Mathf.Max(0f, -s) * 32f;
                float kneeR = Mathf.Max(0f,  s) * 32f;
                float torsoYaw = s * 4.5f;

                ApplyWorldBoneDelta(torsoBone, restTorso, 0f, 3f, torsoYaw);
                ApplyWorldBoneDelta(headBone, restHead, 0f, 0f, -torsoYaw * 0.6f);

                // Drop T-pose arms down cleanly along sides (+74 / -74 deg around forward), then swing forward/back
                ApplyWorldBoneDelta(leftArm, restArmL,  74f, -swing * 0.72f, 0f);
                ApplyWorldBoneDelta(rightArm, restArmR, -74f,  swing * 0.72f, 0f);
                ApplyWorldBoneDelta(leftForeArm, restForeL, 0f, -18f - Mathf.Max(0f, -s) * 14f, 0f);
                ApplyWorldBoneDelta(rightForeArm, restForeR, 0f, -18f - Mathf.Max(0f,  s) * 14f, 0f);

                ApplyWorldBoneDelta(leftLeg, restLegL,  0f,  swing, 0f);
                ApplyWorldBoneDelta(rightLeg, restLegR, 0f, -swing, 0f);
                ApplyWorldBoneDelta(leftLowerLeg, restShinL,  0f, kneeL, 0f);
                ApplyWorldBoneDelta(rightLowerLeg, restShinR, 0f, kneeR, 0f);
            }
            else
            {
                if (leftLeg != null) leftLeg.localRotation = Quaternion.Euler(swing, 0f, 0f);
                if (rightLeg != null) rightLeg.localRotation = Quaternion.Euler(-swing, 0f, 0f);
                if (leftArm != null) leftArm.localRotation = Quaternion.Euler(-swing * 0.75f, 0f, 0f);
                if (rightArm != null) rightArm.localRotation = Quaternion.Euler(swing * 0.75f, 0f, 0f);
            }

            if (bodyPivot != null)
            {
                var lp = bodyPivot.localPosition;
                lp.y = baseBodyY + bob;
                bodyPivot.localPosition = lp;
            }
        }

        private void AnimateIdle(bool instant)
        {
            idlePhase += Time.deltaTime * 2.3f;
            float breath = Mathf.Sin(idlePhase) * 0.014f;
            float gentleSway = Mathf.Sin(idlePhase * 0.7f) * 2.5f;
            float headLook = Mathf.Sin(idlePhase * 0.45f) * 12f;

            if (useSkeletalRig)
            {
                ApplyWorldBoneDelta(torsoBone, restTorso, 0f, gentleSway * 0.4f, 0f);
                ApplyWorldBoneDelta(headBone, restHead, 0f, 0f, headLook);

                ApplyWorldBoneDelta(leftArm, restArmL,  76f,  gentleSway, 0f);
                ApplyWorldBoneDelta(rightArm, restArmR, -76f, -gentleSway, 0f);
                ApplyWorldBoneDelta(leftForeArm, restForeL, 0f, -14f, 0f);
                ApplyWorldBoneDelta(rightForeArm, restForeR, 0f, -14f, 0f);

                ApplyWorldBoneDelta(leftLeg, restLegL,  0f, 0f, 0f);
                ApplyWorldBoneDelta(rightLeg, restLegR, 0f, 0f, 0f);
                ApplyWorldBoneDelta(leftLowerLeg, restShinL,  0f, 0f, 0f);
                ApplyWorldBoneDelta(rightLowerLeg, restShinR, 0f, 0f, 0f);
            }
            else
            {
                float t = instant ? 1f : Time.deltaTime * 6f;
                if (leftLeg != null) leftLeg.localRotation = Quaternion.Slerp(leftLeg.localRotation, Quaternion.identity, t);
                if (rightLeg != null) rightLeg.localRotation = Quaternion.Slerp(rightLeg.localRotation, Quaternion.identity, t);
                if (leftArm != null) leftArm.localRotation = Quaternion.Slerp(leftArm.localRotation, Quaternion.Euler(gentleSway, 0f, 4f), t);
                if (rightArm != null) rightArm.localRotation = Quaternion.Slerp(rightArm.localRotation, Quaternion.Euler(-gentleSway, 0f, -4f), t);
            }

            if (bodyPivot != null)
            {
                var lp = bodyPivot.localPosition;
                lp.y = baseBodyY + breath;
                bodyPivot.localPosition = lp;
            }
        }

        private void ApplyWorldBoneDelta(Transform bone, Quaternion restLocal, float forwardRollDeg, float rightPitchDeg, float upYawDeg)
        {
            if (bone == null || bone.parent == null) return;
            Quaternion restWorld = bone.parent.rotation * restLocal;
            Quaternion deltaWorld = Quaternion.AngleAxis(upYawDeg, transform.up)
                                  * Quaternion.AngleAxis(rightPitchDeg, transform.right)
                                  * Quaternion.AngleAxis(forwardRollDeg, transform.forward);
            bone.localRotation = Quaternion.Inverse(bone.parent.rotation) * (deltaWorld * restWorld);
        }
    }
}
