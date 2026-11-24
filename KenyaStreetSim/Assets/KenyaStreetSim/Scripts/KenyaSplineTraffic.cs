using System.Collections.Generic;
using UnityEngine;

namespace KenyaStreetSim
{
    /// <summary>
    /// Continuous ETS2-grade Left-Hand Traffic (LHT) AI across all 15 Kenyan highways & 10 roundabouts:
    /// - NEVER disappears or teleports at roundabouts or highway ends!
    /// - Automatically curves CLOCKWISE (Kenyan LHT) around mid-highway & terminal roundabouts!
    /// - Seamlessly transitions from highway -> clockwise around roundabout -> onto connected outgoing highways!
    /// - Smoothly executes real U-turn turnaround arcs at outer border termini (Sirare, Malaba, Kitale, Malindi, Thika, Sori)!
    /// - Respects player vehicle envelope, Nairobi traffic lights, police sirens & uphill/downhill 3D slopes!
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

        private static List<OutgoingEdge>[] roundaboutExits;
        private static int globalRouteCounter;

        private Vector3[] pathPoints;
        private float[] cumulativeDist;
        private float totalLength;
        private float currentDist;
        private float currentSpeed;
        private float stuckTimer;
        private float lateralBypassOffset;
        private float sirenPullOverOffset;
        private bool bypassing;

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

                // Forward start (cp[0])
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

                // Reverse start (cp[last])
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

            if (highwayIndex < 0 || highwayIndex >= KenyaNetworkData.Highways.Length)
            {
                enabled = false;
                return;
            }

            BuildHighwayPath(highwayIndex, forwardDirection, prependConnectorFrom: null, connectorRoundabout: -1);
            if (totalLength < 10f)
            {
                enabled = false;
                return;
            }

            float norm = limitToSubRange && rangeMaxNorm > rangeMinNorm + 0.02f
                ? Mathf.Lerp(Mathf.Clamp01(rangeMinNorm), Mathf.Clamp01(rangeMaxNorm), Mathf.Repeat(startNormalizedProgress, 1f))
                : Mathf.Repeat(startNormalizedProgress, 1f);
            currentDist = Mathf.Clamp(norm * totalLength, 2f, Mathf.Max(2f, totalLength - 6f));
            currentSpeed = cruiseSpeed * 0.85f;

            EvaluatePose(currentDist, out Vector3 p, out Quaternion rot);
            transform.SetPositionAndRotation(p, rot);
        }

        private void BuildHighwayPath(int hIdx, bool fwd, Vector3? prependConnectorFrom, int connectorRoundabout)
        {
            highwayIndex = hIdx;
            forwardDirection = fwd;
            var def = KenyaNetworkData.Highways[hIdx];
            var sampled = KenyaNetworkData.SampleHighway(def, 6f);
            if (!fwd) sampled.Reverse();

            // Clamp lane offset safely to this highway's actual tarmac width so vehicles stay strictly on road!
            float maxSafeLane = def.roadClass == KenyaNetworkData.RoadClass.CityExpressway6Lane ? 7.4f
                              : def.roadClass == KenyaNetworkData.RoadClass.Climbing3Lane ? 6.4f
                              : 3.65f;
            float effLane = Mathf.Clamp(laneOffsetMeters, 2.3f, maxSafeLane);

            // 1. Build raw left-hand lane points along the highway
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

            // 2. Replace any mid-highway passage through a Roundabout with a smooth CLOCKWISE (LHT) circular arc!
            var smoothPts = new List<Vector3>(n + 48);

            // Optional seamless transition connector from previous highway's final position!
            if (prependConnectorFrom.HasValue && rawLane.Count > 1)
            {
                Vector3 fromPos = prependConnectorFrom.Value;
                Vector3 toPos = rawLane[0];
                if (connectorRoundabout >= 0 && connectorRoundabout < KenyaNetworkData.Roundabouts.Length)
                {
                    AppendRoundaboutClockwiseArc(smoothPts, KenyaNetworkData.Roundabouts[connectorRoundabout], fromPos, toPos);
                }
                else
                {
                    AppendSmoothUturnOrJoin(smoothPts, fromPos, toPos, transform.forward);
                }
            }

            int idx = 0;
            while (idx < rawLane.Count)
            {
                Vector3 p = rawLane[idx];
                int hitRbt = (idx > 1 && idx < rawLane.Count - 2)
                    ? KenyaNetworkData.FindRoundaboutNear(new Vector2(p.x, p.z), 3.0f)
                    : -1;

                if (hitRbt >= 0)
                {
                    var rb = KenyaNetworkData.Roundabouts[hitRbt];
                    int exitIdx = idx;
                    while (exitIdx < rawLane.Count - 1 &&
                           Vector2.Distance(new Vector2(rawLane[exitIdx].x, rawLane[exitIdx].z), rb.center) <= rb.outerRadius + 3.5f)
                    {
                        exitIdx++;
                    }
                    Vector3 entryPt = smoothPts.Count > 0 ? smoothPts[smoothPts.Count - 1] : rawLane[Mathf.Max(0, idx - 1)];
                    Vector3 exitPt = rawLane[exitIdx];
                    AppendRoundaboutClockwiseArc(smoothPts, rb, entryPt, exitPt);
                    idx = exitIdx;
                }
                else
                {
                    if (smoothPts.Count == 0 || Vector3.SqrMagnitude(p - smoothPts[smoothPts.Count - 1]) > 0.25f)
                    {
                        smoothPts.Add(p);
                    }
                    idx++;
                }
            }

            FinalizePathArrays(smoothPts);
        }

        private static void AppendRoundaboutClockwiseArc(List<Vector3> dst, KenyaNetworkData.RoundaboutDef rb, Vector3 entryPos, Vector3 exitPos)
        {
            float ringR = Mathf.Lerp(rb.innerRadius + 2.8f, rb.outerRadius - 2.6f, 0.52f);
            float angIn = Mathf.Atan2(entryPos.z - rb.center.y, entryPos.x - rb.center.x);
            float angOut = Mathf.Atan2(exitPos.z - rb.center.y, exitPos.x - rb.center.x);

            // Kenyan Left-Hand Traffic moves CLOCKWISE around roundabouts (decreasing angle in Unity XZ plane!)
            float cwSweep = angIn - angOut;
            while (cwSweep < 0.25f) cwSweep += Mathf.PI * 2f;
            if (cwSweep > Mathf.PI * 2.0f + 0.1f) cwSweep -= Mathf.PI * 2f;

            // Smooth entry blend onto the roundabout ring
            Vector3 ringEntry = RingPoint(rb.center, ringR, angIn);
            Vector3 ringExit = RingPoint(rb.center, ringR, angOut);

            AddSmoothBlend(dst, entryPos, ringEntry, 3);

            int steps = Mathf.Clamp(Mathf.CeilToInt((cwSweep * ringR) / 4.2f), 5, 34);
            for (int s = 1; s < steps; s++)
            {
                float t = (float)s / steps;
                float a = angIn - t * cwSweep;
                Vector3 rp = RingPoint(rb.center, ringR, a);
                if (dst.Count == 0 || Vector3.SqrMagnitude(rp - dst[dst.Count - 1]) > 0.25f)
                {
                    dst.Add(rp);
                }
            }

            AddSmoothBlend(dst, ringExit, exitPos, 3);
        }

        private static void AppendSmoothUturnOrJoin(List<Vector3> dst, Vector3 fromPos, Vector3 toPos, Vector3 currentFwd)
        {
            float gap = Vector3.Distance(fromPos, toPos);
            Vector3 fwd = currentFwd; fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
            fwd.Normalize();

            // Smooth wide turnaround bulb when reversing at border termini (Sirare, Malaba, Malindi, Kitale, Thika, Sori)
            if (gap < 18f)
            {
                Vector3 mid = (fromPos + toPos) * 0.5f;
                Vector3 apex = mid + fwd * 9.5f;
                Vector3 c1 = fromPos + fwd * 7.5f;
                Vector3 c2 = toPos + fwd * 7.5f;
                const int steps = 10;
                for (int s = 0; s <= steps; s++)
                {
                    float t = (float)s / steps;
                    float omt = 1f - t;
                    Vector3 pt = omt * omt * omt * fromPos + 3f * omt * omt * t * c1 + 3f * omt * t * t * c2 + t * t * t * toPos;
                    // Nudge slightly through apex for clean wide turning radius
                    pt = Vector3.Lerp(pt, apex, Mathf.Sin(t * Mathf.PI) * 0.18f);
                    pt.y = KenyaNetworkData.GetElevationAt(pt.x, pt.z);
                    if (dst.Count == 0 || Vector3.SqrMagnitude(pt - dst[dst.Count - 1]) > 0.25f) dst.Add(pt);
                }
            }
            else
            {
                AddSmoothBlend(dst, fromPos, toPos, Mathf.Clamp(Mathf.CeilToInt(gap / 5f), 4, 14));
            }
        }

        private static void AddSmoothBlend(List<Vector3> dst, Vector3 a, Vector3 b, int steps)
        {
            for (int s = 0; s <= steps; s++)
            {
                float t = (float)s / steps;
                Vector3 p = Vector3.Lerp(a, b, t);
                p.y = KenyaNetworkData.GetElevationAt(p.x, p.z);
                if (dst.Count == 0 || Vector3.SqrMagnitude(p - dst[dst.Count - 1]) > 0.25f)
                {
                    dst.Add(p);
                }
            }
        }

        private static Vector3 RingPoint(Vector2 center, float radius, float angRad)
        {
            float x = center.x + Mathf.Cos(angRad) * radius;
            float z = center.y + Mathf.Sin(angRad) * radius;
            return new Vector3(x, KenyaNetworkData.GetElevationAt(x, z), z);
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

        private void TransitionToConnectedCorridor(Vector3 currentPos)
        {
            EnsureRoundaboutGraph();
            Vector2 endXZ = new Vector2(currentPos.x, currentPos.z);

            // 1. If we arrived at one of Kenya's 10 Roundabouts -> Drive smoothly CLOCKWISE around it onto a connected highway!
            int rbIdx = KenyaNetworkData.FindRoundaboutNear(endXZ, 34f);
            if (rbIdx >= 0 && roundaboutExits[rbIdx].Count > 0)
            {
                var exits = roundaboutExits[rbIdx];
                // Prefer exiting onto a DIFFERENT connected highway (never abruptly doubling back if other exits exist!)
                var candidates = new List<OutgoingEdge>();
                for (int i = 0; i < exits.Count; i++)
                {
                    if (exits[i].highwayIdx != highwayIndex)
                    {
                        candidates.Add(exits[i]);
                    }
                }
                if (candidates.Count == 0) candidates = exits;

                int pick = ((globalRouteCounter++ + gameObject.name.GetHashCode()) & 0x7FFFFFFF) % candidates.Count;
                var chosen = candidates[pick];
                BuildHighwayPath(chosen.highwayIdx, chosen.forward, prependConnectorFrom: currentPos, connectorRoundabout: rbIdx);
                currentDist = 0f;
                return;
            }

            // 2. If we arrived at a T-Junction connecting to another highway (e.g., Rodi Kopany, Katito, Sondu, Chebilat, Naivasha, Bungoma):
            int bestH = -1;
            bool bestFwd = true;
            float bestD = 26f;
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
                BuildHighwayPath(bestH, bestFwd, prependConnectorFrom: currentPos, connectorRoundabout: -1);
                currentDist = 0f;
                return;
            }

            // 3. Outer Border Terminus (Sirare, Malaba, Kitale, Malindi, Thika, Sori): Smooth real U-turn turnaround loop back onto return lane!
            BuildHighwayPath(highwayIndex, !forwardDirection, prependConnectorFrom: currentPos, connectorRoundabout: -1);
            currentDist = 0f;
        }

        private void Update()
        {
            if (pathPoints == null || totalLength < 10f) return;

            float targetSpeed = cruiseSpeed;

            // Realistic slower cornering speed when driving clockwise around a roundabout!
            bool inRoundabout = KenyaNetworkData.FindRoundaboutNear(new Vector2(transform.position.x, transform.position.z), 4f) >= 0;
            if (inRoundabout)
            {
                targetSpeed = Mathf.Min(targetSpeed, 8.8f);
            }

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

            // 2. AI-vs-AI same-lane spacing + smooth non-teleporting overtake bypass
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

            // Smooth lateral overtake for AI-vs-AI deadlocks (NEVER teleports or pops!)
            if (!redLightStop && !yieldingToPlayer && targetSpeed < 2.2f)
            {
                stuckTimer += Time.deltaTime;
                if (stuckTimer > 2.8f)
                {
                    bypassing = true;
                    targetSpeed = Mathf.Max(7.0f, cruiseSpeed * 0.65f);
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

            float targetBypass = bypassing ? 2.95f : 0f;
            lateralBypassOffset = Mathf.MoveTowards(lateralBypassOffset, targetBypass, Time.deltaTime * 2.4f);

            float accelRate = targetSpeed < currentSpeed ? 22f : 7.0f;
            currentSpeed = hardHaltForPlayer ? 0f : Mathf.MoveTowards(currentSpeed, targetSpeed, accelRate * Time.deltaTime);

            float nextDist = currentDist + currentSpeed * Time.deltaTime;
            if (nextDist >= totalLength - 0.5f)
            {
                // Seamless continuous handoff through Roundabout / Junction / U-Turn — ZERO disappearing EVER!
                TransitionToConnectedCorridor(transform.position);
                nextDist = Mathf.Min(currentSpeed * Time.deltaTime, Mathf.Max(0f, totalLength - 1f));
            }

            EvaluatePose(nextDist, out Vector3 pos, out Quaternion rot);
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
            transform.rotation = Quaternion.Slerp(transform.rotation, rot, Time.deltaTime * 9.5f);

            // Spin wheels cleanly around the vehicle's true right axle
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

            // Yield & pull cleanly onto left shoulder if Kenya Police siren is active nearby!
            if (player.IsPoliceSirenActive && dist < 44f)
            {
                desiredSirenPullOver = 2.65f;
                if (dist < 22f)
                {
                    speedCap = Mathf.Min(speedCap, 4.0f);
                    return true;
                }
            }

            // Player directly ahead in our lane: smooth braking down to full stop before rear bumper!
            if (forwardDot > 0f && lateralDist < 2.95f)
            {
                float bumperGap = forwardDot - combinedLen;
                if (bumperGap < 18f)
                {
                    if (bumperGap <= 2.4f)
                    {
                        speedCap = 0f;
                        hardHalt = true;
                    }
                    else
                    {
                        float frac = Mathf.InverseLerp(2.4f, 18f, bumperGap);
                        speedCap = Mathf.Min(speedCap, Mathf.Max(0f, player.SpeedKmh / 3.6f * 0.9f + cruiseSpeed * frac * 0.65f));
                    }
                    return true;
                }
            }

            return false;
        }

        private bool WouldOverlapPlayer(Vector3 candidatePos)
        {
            var loop = StreetGameLoop.Instance;
            if (loop == null || loop.ActiveVehicle == null) return false;
            var player = loop.ActiveVehicle;

            Vector3 diff = player.transform.position - candidatePos;
            diff.y = 0f;
            float fwd = Mathf.Abs(Vector3.Dot(diff, transform.forward));
            float lat = Mathf.Abs(Vector3.Dot(diff, transform.right));
            float playerHalfLen = player.isCoach ? 7.0f : 4.4f;
            return fwd < (vehicleHalfLength + playerHalfLen + 0.85f) && lat < 2.65f;
        }

        private bool CheckAiObstacleAhead(out float minDist)
        {
            minDist = float.MaxValue;
            Vector3 origin = transform.position + Vector3.up * 1.15f + transform.forward * (vehicleHalfLength + 0.35f);
            float probeLen = safeStopDistance * 1.85f;
            int hitCount = Physics.SphereCastNonAlloc(origin, 1.05f, transform.forward, CastHits, probeLen, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hitCount; i++)
            {
                var col = CastHits[i].collider;
                if (col == null || col.transform.IsChildOf(transform)) continue;
                if (col.GetComponentInParent<KenyaSplineTraffic>() == null) continue;
                if (CastHits[i].distance < minDist) minDist = CastHits[i].distance;
            }
            return minDist < float.MaxValue;
        }
    }
}
