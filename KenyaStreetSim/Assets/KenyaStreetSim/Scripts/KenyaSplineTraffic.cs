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

                // Kenya Left-Hand Traffic: offset to the LEFT of travel direction
                Vector3 left = Vector3.Cross(fwd, Vector3.up).normalized;
                pathPoints[i] = centerLine[i] + left * laneOffsetMeters;

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
            float obstacleDist = float.MaxValue;
            bool obstacleAhead = !bypassing && CheckObstacleAhead(out obstacleDist);

            if (obstacleAhead)
            {
                float ratio = Mathf.InverseLerp(safeStopDistance * 0.35f, safeStopDistance * 1.7f, obstacleDist);
                targetSpeed = cruiseSpeed * ratio;
                if (obstacleDist < safeStopDistance * 0.50f) targetSpeed = 0f;
            }

            if (redLightStop)
            {
                targetSpeed = 0f;
            }

            // Anti-Traffic-Jam Deadlock Recovery: vehicles NEVER sit frozen blocking Kenyan highways!
            if (!redLightStop && targetSpeed < 2.2f)
            {
                stuckTimer += Time.deltaTime;
                if (stuckTimer > 2.6f)
                {
                    bypassing = true;
                    targetSpeed = Mathf.Max(6.5f, cruiseSpeed * 0.65f);
                }
                if (stuckTimer > 6.5f)
                {
                    // Cleanly skip past deadlock pocket so zero traffic jams ever accumulate
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

            float accelRate = targetSpeed < currentSpeed ? 14f : 7.5f;
            currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, accelRate * Time.deltaTime);

            currentDist += currentSpeed * Time.deltaTime;
            float span = Mathf.Max(20f, maxDist - minDist);
            if (currentDist >= maxDist)
            {
                currentDist = minDist + Mathf.Repeat(currentDist - minDist, span);
            }
            else if (currentDist < minDist)
            {
                currentDist = minDist;
            }

            EvaluatePose(currentDist, out Vector3 pos, out Quaternion rot);
            if (Mathf.Abs(lateralBypassOffset) > 0.02f)
            {
                pos += (rot * Vector3.right) * lateralBypassOffset;
            }
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

            Vector3 dir = pathPoints[hi] - pathPoints[lo];
            dir.y = 0f;
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

        private bool CheckObstacleAhead(out float nearestDist)
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

                // Only yield to vehicles actually in our SAME lane & direction (never freeze for oncoming traffic or roadside scenery!)
                var otherTraffic = hit.collider.GetComponentInParent<KenyaSplineTraffic>();
                if (otherTraffic != null)
                {
                    if (otherTraffic == this) continue;
                    float align = Vector3.Dot(otherTraffic.transform.forward, fwd);
                    if (align < 0.45f) continue; // Ignore oncoming opposite-lane vehicles!
                    float lat = Mathf.Abs(Vector3.Dot(otherTraffic.transform.position - transform.position, right));
                    if (lat > 2.35f) continue;   // Ignore adjacent lane!
                    if (hit.distance < nearestDist) { nearestDist = hit.distance; found = true; }
                    continue;
                }

                var playerVeh = hit.collider.GetComponentInParent<KenyaStreetVehicle>();
                if (playerVeh != null)
                {
                    float lat = Mathf.Abs(Vector3.Dot(playerVeh.transform.position - transform.position, right));
                    if (lat > 2.55f) continue;   // Player is in opposite lane or shoulder — keep flowing!
                    if (hit.distance < nearestDist) { nearestDist = hit.distance; found = true; }
                }
            }

            return found;
        }
    }
}
