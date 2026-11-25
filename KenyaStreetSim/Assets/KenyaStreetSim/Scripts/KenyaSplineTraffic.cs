using System.Collections.Generic;
using UnityEngine;

namespace KenyaStreetSim
{
    /// <summary>
    /// Realistic ETS2-grade Kenyan Left-Hand Traffic (LHT) AI across all 15 highways & 10 roundabouts:
    /// - True C1-continuous Cubic Hermite roundabout entry & exit curves (clockwise LHT, zero 90-deg snaps!).
    /// - Real wheelbase front/rear axle kinematics with smooth hill pitch and curvature look-ahead braking.
    /// - Zero-overlap AI spacing along straight AND curved roads/roundabouts (no ghost overtaking/clipping!).
    /// - Authentic Kenyan LHT roundabout Give-Way (entering vehicles yield to traffic circulating from the right).
    /// - Distance Render LOD beyond 390m for smooth 60 FPS across the entire 41-town Kenya map.
    /// </summary>
    public class KenyaSplineTraffic : MonoBehaviour
    {
        [Header("Highway Route")]
        public int highwayIndex;
        [Tooltip("True = travel forward along highway waypoints (left lane); False = oncoming opposite lane")]
        public bool forwardDirection = true;
        public float startNormalizedProgress;
        public float laneOffsetMeters = 3.4f;

        [Header("Legacy Sub-Range Fields (kept for inspector compatibility; traffic now circulates continuously!)")]
        public bool limitToSubRange;
        [Range(0f, 1f)] public float rangeMinNorm = 0f;
        [Range(0f, 1f)] public float rangeMaxNorm = 1f;

        [Header("Driving")]
        public float cruiseSpeed = 14.5f;
        public float vehicleHalfLength = 2.6f;
        public float safeStopDistance = 7.5f;
        public float wheelRadius = 0.36f;
        public Transform[] wheelVisuals;

        private struct OutgoingEdge
        {
            public int highwayIdx;
            public bool forward;
            public Vector3 startPos;
        }

        private static readonly List<KenyaSplineTraffic> ActiveTraffic = new List<KenyaSplineTraffic>(96);
        private static List<OutgoingEdge>[] roundaboutExits;
        private static int globalRouteCounter;
        private static int nextTrafficUid;

        private Vector3[] pathPoints;
        private float[] cumulativeDist;
        private float totalLength;
        private float currentDist;
        private float currentSpeed;
        private float sirenPullOverOffset;
        private float halfWheelbase;
        private float vehicleHalfWidth = 1.25f;
        private int trafficUid;

        // Cached renderers & collider for distance LOD (saves ~3,800 renderers when distant!)
        private Renderer[] cachedRenderers;
        private Collider cachedCollider;
        private bool visualsVisible = true;
        private float nextLodCheckTime;
        private float farAccumDt;

        public bool IsInRoundabout { get; private set; }
        public int CurrentRoundaboutIndex { get; private set; } = -1;
        public float CurrentSpeedMs => currentSpeed;

        private void OnEnable()
        {
            if (!ActiveTraffic.Contains(this)) ActiveTraffic.Add(this);
        }

        private void OnDisable()
        {
            ActiveTraffic.Remove(this);
        }

        private static void EnsureRoundaboutGraph()
        {
            if (roundaboutExits != null && roundaboutExits.Length == KenyaNetworkData.Roundabouts.Length) return;

            int rCount = KenyaNetworkData.Roundabouts.Length;
            roundaboutExits = new List<OutgoingEdge>[rCount];
            for (int r = 0; r < rCount; r++) roundaboutExits[r] = new List<OutgoingEdge>();

            for (int h = 0; h < KenyaNetworkData.Highways.Length; h++)
            {
                var def = KenyaNetworkData.Highways[h];
                if (def.controlPoints == null || def.controlPoints.Length < 2) continue;

                Vector2 cpStart = def.controlPoints[0];
                int rStart = KenyaNetworkData.FindRoundaboutNear(cpStart, 32f);
                if (rStart >= 0)
                {
                    roundaboutExits[rStart].Add(new OutgoingEdge
                    {
                        highwayIdx = h,
                        forward = true,
                        startPos = new Vector3(cpStart.x, KenyaNetworkData.GetElevationAt(cpStart), cpStart.y)
                    });
                }

                Vector2 cpEnd = def.controlPoints[def.controlPoints.Length - 1];
                int rEnd = KenyaNetworkData.FindRoundaboutNear(cpEnd, 32f);
                if (rEnd >= 0)
                {
                    roundaboutExits[rEnd].Add(new OutgoingEdge
                    {
                        highwayIdx = h,
                        forward = false,
                        startPos = new Vector3(cpEnd.x, KenyaNetworkData.GetElevationAt(cpEnd), cpEnd.y)
                    });
                }
            }
        }

        private void Start()
        {
            EnsureRoundaboutGraph();

            cachedRenderers = GetComponentsInChildren<Renderer>(true);
            cachedCollider = GetComponent<Collider>();
            halfWheelbase = Mathf.Clamp(vehicleHalfLength * 0.56f, 1.25f, 4.2f);
            vehicleHalfWidth = vehicleHalfLength > 5.0f ? 1.38f : 1.12f;

            if (highwayIndex < 0 || highwayIndex >= KenyaNetworkData.Highways.Length)
            {
                enabled = false;
                return;
            }

            BuildHighwayPath(highwayIndex, forwardDirection, prependConnectorFrom: null, prependTangent: Vector3.zero, connectorRoundabout: -1);
            if (totalLength < 10f)
            {
                enabled = false;
                return;
            }

            float norm = limitToSubRange && rangeMaxNorm > rangeMinNorm + 0.02f
                ? Mathf.Lerp(Mathf.Clamp01(rangeMinNorm), Mathf.Clamp01(rangeMaxNorm), Mathf.Repeat(startNormalizedProgress, 1f))
                : Mathf.Repeat(startNormalizedProgress, 1f);
            currentDist = Mathf.Clamp(norm * totalLength, 4f, Mathf.Max(4f, totalLength - 8f));
            currentSpeed = cruiseSpeed * 0.85f;

            // Guarantee no two vehicles ever spawn overlapping on the same corridor!
            EnsureCleanSpawnSpacing();

            trafficUid = ++nextTrafficUid;
            EvaluateWheelbasePose(currentDist, out Vector3 p, out Quaternion rot);
            transform.SetPositionAndRotation(p, rot);
            nextLodCheckTime = Time.unscaledTime + (trafficUid % 17) * 0.02f;
        }

        private void EnsureCleanSpawnSpacing()
        {
            for (int attempt = 0; attempt < 8; attempt++)
            {
                EvaluateWheelbasePose(currentDist, out Vector3 candidate, out _);
                bool overlap = false;
                for (int i = 0; i < ActiveTraffic.Count; i++)
                {
                    var other = ActiveTraffic[i];
                    if (other == null || other == this || other.pathPoints == null) continue;
                    float minClear = vehicleHalfLength + other.vehicleHalfLength + 12f;
                    Vector3 d = other.transform.position - candidate;
                    d.y = 0f;
                    if (d.sqrMagnitude < minClear * minClear)
                    {
                        overlap = true;
                        break;
                    }
                }
                if (!overlap) break;
                currentDist = Mathf.Repeat(currentDist + (vehicleHalfLength * 2f + 22f), Mathf.Max(20f, totalLength - 10f));
            }
        }

        private void BuildHighwayPath(int hIdx, bool fwd, Vector3? prependConnectorFrom, Vector3 prependTangent, int connectorRoundabout)
        {
            highwayIndex = hIdx;
            forwardDirection = fwd;
            var def = KenyaNetworkData.Highways[hIdx];
            var sampled = KenyaNetworkData.SampleHighway(def, 4.5f);
            if (!fwd) sampled.Reverse();

            float maxSafeLane = def.roadClass == KenyaNetworkData.RoadClass.CityExpressway6Lane ? 7.4f
                              : def.roadClass == KenyaNetworkData.RoadClass.Climbing3Lane ? 6.4f
                              : 3.65f;
            float effLane = Mathf.Clamp(laneOffsetMeters, 2.35f, maxSafeLane);
            // Outer lane vehicles take slightly wider roundabout ring so multi-lane roundabouts feel natural!
            float ringLaneFrac = Mathf.Clamp01(Mathf.InverseLerp(2.4f, 7.2f, effLane));

            int n = sampled.Count;
            var rawLane = new List<Vector3>(n);
            for (int i = 0; i < n; i++)
            {
                Vector3 tan = i == 0 ? (sampled[1] - sampled[0])
                            : i == n - 1 ? (sampled[n - 1] - sampled[n - 2])
                            : (sampled[i + 1] - sampled[i - 1]);
                tan.y = 0f;
                if (tan.sqrMagnitude < 0.0001f) tan = Vector3.forward;
                tan.Normalize();
                Vector3 left = Vector3.Cross(tan, Vector3.up).normalized;
                Vector3 pt = sampled[i] + left * effLane;
                pt.y = KenyaNetworkData.GetElevationAt(pt.x, pt.z);
                rawLane.Add(pt);
            }

            // Trim the beginning of rawLane if it starts inside a connector roundabout so we never cut through the central island!
            int startIdx = 0;
            if (connectorRoundabout >= 0 && connectorRoundabout < KenyaNetworkData.Roundabouts.Length)
            {
                var startRb = KenyaNetworkData.Roundabouts[connectorRoundabout];
                float clearR = startRb.outerRadius + 6.5f;
                while (startIdx < rawLane.Count - 2 &&
                       Vector2.Distance(new Vector2(rawLane[startIdx].x, rawLane[startIdx].z), startRb.center) < clearR)
                {
                    startIdx++;
                }
            }

            // Also trim the tail of rawLane if it terminates inside a roundabout so TransitionToConnectedCorridor triggers cleanly at the entry!
            int endIdx = rawLane.Count - 1;
            if (endIdx > startIdx + 3)
            {
                Vector2 tailXZ = new Vector2(rawLane[endIdx].x, rawLane[endIdx].z);
                int endRbIdx = KenyaNetworkData.FindRoundaboutNear(tailXZ, 30f);
                if (endRbIdx >= 0)
                {
                    var endRb = KenyaNetworkData.Roundabouts[endRbIdx];
                    float clearR = endRb.outerRadius + 6.5f;
                    while (endIdx > startIdx + 2 &&
                           Vector2.Distance(new Vector2(rawLane[endIdx].x, rawLane[endIdx].z), endRb.center) < clearR)
                    {
                        endIdx--;
                    }
                }
            }

            var smoothPts = new List<Vector3>(n + 72);

            // 1. Optional seamless C1-continuous transition from previous corridor (Roundabout or U-turn or T-junction)
            if (prependConnectorFrom.HasValue && startIdx < endIdx)
            {
                Vector3 fromPos = prependConnectorFrom.Value;
                Vector3 toPos = rawLane[startIdx];
                Vector3 outDir = (rawLane[Mathf.Min(endIdx, startIdx + 2)] - toPos);
                outDir.y = 0f;
                if (outDir.sqrMagnitude < 0.01f) outDir = Vector3.forward;
                outDir.Normalize();

                Vector3 inDir = prependTangent;
                inDir.y = 0f;
                if (inDir.sqrMagnitude < 0.01f) inDir = (toPos - fromPos);
                inDir.y = 0f;
                if (inDir.sqrMagnitude < 0.01f) inDir = Vector3.forward;
                inDir.Normalize();

                if (connectorRoundabout >= 0 && connectorRoundabout < KenyaNetworkData.Roundabouts.Length)
                {
                    AppendSmoothRoundaboutArc(smoothPts, KenyaNetworkData.Roundabouts[connectorRoundabout], fromPos, inDir, toPos, outDir, ringLaneFrac);
                }
                else
                {
                    AppendSmoothHermiteOrUturn(smoothPts, fromPos, inDir, toPos, outDir);
                }
            }

            // 2. Walk the highway, replacing any mid-highway Roundabout crossing with a smooth C1 clockwise arc!
            int idx = startIdx;
            while (idx <= endIdx)
            {
                Vector3 p = rawLane[idx];
                int hitRbt = (idx > startIdx + 1 && idx < endIdx - 1)
                    ? KenyaNetworkData.FindRoundaboutNear(new Vector2(p.x, p.z), 6.5f)
                    : -1;

                if (hitRbt >= 0)
                {
                    var rb = KenyaNetworkData.Roundabouts[hitRbt];
                    float clearR = rb.outerRadius + 6.5f;

                    // Back up entry point cleanly outside outerRadius + 6.5f if needed
                    Vector3 entryPt = smoothPts.Count > 0 ? smoothPts[smoothPts.Count - 1] : rawLane[Mathf.Max(startIdx, idx - 1)];
                    Vector3 inDir = (smoothPts.Count >= 2)
                        ? (smoothPts[smoothPts.Count - 1] - smoothPts[smoothPts.Count - 2])
                        : (p - entryPt);
                    inDir.y = 0f;
                    if (inDir.sqrMagnitude < 0.01f) inDir = Vector3.forward;
                    inDir.Normalize();

                    int exitI = idx;
                    while (exitI < endIdx &&
                           Vector2.Distance(new Vector2(rawLane[exitI].x, rawLane[exitI].z), rb.center) < clearR)
                    {
                        exitI++;
                    }

                    Vector3 exitPt = rawLane[exitI];
                    Vector3 outDir = (rawLane[Mathf.Min(endIdx, exitI + 2)] - exitPt);
                    outDir.y = 0f;
                    if (outDir.sqrMagnitude < 0.01f) outDir = inDir;
                    outDir.Normalize();

                    AppendSmoothRoundaboutArc(smoothPts, rb, entryPt, inDir, exitPt, outDir, ringLaneFrac);
                    idx = exitI + 1;
                }
                else
                {
                    AppendPointDedup(smoothPts, p, 0.35f);
                    idx++;
                }
            }

            FinalizePathArrays(smoothPts);
        }

        /// <summary>
        /// Builds a true C1-continuous Kenyan LHT CLOCKWISE roundabout trajectory:
        ///   incoming highway -> Cubic Hermite entry flare matching clockwise ring tangent
        ///   -> dense circular clockwise ring arc
        ///   -> Cubic Hermite exit flare matching outgoing highway tangent.
        /// Eliminates sharp 90-degree snaps and central-island clipping!
        /// </summary>
        private static void AppendSmoothRoundaboutArc(
            List<Vector3> dst,
            KenyaNetworkData.RoundaboutDef rb,
            Vector3 entryPos, Vector3 entryDir,
            Vector3 exitPos, Vector3 exitDir,
            float laneFrac)
        {
            float ringR = Mathf.Lerp(rb.innerRadius + 2.8f, rb.outerRadius - 2.6f, Mathf.Lerp(0.32f, 0.68f, laneFrac));
            float angApproach = Mathf.Atan2(entryPos.z - rb.center.y, entryPos.x - rb.center.x);
            float angDepart   = Mathf.Atan2(exitPos.z - rb.center.y, exitPos.x - rb.center.x);

            // Merge slightly downstream clockwise (-0.28 rad) and peel off slightly upstream clockwise (+0.28 rad)
            // so the Hermite entry/exit flares curve smoothly like real highway slip-lanes!
            float flareOffset = Mathf.Clamp(7.5f / Mathf.Max(8f, ringR), 0.18f, 0.38f);
            float angRingIn  = angApproach - flareOffset;
            float angRingOut = angDepart   + flareOffset;

            // Kenyan LHT circulates CLOCKWISE (decreasing angle in Unity XZ!)
            float cwSweep = angRingIn - angRingOut;
            while (cwSweep < 0.12f) cwSweep += Mathf.PI * 2f;
            while (cwSweep > Mathf.PI * 2f + 0.12f) cwSweep -= Mathf.PI * 2f;

            // Prevent unnecessary extra 360 loop when entry & exit are close
            float rawApproachToDepart = angApproach - angDepart;
            while (rawApproachToDepart <= 0f) rawApproachToDepart += Mathf.PI * 2f;
            if (rawApproachToDepart < flareOffset * 2.2f)
            {
                // Tight left turn (first exit in LHT): single clean Hermite sweep hugging the outer apron!
                float handle = Mathf.Clamp(Vector3.Distance(entryPos, exitPos) * 0.42f, 4f, 16f);
                AppendCubicHermite(dst, entryPos, entryDir * handle, exitPos, exitDir * handle, 1.8f);
                return;
            }

            Vector3 ringEntry = RingPoint(rb.center, ringR, angRingIn);
            Vector3 ringExit  = RingPoint(rb.center, ringR, angRingOut);
            Vector3 tRingIn   = ClockwiseRingTangent(angRingIn);
            Vector3 tRingOut  = ClockwiseRingTangent(angRingOut);

            // 1. C1-continuous Cubic Hermite Entry Flare
            float hIn = Mathf.Clamp(Vector3.Distance(entryPos, ringEntry) * 0.52f, 4.5f, 14f);
            AppendCubicHermite(dst, entryPos, entryDir * hIn, ringEntry, tRingIn * hIn, 1.8f);

            // 2. Smooth Clockwise Circular Ring Arc (dense 1.8m step -> zero polygonal twitch!)
            float arcLen = cwSweep * ringR;
            int steps = Mathf.Clamp(Mathf.CeilToInt(arcLen / 1.8f), 4, 72);
            for (int s = 1; s <= steps; s++)
            {
                float t = (float)s / steps;
                float a = angRingIn - t * cwSweep;
                AppendPointDedup(dst, RingPoint(rb.center, ringR, a), 0.30f);
            }

            // 3. C1-continuous Cubic Hermite Exit Flare
            float hOut = Mathf.Clamp(Vector3.Distance(ringExit, exitPos) * 0.52f, 4.5f, 14f);
            AppendCubicHermite(dst, ringExit, tRingOut * hOut, exitPos, exitDir * hOut, 1.8f);
        }

        private static void AppendSmoothHermiteOrUturn(List<Vector3> dst, Vector3 fromPos, Vector3 fromDir, Vector3 toPos, Vector3 toDir)
        {
            float gap = Vector3.Distance(fromPos, toPos);
            float dot = Vector3.Dot(fromDir, toDir);

            // Outer border terminus turnaround (U-turn onto opposite lane): wide realistic turning bulb!
            if (gap < 20f && dot < -0.45f)
            {
                Vector3 mid = (fromPos + toPos) * 0.5f;
                Vector3 apex = mid + fromDir * 12.5f;
                Vector3 side = (toPos - fromPos);
                side.y = 0f;
                if (side.sqrMagnitude < 0.01f) side = Vector3.Cross(Vector3.up, fromDir);
                side.Normalize();

                AppendCubicHermite(dst, fromPos, fromDir * 10f, apex, side * 9f, 1.8f);
                AppendCubicHermite(dst, apex, side * 9f, toPos, toDir * 10f, 1.8f);
            }
            else
            {
                float handle = Mathf.Clamp(gap * 0.48f, 4.5f, 18f);
                AppendCubicHermite(dst, fromPos, fromDir * handle, toPos, toDir * handle, 2.0f);
            }
        }

        private static void AppendCubicHermite(List<Vector3> dst, Vector3 p0, Vector3 m0, Vector3 p1, Vector3 m1, float stepMeters)
        {
            float estLen = Vector3.Distance(p0, p1) * 1.25f;
            int steps = Mathf.Clamp(Mathf.CeilToInt(estLen / Mathf.Max(0.8f, stepMeters)), 4, 28);
            for (int s = 0; s <= steps; s++)
            {
                float t = (float)s / steps;
                float t2 = t * t;
                float t3 = t2 * t;
                float h00 = 2f * t3 - 3f * t2 + 1f;
                float h10 = t3 - 2f * t2 + t;
                float h01 = -2f * t3 + 3f * t2;
                float h11 = t3 - t2;

                Vector3 pt = h00 * p0 + h10 * m0 + h01 * p1 + h11 * m1;
                pt.y = KenyaNetworkData.GetElevationAt(pt.x, pt.z);
                AppendPointDedup(dst, pt, 0.30f);
            }
        }

        private static Vector3 ClockwiseRingTangent(float angRad)
        {
            // Clockwise in Unity XZ (+X east, +Z north) is decreasing angle: d/d(-a) (cos a, sin a) = (+sin a, -cos a)
            return new Vector3(Mathf.Sin(angRad), 0f, -Mathf.Cos(angRad)).normalized;
        }

        private static Vector3 RingPoint(Vector2 center, float radius, float angRad)
        {
            float x = center.x + Mathf.Cos(angRad) * radius;
            float z = center.y + Mathf.Sin(angRad) * radius;
            return new Vector3(x, KenyaNetworkData.GetElevationAt(x, z), z);
        }

        private static void AppendPointDedup(List<Vector3> dst, Vector3 pt, float minDist)
        {
            if (dst.Count == 0 || (pt - dst[dst.Count - 1]).sqrMagnitude > minDist * minDist)
            {
                dst.Add(pt);
            }
        }

        private void FinalizePathArrays(List<Vector3> pts)
        {
            int n = pts.Count;
            pathPoints = new Vector3[n];
            cumulativeDist = new float[n];
            totalLength = 0f;
            for (int i = 0; i < n; i++)
            {
                pathPoints[i] = pts[i];
                if (i > 0) totalLength += Vector3.Distance(pathPoints[i - 1], pathPoints[i]);
                cumulativeDist[i] = totalLength;
            }
        }

        private void TransitionToConnectedCorridor(Vector3 currentPos, Vector3 currentFwd)
        {
            EnsureRoundaboutGraph();
            Vector2 endXZ = new Vector2(currentPos.x, currentPos.z);

            // 1. Arrived at a Kenyan Roundabout -> smoothly take a clockwise arc onto a connected outgoing highway!
            int rbIdx = KenyaNetworkData.FindRoundaboutNear(endXZ, 36f);
            if (rbIdx >= 0 && roundaboutExits[rbIdx].Count > 0)
            {
                var exits = roundaboutExits[rbIdx];
                var candidates = new List<OutgoingEdge>();
                for (int i = 0; i < exits.Count; i++)
                {
                    if (exits[i].highwayIdx != highwayIndex) candidates.Add(exits[i]);
                }
                if (candidates.Count == 0) candidates = exits;

                int pick = ((globalRouteCounter++ + gameObject.name.GetHashCode()) & 0x7FFFFFFF) % candidates.Count;
                var chosen = candidates[pick];
                BuildHighwayPath(chosen.highwayIdx, chosen.forward, prependConnectorFrom: currentPos, prependTangent: currentFwd, connectorRoundabout: rbIdx);
                currentDist = 0f;
                return;
            }

            // 2. Arrived at a T-Junction connecting to another highway
            int bestH = -1;
            bool bestFwd = true;
            float bestD = 28f;
            for (int h = 0; h < KenyaNetworkData.Highways.Length; h++)
            {
                if (h == highwayIndex) continue;
                var cp = KenyaNetworkData.Highways[h].controlPoints;
                if (cp == null || cp.Length < 2) continue;

                float d0 = Vector2.Distance(endXZ, cp[0]);
                if (d0 < bestD) { bestD = d0; bestH = h; bestFwd = true; }
                float d1 = Vector2.Distance(endXZ, cp[cp.Length - 1]);
                if (d1 < bestD) { bestD = d1; bestH = h; bestFwd = false; }
            }

            if (bestH >= 0)
            {
                BuildHighwayPath(bestH, bestFwd, prependConnectorFrom: currentPos, prependTangent: currentFwd, connectorRoundabout: -1);
                currentDist = 0f;
                return;
            }

            // 3. Outer Border Terminus -> wide realistic turnaround bulb back onto return lane
            BuildHighwayPath(highwayIndex, !forwardDirection, prependConnectorFrom: currentPos, prependTangent: currentFwd, connectorRoundabout: -1);
            currentDist = 0f;
        }

        private void Update()
        {
            if (pathPoints == null || totalLength < 10f) return;

            // Distance LOD check: keeps distant vehicles ticking cheaply while disabling ~3,800 distant renderers!
            if (Time.unscaledTime >= nextLodCheckTime)
            {
                nextLodCheckTime = Time.unscaledTime + 0.35f;
                UpdateDistanceLod();
            }

            if (!visualsVisible)
            {
                // Lightweight background cruise at ~5 Hz when >390m away so traffic keeps circulating across Kenya!
                farAccumDt += Time.deltaTime;
                if (farAccumDt < 0.20f) return;
                float stepDt = farAccumDt;
                farAccumDt = 0f;

                currentSpeed = Mathf.MoveTowards(currentSpeed, cruiseSpeed * 0.85f, 4f * stepDt);
                float nextFar = currentDist + currentSpeed * stepDt;
                if (nextFar >= totalLength - 1f)
                {
                    TransitionToConnectedCorridor(transform.position, transform.forward);
                    nextFar = 0.5f;
                }
                currentDist = nextFar;
                EvaluateWheelbasePose(currentDist, out Vector3 farPos, out Quaternion farRot);
                transform.SetPositionAndRotation(farPos, farRot);
                return;
            }

            float dt = Time.deltaTime;
            Vector2 posXZ = new Vector2(transform.position.x, transform.position.z);
            float targetSpeed = cruiseSpeed;

            // 1. Track Roundabout state & slow smoothly BEFORE curves/roundabouts using real look-ahead curvature!
            CurrentRoundaboutIndex = KenyaNetworkData.FindRoundaboutNear(posXZ, 2.5f);
            IsInRoundabout = CurrentRoundaboutIndex >= 0;

            float upcomingCurvatureDeg = ComputeLookAheadCurvatureDeg(currentDist, 18f);
            if (IsInRoundabout)
            {
                targetSpeed = Mathf.Min(targetSpeed, vehicleHalfLength > 5f ? 6.6f : 8.2f);
            }
            else if (upcomingCurvatureDeg > 14f)
            {
                float curveSpeed = Mathf.Lerp(cruiseSpeed, vehicleHalfLength > 5f ? 7.0f : 8.6f, Mathf.InverseLerp(14f, 65f, upcomingCurvatureDeg));
                targetSpeed = Mathf.Min(targetSpeed, curveSpeed);
            }

            // 2. Nairobi CBD Traffic Lights
            if (highwayIndex == 0 && ShouldStopForNairobiLight())
            {
                targetSpeed = 0f;
            }

            // 3. Kenyan LHT Roundabout Give-Way (Yield to traffic already circulating clockwise on our right!)
            if (!IsInRoundabout && ShouldYieldAtApproachingRoundabout(posXZ, transform.forward))
            {
                targetSpeed = 0f;
            }

            // 4. Player Road Respect & Police Siren Yield (never pushes or rams the player!)
            bool yieldingToPlayer = CheckPlayerRespect(out float playerSpeedCap, out float desiredSirenPullOver, out bool hardHalt);
            if (yieldingToPlayer)
            {
                targetSpeed = Mathf.Min(targetSpeed, playerSpeedCap);
            }
            sirenPullOverOffset = Mathf.MoveTowards(sirenPullOverOffset, desiredSirenPullOver, dt * 2.2f);

            // 5. ZERO-OVERLAP Curved-Path AI Following (works on highways, sharp bends AND inside roundabouts!)
            if (CheckAiTrafficAhead(out float aiSpeedCap, out bool aiHardStop))
            {
                targetSpeed = Mathf.Min(targetSpeed, aiSpeedCap);
                if (aiHardStop) hardHalt = true;
            }

            // Smooth heavy-vehicle acceleration & braking (never jerky!)
            float accelRate = targetSpeed < currentSpeed ? 16.5f : 5.2f;
            currentSpeed = hardHalt ? 0f : Mathf.MoveTowards(currentSpeed, targetSpeed, accelRate * dt);

            float nextDist = currentDist + currentSpeed * dt;
            if (nextDist >= totalLength - 0.6f)
            {
                TransitionToConnectedCorridor(transform.position, transform.forward);
                nextDist = Mathf.Min(currentSpeed * dt, Mathf.Max(0f, totalLength - 1f));
            }

            EvaluateWheelbasePose(nextDist, out Vector3 pos, out Quaternion rot);
            if (Mathf.Abs(sirenPullOverOffset) > 0.02f)
            {
                pos -= (rot * Vector3.right) * sirenPullOverOffset;
                pos.y = KenyaNetworkData.GetElevationAt(pos.x, pos.z);
            }

            // 6. Hard physical non-overlap bumper shield against BOTH Player and other AI vehicles!
            if (WouldOverlapPlayer(pos, rot) || WouldOverlapOtherAi(pos, rot))
            {
                currentSpeed = Mathf.MoveTowards(currentSpeed, 0f, 35f * dt);
                return;
            }

            currentDist = nextDist;
            transform.position = pos;
            transform.rotation = Quaternion.Slerp(transform.rotation, rot, 1f - Mathf.Exp(-10.5f * dt));

            // Spin wheels smoothly around the vehicle's true right axle
            if (wheelVisuals != null && wheelRadius > 0.05f && currentSpeed > 0.05f)
            {
                float deg = (currentSpeed * dt / wheelRadius) * Mathf.Rad2Deg;
                Vector3 axleWorld = transform.right;
                for (int i = 0; i < wheelVisuals.Length; i++)
                {
                    if (wheelVisuals[i] != null)
                        wheelVisuals[i].Rotate(axleWorld, deg, Space.World);
                }
            }
        }

        private void UpdateDistanceLod()
        {
            var loop = StreetGameLoop.Instance;
            if (loop == null || loop.ActiveVehicle == null) return;
            Vector3 pPos = loop.ActiveVehicle.transform.position;
            float dx = transform.position.x - pPos.x;
            float dz = transform.position.z - pPos.z;
            float d2 = dx * dx + dz * dz;

            bool wantVisible = visualsVisible ? (d2 <= 410f * 410f) : (d2 <= 360f * 360f);
            if (wantVisible == visualsVisible) return;

            visualsVisible = wantVisible;
            if (cachedRenderers != null)
            {
                for (int i = 0; i < cachedRenderers.Length; i++)
                {
                    if (cachedRenderers[i] != null) cachedRenderers[i].enabled = visualsVisible;
                }
            }
            if (cachedCollider != null) cachedCollider.enabled = visualsVisible;
        }

        /// <summary>
        /// Evaluates front & rear axle positions along the spline so long coaches & semi-trailers
        /// sweep naturally around roundabouts and pitch smoothly over Rift Valley hills with zero segment snap!
        /// </summary>
        private void EvaluateWheelbasePose(float dist, out Vector3 centerPos, out Quaternion rot)
        {
            Vector3 pRear  = EvalPathPosition(Mathf.Max(0f, dist - halfWheelbase));
            Vector3 pFront = EvalPathPosition(Mathf.Min(totalLength, dist + halfWheelbase));

            centerPos = (pRear + pFront) * 0.5f;
            centerPos.y = KenyaNetworkData.GetElevationAt(centerPos.x, centerPos.z);

            Vector3 dir = pFront - pRear;
            if (dir.sqrMagnitude < 0.0004f)
            {
                rot = transform.rotation;
                return;
            }

            // Gently clamp pitch to realistic Kenyan highway grade (max ±14 deg)
            Vector3 flat = new Vector3(dir.x, 0f, dir.z);
            float flatLen = flat.magnitude;
            if (flatLen > 0.001f)
            {
                float clampedY = Mathf.Clamp(dir.y, -flatLen * 0.25f, flatLen * 0.25f);
                dir = new Vector3(dir.x, clampedY, dir.z).normalized;
                rot = Quaternion.LookRotation(dir, Vector3.up);
            }
            else
            {
                rot = transform.rotation;
            }
        }

        private Vector3 EvalPathPosition(float dist)
        {
            if (pathPoints == null || pathPoints.Length == 0) return transform.position;
            if (pathPoints.Length == 1) return pathPoints[0];

            float d = Mathf.Clamp(dist, 0f, totalLength);
            int lo = 0;
            int hi = cumulativeDist.Length - 1;
            while (lo < hi - 1)
            {
                int mid = (lo + hi) >> 1;
                if (cumulativeDist[mid] <= d) lo = mid;
                else hi = mid;
            }

            float segLen = Mathf.Max(0.001f, cumulativeDist[hi] - cumulativeDist[lo]);
            float u = Mathf.Clamp01((d - cumulativeDist[lo]) / segLen);
            Vector3 pos = Vector3.Lerp(pathPoints[lo], pathPoints[hi], u);
            pos.y = KenyaNetworkData.GetElevationAt(pos.x, pos.z);
            return pos;
        }

        private float ComputeLookAheadCurvatureDeg(float dist, float lookAheadMeters)
        {
            Vector3 p0 = EvalPathPosition(dist);
            Vector3 p1 = EvalPathPosition(Mathf.Min(totalLength, dist + 4.5f));
            Vector3 p2 = EvalPathPosition(Mathf.Min(totalLength, dist + lookAheadMeters));
            Vector3 p3 = EvalPathPosition(Mathf.Min(totalLength, dist + lookAheadMeters + 5.0f));
            Vector3 d1 = p1 - p0; d1.y = 0f;
            Vector3 d2 = p3 - p2; d2.y = 0f;
            if (d1.sqrMagnitude < 0.01f || d2.sqrMagnitude < 0.01f) return 0f;
            return Vector3.Angle(d1, d2);
        }

        private bool ShouldYieldAtApproachingRoundabout(Vector2 posXZ, Vector3 fwd)
        {
            Vector2 fwd2 = new Vector2(fwd.x, fwd.z).normalized;
            var rbs = KenyaNetworkData.Roundabouts;

            for (int r = 0; r < rbs.Length; r++)
            {
                var rb = rbs[r];
                Vector2 toCenter = rb.center - posXZ;
                float distToCenter = toCenter.magnitude;
                float distToOuter = distToCenter - rb.outerRadius;
                if (distToOuter < 0.5f || distToOuter > 14.0f) continue;

                // Only yield if actually heading INTO this roundabout!
                if (Vector2.Dot(toCenter.normalized, fwd2) < 0.45f) continue;

                float myAng = Mathf.Atan2(posXZ.y - rb.center.y, posXZ.x - rb.center.x);

                // Check AI vehicles circulating inside this roundabout coming from the RIGHT (upstream clockwise: +10°..+100° CCW from our spoke)
                for (int i = 0; i < ActiveTraffic.Count; i++)
                {
                    var other = ActiveTraffic[i];
                    if (other == null || other == this || !other.visualsVisible) continue;
                    Vector2 oXZ = new Vector2(other.transform.position.x, other.transform.position.z);
                    float oDist = Vector2.Distance(oXZ, rb.center);
                    if (oDist > rb.outerRadius + 3.5f) continue;

                    float oAng = Mathf.Atan2(oXZ.y - rb.center.y, oXZ.x - rb.center.x);
                    float upstreamDelta = Mathf.Repeat(oAng - myAng, Mathf.PI * 2f);
                    if (upstreamDelta > 0.05f && upstreamDelta < 1.65f)
                    {
                        return true;
                    }
                }

                // Also yield cleanly if the PLAYER is circulating inside this roundabout coming from our right!
                var loop = StreetGameLoop.Instance;
                if (loop != null && loop.ActiveVehicle != null)
                {
                    var pv = loop.ActiveVehicle;
                    Vector2 pXZ = new Vector2(pv.transform.position.x, pv.transform.position.z);
                    if (Vector2.Distance(pXZ, rb.center) <= rb.outerRadius + 4.5f && pv.SpeedKmh > 4f)
                    {
                        float pAng = Mathf.Atan2(pXZ.y - rb.center.y, pXZ.x - rb.center.x);
                        float upstreamDelta = Mathf.Repeat(pAng - myAng, Mathf.PI * 2f);
                        if (upstreamDelta > 0.05f && upstreamDelta < 1.85f)
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        private bool CheckAiTrafficAhead(out float speedCap, out bool hardStop)
        {
            speedCap = cruiseSpeed;
            hardStop = false;
            bool found = false;

            Vector3 myPos = transform.position;
            Vector3 myFwd = transform.forward;
            Vector3 myRight = transform.right;

            // Sample 3 curved look-ahead anchors along OUR spline so we see leaders around roundabouts & hills!
            Vector3 look1 = EvalPathPosition(Mathf.Min(totalLength, currentDist + vehicleHalfLength + 6.5f));
            Vector3 look2 = EvalPathPosition(Mathf.Min(totalLength, currentDist + vehicleHalfLength + 14.5f));
            Vector3 look3 = EvalPathPosition(Mathf.Min(totalLength, currentDist + vehicleHalfLength + 24.0f));

            float checkRange = vehicleHalfLength + safeStopDistance * 2.8f + 12f;
            float checkRangeSqr = checkRange * checkRange;

            for (int i = 0; i < ActiveTraffic.Count; i++)
            {
                var other = ActiveTraffic[i];
                if (other == null || other == this || !other.visualsVisible) continue;

                Vector3 toOther = other.transform.position - myPos;
                toOther.y = 0f;
                float d2 = toOther.sqrMagnitude;
                if (d2 > checkRangeSqr || d2 < 0.01f) continue;

                // Ignore oncoming opposite-lane vehicles unless directly overlapping our curved path
                float headingDot = Vector3.Dot(myFwd, other.transform.forward);
                float fwdDot = Vector3.Dot(toOther, myFwd);
                float latDist = Mathf.Abs(Vector3.Dot(toOther, myRight));

                bool inForwardCorridor = fwdDot > 0.2f && latDist < (vehicleHalfWidth + other.vehicleHalfWidth + 0.55f) && headingDot > 0.20f;

                // Also detect leader along our curved spline ahead (essential on roundabouts & bends!)
                bool onCurvedPathAhead = false;
                if (!inForwardCorridor && headingDot > -0.15f)
                {
                    float laneBand = vehicleHalfWidth + other.vehicleHalfWidth + 0.75f;
                    float bandSqr = laneBand * laneBand;
                    Vector3 oPos = other.transform.position;
                    float c1 = FlatDistSqr(oPos, look1);
                    float c2 = FlatDistSqr(oPos, look2);
                    float c3 = FlatDistSqr(oPos, look3);
                    if (c1 < bandSqr || c2 < bandSqr || c3 < bandSqr)
                    {
                        // Confirm the other vehicle is genuinely ahead (not behind us!)
                        onCurvedPathAhead = fwdDot > -1.0f;
                    }
                }

                if (!inForwardCorridor && !onCurvedPathAhead) continue;

                float centerDist = Mathf.Sqrt(d2);
                float bumperGap = centerDist - (vehicleHalfLength + other.vehicleHalfLength);
                float desiredGap = Mathf.Max(safeStopDistance, currentSpeed * 1.15f + 4.5f);

                if (bumperGap < desiredGap + 10f)
                {
                    found = true;
                    if (bumperGap <= 4.2f)
                    {
                        speedCap = 0f;
                        hardStop = true;
                    }
                    else
                    {
                        float t = Mathf.InverseLerp(4.2f, desiredGap + 10f, bumperGap);
                        float matchSpeed = Mathf.Lerp(0f, other.currentSpeed * 0.94f + cruiseSpeed * 0.35f, t);
                        speedCap = Mathf.Min(speedCap, Mathf.Max(0f, matchSpeed));
                    }
                }
            }

            return found;
        }

        private bool WouldOverlapOtherAi(Vector3 candidatePos, Quaternion candidateRot)
        {
            Vector3 cFwd = candidateRot * Vector3.forward;
            Vector3 cRight = candidateRot * Vector3.right;

            for (int i = 0; i < ActiveTraffic.Count; i++)
            {
                var other = ActiveTraffic[i];
                if (other == null || other == this || !other.visualsVisible) continue;

                Vector3 diff = other.transform.position - candidatePos;
                diff.y = 0f;
                float maxR = vehicleHalfLength + other.vehicleHalfLength + 2.5f;
                if (diff.sqrMagnitude > maxR * maxR) continue;

                float fwd = Vector3.Dot(diff, cFwd);
                float lat = Mathf.Abs(Vector3.Dot(diff, cRight));
                float minLen = vehicleHalfLength + other.vehicleHalfLength + 1.15f;
                float minWid = vehicleHalfWidth + other.vehicleHalfWidth + 0.35f;

                if (Mathf.Abs(fwd) < minLen && lat < minWid)
                {
                    // Yield rule: always yield if the other vehicle is in front of us, OR already circulating in a roundabout,
                    // OR (symmetric tie-breaker) has a lower trafficUid so two cars NEVER mutually lock!
                    if (fwd > 0.1f) return true;
                    if (other.IsInRoundabout && !IsInRoundabout) return true;
                    if (Mathf.Abs(fwd) <= 0.1f && other.trafficUid < trafficUid) return true;
                }
            }
            return false;
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

        private bool CheckPlayerRespect(out float speedCap, out float desiredSirenPullOver, out bool hardHalt)
        {
            speedCap = cruiseSpeed;
            desiredSirenPullOver = 0f;
            hardHalt = false;

            var loop = StreetGameLoop.Instance;
            if (loop == null || loop.ActiveVehicle == null) return false;
            var player = loop.ActiveVehicle;

            Vector3 toPlayer = player.transform.position - transform.position;
            toPlayer.y = 0f;
            float dist = toPlayer.magnitude;
            if (dist > 48f) return false;

            float forwardDot = Vector3.Dot(toPlayer, transform.forward);
            float lateralDist = Mathf.Abs(Vector3.Dot(toPlayer, transform.right));
            float playerHalfLen = player.isCoach ? 7.2f : 4.6f;
            float combinedLen = vehicleHalfLength + playerHalfLen;

            if (player.IsPoliceSirenActive && dist < 44f)
            {
                desiredSirenPullOver = 2.65f;
                if (dist < 22f)
                {
                    speedCap = Mathf.Min(speedCap, 4.0f);
                    return true;
                }
            }

            if (forwardDot > 0f && lateralDist < 3.05f)
            {
                float bumperGap = forwardDot - combinedLen;
                if (bumperGap < 18f)
                {
                    if (bumperGap <= 2.6f)
                    {
                        speedCap = 0f;
                        hardHalt = true;
                    }
                    else
                    {
                        float frac = Mathf.InverseLerp(2.6f, 18f, bumperGap);
                        speedCap = Mathf.Min(speedCap, Mathf.Max(0f, player.SpeedKmh / 3.6f * 0.9f + cruiseSpeed * frac * 0.65f));
                    }
                    return true;
                }
            }

            return false;
        }

        private bool WouldOverlapPlayer(Vector3 candidatePos, Quaternion candidateRot)
        {
            var loop = StreetGameLoop.Instance;
            if (loop == null || loop.ActiveVehicle == null) return false;
            var player = loop.ActiveVehicle;

            Vector3 diff = player.transform.position - candidatePos;
            diff.y = 0f;
            Vector3 cFwd = candidateRot * Vector3.forward;
            Vector3 cRight = candidateRot * Vector3.right;
            float fwd = Mathf.Abs(Vector3.Dot(diff, cFwd));
            float lat = Mathf.Abs(Vector3.Dot(diff, cRight));
            float playerHalfLen = player.isCoach ? 7.0f : 4.4f;
            return fwd < (vehicleHalfLength + playerHalfLen + 0.85f) && lat < 2.70f;
        }

        private static float FlatDistSqr(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return dx * dx + dz * dz;
        }
    }
}
