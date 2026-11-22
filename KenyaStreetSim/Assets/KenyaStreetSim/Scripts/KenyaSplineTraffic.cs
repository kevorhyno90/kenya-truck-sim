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

        [Header("Driving")]
        public float cruiseSpeed = 14.5f;
        public float vehicleHalfLength = 2.6f;
        public float safeStopDistance = 8.5f;
        public float wheelRadius = 0.36f;
        public Transform[] wheelVisuals;

        private Vector3[] pathPoints;
        private float[] cumulativeDist;
        private float totalLength;
        private float currentDist;
        private float currentSpeed;

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

            currentDist = Mathf.Repeat(startNormalizedProgress, 1f) * totalLength;
            currentSpeed = cruiseSpeed * 0.75f;
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

            // Check obstacle ahead (player bus, other AI cars, pedestrians)
            if (CheckObstacleAhead(out float obstacleDist))
            {
                float ratio = Mathf.InverseLerp(safeStopDistance * 0.45f, safeStopDistance * 2.0f, obstacleDist);
                targetSpeed = cruiseSpeed * ratio;
                if (obstacleDist < safeStopDistance * 0.60f) targetSpeed = 0f;
            }

            // Obey Nairobi CBD intersection traffic light when on Highway 0
            if (highwayIndex == 0 && ShouldStopForNairobiLight())
            {
                targetSpeed = 0f;
            }

            float accelRate = targetSpeed < currentSpeed ? 14f : 6f;
            currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, accelRate * Time.deltaTime);

            currentDist += currentSpeed * Time.deltaTime;
            if (currentDist >= totalLength)
            {
                currentDist -= totalLength;
            }

            EvaluatePose(currentDist, out Vector3 pos, out Quaternion rot);
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

        private bool CheckObstacleAhead(out float nearestDist)
        {
            nearestDist = float.MaxValue;
            Vector3 fwd = transform.forward;
            Vector3 origin = transform.position + Vector3.up * 1.05f + fwd * (vehicleHalfLength + 0.4f);
            float checkRange = safeStopDistance * 2.0f;

            if (Physics.BoxCast(
                    origin,
                    new Vector3(0.95f, 0.7f, 0.25f),
                    fwd,
                    out RaycastHit hit,
                    transform.rotation,
                    checkRange,
                    ~0,
                    QueryTriggerInteraction.Ignore))
            {
                if (!hit.transform.IsChildOf(transform))
                {
                    nearestDist = hit.distance;
                    return true;
                }
            }

            return false;
        }
    }
}
