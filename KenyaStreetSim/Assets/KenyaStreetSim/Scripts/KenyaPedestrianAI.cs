using UnityEngine;

namespace KenyaStreetSim
{
    /// <summary>
    /// Stylized articulated Kenyan pedestrian: sidewalk strollers, intersection zebra crossers,
    /// and bus-stage waiting commuters with limb walk cycles, idle breathing, and vehicle yielding.
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

        [Header("Articulated Limbs")]
        public Transform bodyPivot;
        public Transform leftArm;
        public Transform rightArm;
        public Transform leftLeg;
        public Transform rightLeg;

        private bool goingToB = true;
        private float walkCyclePhase;
        private float idlePhase;
        private float baseBodyY;
        private bool boarded;
        private float celebrateTimer;

        private void Start()
        {
            walkCyclePhase = Random.Range(0f, Mathf.PI * 2f);
            idlePhase = Random.Range(0f, Mathf.PI * 2f);
            if (bodyPivot != null)
            {
                baseBodyY = bodyPivot.localPosition.y;
            }
        }

        public void TriggerBoardCelebration()
        {
            if (boarded) return;
            boarded = true;
            celebrateTimer = 1.1f;
        }

        public void ResetCommuter()
        {
            boarded = false;
            celebrateTimer = 0f;
            gameObject.SetActive(true);
            transform.localScale = Vector3.one;
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
                if (leftArm != null) leftArm.localRotation = Quaternion.Euler(-140f, 0f, 12f);
                if (rightArm != null) rightArm.localRotation = Quaternion.Euler(-140f, 0f, -12f);
                if (celebrateTimer <= 0f)
                {
                    gameObject.SetActive(false);
                }
                return;
            }

            if (mode == BehaviorMode.BusStageWaiter)
            {
                AnimateIdle();
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
                // Wait at curb if main road traffic has green light, unless already mid-road
                bool onRoad = Mathf.Abs(transform.position.x) < 6.4f;
                if (!onRoad && crossingLight != null && crossingLight.IsMainBoulevardGreen)
                {
                    shouldWait = true;
                }

                // Always yield if a vehicle is directly ahead
                if (!shouldWait && IsVehicleNearAhead(dir))
                {
                    shouldWait = true;
                }
            }

            if (shouldWait)
            {
                AnimateIdle();
                return;
            }

            // Smoothly face travel direction and walk
            Quaternion desiredRot = Quaternion.LookRotation(dir, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRot, Time.deltaTime * 8f);
            transform.position += dir * (walkSpeed * Time.deltaTime);

            AnimateWalk();
        }

        private bool IsVehicleNearAhead(Vector3 dir)
        {
            Vector3 origin = transform.position + Vector3.up * 0.9f;
            if (Physics.SphereCast(origin, 0.45f, dir, out RaycastHit hit, 3.6f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.GetComponentInParent<KenyaStreetVehicle>() != null ||
                    hit.collider.GetComponentInParent<KenyaTrafficVehicle>() != null)
                {
                    return true;
                }
            }
            return false;
        }

        private void AnimateWalk()
        {
            walkCyclePhase += Time.deltaTime * walkSpeed * 5.2f;
            float swing = Mathf.Sin(walkCyclePhase) * 32f;
            float bob = Mathf.Abs(Mathf.Sin(walkCyclePhase)) * 0.045f;

            if (leftLeg != null) leftLeg.localRotation = Quaternion.Euler(swing, 0f, 0f);
            if (rightLeg != null) rightLeg.localRotation = Quaternion.Euler(-swing, 0f, 0f);
            if (leftArm != null) leftArm.localRotation = Quaternion.Euler(-swing * 0.75f, 0f, 0f);
            if (rightArm != null) rightArm.localRotation = Quaternion.Euler(swing * 0.75f, 0f, 0f);

            if (bodyPivot != null)
            {
                var lp = bodyPivot.localPosition;
                lp.y = baseBodyY + bob;
                bodyPivot.localPosition = lp;
            }
        }

        private void AnimateIdle()
        {
            idlePhase += Time.deltaTime * 2.6f;
            float breath = Mathf.Sin(idlePhase) * 0.02f;
            float gentleSway = Mathf.Sin(idlePhase * 0.7f) * 3f;

            if (leftLeg != null) leftLeg.localRotation = Quaternion.Slerp(leftLeg.localRotation, Quaternion.identity, Time.deltaTime * 6f);
            if (rightLeg != null) rightLeg.localRotation = Quaternion.Slerp(rightLeg.localRotation, Quaternion.identity, Time.deltaTime * 6f);
            if (leftArm != null) leftArm.localRotation = Quaternion.Slerp(leftArm.localRotation, Quaternion.Euler(gentleSway, 0f, 4f), Time.deltaTime * 6f);
            if (rightArm != null) rightArm.localRotation = Quaternion.Slerp(rightArm.localRotation, Quaternion.Euler(-gentleSway, 0f, -4f), Time.deltaTime * 6f);

            if (bodyPivot != null)
            {
                var lp = bodyPivot.localPosition;
                lp.y = baseBodyY + breath;
                bodyPivot.localPosition = lp;
            }
        }
    }
}
