using System.Collections.Generic;
using UnityEngine;

namespace KenyaStreetSim
{
    /// <summary>
    /// Spline-following Left-Hand Traffic (LHT) AI vehicle driving smoothly across all curved
    /// Kenyan ENA Coach highways (Nairobi, B3 Narok-Kisii, A1 Migori, A104/B1 Nakuru-Kisumu, A109 Mombasa).
    /// Handles smooth curve steering, obstacle braking behind player/traffic, and spinning wheels.
    /// </summary>
    public class KenyaSplineTraffic : MonoBehaviour
    {
        [Header("Highway Route")]
        public int highwayIndex;
        [Tooltip("True = travel forward along highway waypoints (left lane); False = oncoming opposite lane")]
        public bool forwardDirection = true;
        public float startNormalizedProgress;
        public float laneOffsetMeters = 3.4f;

        [Header("Town-Weighted Local Circulation")]
        [Tooltip("When true, vehicle circulates briskly within a major Kenyan town segment rather than entire rural highway")]
        public bool limitToSubRange;
        [Range(0f, 1f)] public float rangeMinNorm = 0f;
        [Range(0f, 1f)] public float rangeMaxNorm = 1f;

        [Header("Driving")]
        public float cruiseSpeed = 14.5f;
        public float vehicleHalfLength = 2.6f;
        public float safeStopDistance = 7.5f;
        public float wheelRadius = 0.36f;
        public Transform[] wheelVisuals;

        private Vector3[] pathPoints;
        private float[] cumulativeDist;
        private float totalLength;
        private float minDist;
        private float maxDist;
        private float currentDist;
        private float currentSpeed;
        private float stuckTimer;
        private float lateralBypassOffset;
        private float sirenPullOverOffset;
        private bool bypassing;

        private void Start()
        {
            if (highwayIndex < 0 || highwayIndex >= KenyaNetworkData.Highways.Length)
            {
                enabled = false;
                return;
            }

            var sampled = KenyaNetworkData.SampleHighway(KenyaNetworkData.Highways[highwayIndex], 6f);
            if (!forwardDirection)
            {
                sampled.Reverse();
            }

            BuildOffsetPath(sampled);
            if (totalLength < 10f)
            {
                enabled = false;
                return;
            }

            if (limitToSubRange && rangeMaxNorm > rangeMinNorm + 0.02f)
            {
                minDist = Mathf.Clamp01(rangeMinNorm) * totalLength;
                maxDist = Mathf.Clamp01(rangeMaxNorm) * totalLength;
                currentDist = Mathf.Lerp(minDist, maxDist, Mathf.Repeat(startNormalizedProgress, 1f));
            }
            else
            {
                minDist = 0f;
                maxDist = totalLength;
                currentDist = Mathf.Repeat(startNormalizedProgress, 1f) * totalLength;
            }

            currentSpeed = cruiseSpeed * 0.85f;
            EvaluatePose(currentDist, out Vector3 p, out Quaternion rot);
            transform.SetPositionAndRotation(p, rot);
        }

        private void BuildOffsetPath(List<Vector3> centerLine)
        {
            int n = centerLine.Count;
            pathPoints = new Vector3[n];
            cumulativeDist = new float[n];
            totalLength = 0f;

            for (int i = 0; i < n; i++)
            {
                Vector3 fwd;
                if (i == 0) fwd = (centerLine[1] - centerLine[0]).normalized;
                else if (i == n - 1) fwd = (centerLine[n - 1] - centerLine[n - 2]).normalized;
                else fwd = (centerLine[i + 1] - centerLine[i - 1]).normalized;

                // Kenya Left-Hand Traffic: offset to the LEFT of travel direction & hug exact 3D highway slope elevation!
                Vector3 fwdFlat = new Vector3(fwd.x, 0f, fwd.z).normalized;
                Vector3 left = Vector3.Cross(fwdFlat, Vector3.up).normalized;
                Vector3 pt = centerLine[i] + left * laneOffsetMeters;
                pt.y = KenyaNetworkData.GetElevationAt(pt.x, pt.z);
                pathPoints[i] = pt;

                if (i > 0)
                {
                    totalLength += Vector3.Distance(pathPoints[i - 1], pathPoints[i]);
                }
                cumulativeDist[i] = totalLength;
            }
        }

        private void Update()
        {
            if (pathPoints == null || totalLength < 10f) return;

            float targetSpeed = cruiseSpeed;
            bool redLightStop = (highwayIndex == 0 && ShouldStopForNairobiLight());

            // 1. STRICT PLAYER ROAD RESPECT & POLICE SIREN YIELD (AI cars NEVER push/ram/shove the player!)
            bool yieldingToPlayer = CheckPlayerRespect(out float playerSpeedCap, out float desiredSirenPullOver, out bool hardHaltForPlayer);
            if (yieldingToPlayer)
            {
                targetSpeed = Mathf.Min(targetSpeed, playerSpeedCap);
                bypassing = false;
                stuckTimer = 0f;
            }
            sirenPullOverOffset = Mathf.MoveTowards(sirenPullOverOffset, desiredSirenPullOver, Time.deltaTime * 2.2f);

            // 2. AI-vs-AI same-lane spacing + anti-jam deadlock breaker (ONLY when not yielding to player!)
            float obstacleDist = float.MaxValue;
            bool aiAhead = !yieldingToPlayer && !bypassing && CheckAiObstacleAhead(out obstacleDist);
            if (aiAhead)
            {
                float ratio = Mathf.InverseLerp(safeStopDistance * 0.35f, safeStopDistance * 1.7f, obstacleDist);
                targetSpeed = Mathf.Min(targetSpeed, cruiseSpeed * ratio);
                if (obstacleDist < safeStopDistance * 0.50f) targetSpeed = 0f;
            }

            if (redLightStop)
            {
                targetSpeed = 0f;
            }

            // Anti-Traffic-Jam Deadlock Recovery for AI-vs-AI ONLY (never bypasses through the player!)
            if (!redLightStop && !yieldingToPlayer && targetSpeed < 2.2f)
            {
                stuckTimer += Time.deltaTime;
                if (stuckTimer > 3.2f)
                {
                    bypassing = true;
                    targetSpeed = Mathf.Max(6.5f, cruiseSpeed * 0.65f);
                }
                if (stuckTimer > 7.5f)
                {
                    currentDist += 38f;
                    stuckTimer = 0f;
                    bypassing = false;
                }
            }
            else
            {
                stuckTimer = Mathf.MoveTowards(stuckTimer, 0f, Time.deltaTime * 1.6f);
                if (bypassing && stuckTimer < 0.25f)
                {
                    bypassing = false;
                }
            }

            float targetBypass = bypassing ? 2.85f : 0f;
            lateralBypassOffset = Mathf.MoveTowards(lateralBypassOffset, targetBypass, Time.deltaTime * 2.4f);

            float accelRate = targetSpeed < currentSpeed ? 22f : 7.0f;
            currentSpeed = hardHaltForPlayer ? 0f : Mathf.MoveTowards(currentSpeed, targetSpeed, accelRate * Time.deltaTime);

            float nextDist = currentDist + currentSpeed * Time.deltaTime;
            float span = Mathf.Max(20f, maxDist - minDist);
            if (nextDist >= maxDist)
            {
                nextDist = minDist + Mathf.Repeat(nextDist - minDist, span);
            }
            else if (nextDist < minDist)
            {
                nextDist = minDist;
            }

            EvaluatePose(nextDist, out Vector3 pos, out Quaternion rot);
            // Kenya LHT: siren pull-over moves gently LEFT (-right), AI-vs-AI overtake moves gently RIGHT (+right)
            float netLateral = lateralBypassOffset - sirenPullOverOffset;
            if (Mathf.Abs(netLateral) > 0.02f)
            {
                pos += (rot * Vector3.right) * netLateral;
            }

            // ZERO-PUSH SAFETY SHIELD: never step transform into the player's physical vehicle envelope!
            if (WouldOverlapPlayer(pos))
            {
                currentSpeed = 0f;
                return;
            }

            currentDist = nextDist;
            transform.position = pos;
            transform.rotation = Quaternion.Slerp(transform.rotation, rot, Time.deltaTime * 10f);

            // Spin wheels cleanly around the vehicle's true right axle regardless of imported mesh axis conventions
            if (wheelVisuals != null && wheelRadius > 0.05f && currentSpeed > 0.05f)
            {
                float deg = (currentSpeed * Time.deltaTime / wheelRadius) * Mathf.Rad2Deg;
                Vector3 axleWorld = transform.right;
                for (int i = 0; i < wheelVisuals.Length; i++)
                {
                    if (wheelVisuals[i] != null)
                        wheelVisuals[i].Rotate(axleWorld, deg, Space.World);
                }
            }
        }

        private void EvaluatePose(float dist, out Vector3 pos, out Quaternion rot)
        {
            // Binary search cumulative distance
            int lo = 0;
            int hi = cumulativeDist.Length - 1;
            while (lo < hi - 1)
            {
                int mid = (lo + hi) >> 1;
                if (cumulativeDist[mid] <= dist) lo = mid;
                else hi = mid;
            }

            float segLen = Mathf.Max(0.001f, cumulativeDist[hi] - cumulativeDist[lo]);
            float u = Mathf.Clamp01((dist - cumulativeDist[lo]) / segLen);
            pos = Vector3.Lerp(pathPoints[lo], pathPoints[hi], u);
            pos.y = KenyaNetworkData.GetElevationAt(pos.x, pos.z);

            // Preserve smooth 3D uphill/downhill highway slope pitch on Mai Mahiu, Salgaa, Timboroa & Sondu!
            Vector3 dir = pathPoints[hi] - pathPoints[lo];
            rot = dir.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(dir.normalized, Vector3.up) : transform.rotation;
        }

        private bool ShouldStopForNairobiLight()
        {
            var lights = StreetTrafficLight.ActiveIntersections;
            if (lights.Count == 0) return false;
            Vector3 p = transform.position;
            float dirSign = forwardDirection ? 1f : -1f;

            for (int i = 0; i < lights.Count; i++)
            {
                var l = lights[i];
                if (l == null || !l.IsMainBoulevardStop) continue;
                float stopZ = l.intersectionZ - dirSign * 10.5f;
                float distToStop = (stopZ - p.z) * dirSign;
                if (distToStop > 0.2f && distToStop < 11.5f) return true;
            }
            return false;
        }

        private static readonly RaycastHit[] CastHits = new RaycastHit[12];

        private bool CheckPlayerRespect(out float speedCap, out float pullOverMeters, out bool hardHalt)
        {
            speedCap = cruiseSpeed;
            pullOverMeters = 0f;
            hardHalt = false;

            var loop = StreetGameLoop.Instance;
            if (loop == null || loop.ActiveVehicle == null) return false;
            var player = loop.ActiveVehicle;

            Vector3 toPlayer = player.transform.position - transform.position;
            toPlayer.y = 0f;
            float distSqr = toPlayer.sqrMagnitude;
            if (distSqr > 60f * 60f) return false;

            float dist = Mathf.Sqrt(distSqr);
            bool yielding = false;

            // 1. Respect Kenya Police Emergency Siren ([G]): pull calmly onto left shoulder & slow down!
            if (player.IsPoliceSirenActive && dist < 52f)
            {
                pullOverMeters = 1.85f;
                speedCap = Mathf.Min(speedCap, 2.5f);
                yielding = true;
            }

            // 2. Respect Player Vehicle Lane Space: brake well in advance and NEVER shove/ram player!
            Vector3 local = transform.InverseTransformPoint(player.transform.position);
            float pHalfL = player.isCoach ? 7.4f : (player.jobRole == KenyaStreetVehicle.VehicleJobRole.PolicePatrol ? 2.8f : 4.4f);
            float pHalfW = player.isCoach ? 1.55f : 1.35f;
            float gapAhead = local.z - (vehicleHalfLength + pHalfL);
            float latAbs = Mathf.Abs(local.x);

            // If player is ahead in or encroaching into our lane, slow smoothly and stop respectfully 4.5m back!
            if (gapAhead > -2.5f && gapAhead < 22f && latAbs < pHalfW + 1.85f)
            {
                yielding = true;
                if (gapAhead <= 4.5f)
                {
                    speedCap = 0f;
                    hardHalt = true;
                }
                else
                {
                    float ratio = Mathf.InverseLerp(4.5f, 22f, gapAhead);
                    speedCap = Mathf.Min(speedCap, cruiseSpeed * ratio);
                }
            }
            return yielding;
        }

        private bool WouldOverlapPlayer(Vector3 candidatePos)
        {
            var loop = StreetGameLoop.Instance;
            if (loop == null || loop.ActiveVehicle == null) return false;
            var player = loop.ActiveVehicle;

            Vector3 delta = candidatePos - player.transform.position;
            delta.y = 0f;
            float pHalfL = player.isCoach ? 7.4f : (player.jobRole == KenyaStreetVehicle.VehicleJobRole.PolicePatrol ? 2.8f : 4.4f);
            float pHalfW = player.isCoach ? 1.55f : 1.35f;

            // Project candidate offset into player's local frame (accurate for angled turns & long buses!)
            float alongPlayer = Mathf.Abs(Vector3.Dot(delta, player.transform.forward));
            float acrossPlayer = Mathf.Abs(Vector3.Dot(delta, player.transform.right));

            return alongPlayer < (pHalfL + vehicleHalfLength + 1.1f) && acrossPlayer < (pHalfW + 1.35f);
        }

        private bool CheckAiObstacleAhead(out float nearestDist)
        {
            nearestDist = float.MaxValue;
            Vector3 fwd = transform.forward;
            Vector3 right = transform.right;
            Vector3 origin = transform.position + Vector3.up * 1.05f + fwd * (vehicleHalfLength + 0.45f);
            float checkRange = safeStopDistance * 1.75f;

            int count = Physics.BoxCastNonAlloc(
                origin,
                new Vector3(0.85f, 0.65f, 0.25f),
                fwd,
                CastHits,
                transform.rotation,
                checkRange,
                ~0,
                QueryTriggerInteraction.Ignore);

            bool found = false;
            for (int i = 0; i < count; i++)
            {
                var hit = CastHits[i];
                if (hit.transform == null || hit.transform.IsChildOf(transform)) continue;

                var otherTraffic = hit.collider.GetComponentInParent<KenyaSplineTraffic>();
                if (otherTraffic != null)
                {
                    if (otherTraffic == this) continue;
                    float align = Vector3.Dot(otherTraffic.transform.forward, fwd);
                    if (align < 0.45f) continue; // Ignore oncoming opposite-lane vehicles!
                    float lat = Mathf.Abs(Vector3.Dot(otherTraffic.transform.position - transform.position, right));
                    if (lat > 2.35f) continue;
                    if (hit.distance < nearestDist) { nearestDist = hit.distance; found = true; }
                }
            }

            return found;
        }
    }
}
