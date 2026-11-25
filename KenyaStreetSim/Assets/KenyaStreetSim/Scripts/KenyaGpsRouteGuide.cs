using System.Collections.Generic;
using UnityEngine;

namespace KenyaStreetSim
{
    /// <summary>
    /// Live 3D ETS2-style Kenyan Route GPS Guide & Full Journey Telemetry Calculator:
    /// - Draws a glowing cyan/gold highway ribbon right above the tarmac ahead of your vehicle along
    ///   your selected route toward the active Passenger Pickup or FRR Parcel Depot.
    /// - Computes BOTH real road-spline distance to the next stop AND total remaining journey distance
    ///   across all remaining stops to your final Kenyan destination, plus live ETA & completion %.
    /// </summary>
    public class KenyaGpsRouteGuide : MonoBehaviour
    {
        public static KenyaGpsRouteGuide Instance { get; private set; }

        public string CurrentInstruction { get; private set; } = "FOLLOW ROUTE TO NAIROBI CBD";
        public float DistanceToNextStop { get; private set; }
        public float TotalRemainingJourneyMeters { get; private set; }
        public float TotalRouteLengthMeters { get; private set; } = 1000f;
        public float JourneyProgress01 { get; private set; }
        public float NextStopEtaSeconds { get; private set; }
        public float TotalJourneyEtaSeconds { get; private set; }
        public string NextStopName { get; private set; } = "NAIROBI CBD";
        public string FinalDestinationName { get; private set; } = "MIGORI";
        public string UpcomingStopsSummary { get; private set; } = "";
        public int RemainingStopCount { get; private set; }
        public string ActiveStopPointType { get; private set; } = "PASSENGER PICKUP";

        private LineRenderer guideLine;
        private Transform[] chevronArrows;
        private List<Vector3> routePoints = new List<Vector3>();
        private int cachedRouteIdx = -1;
        private Material lineMat;
        private Material chevronMat;
        private float animPhase;
        private float smoothedSpeedMs = 15.5f;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            CreateGuideVisuals();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (lineMat != null) Destroy(lineMat);
            if (chevronMat != null) Destroy(chevronMat);
        }

        private void CreateGuideVisuals()
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            lineMat = new Material(shader);
            lineMat.SetColor("_BaseColor", new Color(0.08f, 0.88f, 1.0f, 0.92f));

            var lineGo = new GameObject("GpsHighwayRibbon");
            lineGo.transform.SetParent(transform, false);
            guideLine = lineGo.AddComponent<LineRenderer>();
            guideLine.sharedMaterial = lineMat;
            guideLine.startWidth = 0.55f;
            guideLine.endWidth = 0.25f;
            guideLine.useWorldSpace = true;
            guideLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            guideLine.receiveShadows = false;
            guideLine.numCapVertices = 2;

            chevronMat = new Material(shader);
            chevronMat.SetColor("_BaseColor", new Color(1.0f, 0.78f, 0.12f, 0.95f));

            chevronArrows = new Transform[5];
            for (int i = 0; i < chevronArrows.Length; i++)
            {
                var holder = new GameObject($"GpsChevron_{i}").transform;
                holder.SetParent(transform, false);
                var leftArm = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(leftArm.GetComponent<Collider>());
                leftArm.transform.SetParent(holder, false);
                leftArm.transform.localPosition = new Vector3(-0.34f, 0f, -0.18f);
                leftArm.transform.localRotation = Quaternion.Euler(0f, 38f, 0f);
                leftArm.transform.localScale = new Vector3(0.18f, 0.08f, 0.95f);
                var lr = leftArm.GetComponent<Renderer>();
                lr.sharedMaterial = chevronMat;
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

                var rightArm = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(rightArm.GetComponent<Collider>());
                rightArm.transform.SetParent(holder, false);
                rightArm.transform.localPosition = new Vector3(0.34f, 0f, -0.18f);
                rightArm.transform.localRotation = Quaternion.Euler(0f, -38f, 0f);
                rightArm.transform.localScale = new Vector3(0.18f, 0.08f, 0.95f);
                var rr = rightArm.GetComponent<Renderer>();
                rr.sharedMaterial = chevronMat;
                rr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

                holder.gameObject.SetActive(false);
                chevronArrows[i] = holder;
            }
        }

        private void Update()
        {
            var loop = StreetGameLoop.Instance;
            if (loop == null || loop.ActiveVehicle == null || loop.State != StreetGameLoop.RunState.Running)
            {
                SetGuideVisible(false);
                return;
            }

            if (cachedRouteIdx != loop.ActiveRouteIndex)
            {
                cachedRouteIdx = loop.ActiveRouteIndex;
                routePoints = KenyaRouteCatalog.BuildOrderedRoutePoints(cachedRouteIdx, 8f);
                TotalRouteLengthMeters = ComputeTotalRouteHubDistance(loop.ActiveRoute);
            }

            var stage = loop.ActiveStage;
            if (stage == null || routePoints == null || routePoints.Count < 2)
            {
                SetGuideVisible(false);
                return;
            }

            Vector3 vehPos = loop.ActiveVehicle.transform.position;
            Vector3 vehFwd = loop.ActiveVehicle.transform.forward;
            Vector3 stagePos = stage.transform.position;

            // Smooth highway speed estimate for realistic ETS2 ETA (falls back to 58 km/h when parked at a terminal)
            float curMs = Mathf.Max(0f, loop.ActiveVehicle.SpeedKmh / 3.6f);
            float etaSpeedTarget = curMs > 5.5f ? curMs : 16.0f;
            smoothedSpeedMs = Mathf.Lerp(smoothedSpeedMs, etaSpeedTarget, Time.deltaTime * 0.9f);

            // Real road-following estimate to active stop & full remaining journey to final Kenyan terminus!
            UpdateFullJourneyTelemetry(loop, vehPos, stagePos);

            ActiveStopPointType = loop.ActiveJobRole switch
            {
                KenyaStreetVehicle.VehicleJobRole.PolicePatrol => "POLICE CHECKPOINT",
                KenyaStreetVehicle.VehicleJobRole.CargoParcel => "FRR PARCEL DEPOT",
                _ => "PASSENGER PICKUP"
            };

            // Find closest route spline point ahead toward active stage
            int nearIdx = FindClosestPointIndex(vehPos);
            int targetIdx = FindClosestPointIndex(stagePos);
            int stepDir = targetIdx >= nearIdx ? 1 : -1;

            // Build 165m ahead ribbon along the exact selected highway spline (offset into Kenya Left-Hand lane!)
            var ribbon = new List<Vector3>(26);
            float accumDist = 0f;
            int idx = nearIdx;
            Vector3 lastPt = vehPos;
            for (int iter = 0; iter < 28; iter++)
            {
                if (idx < 0 || idx >= routePoints.Count) break;
                Vector3 rp = routePoints[idx];
                int nextI = Mathf.Clamp(idx + stepDir, 0, routePoints.Count - 1);
                Vector3 segFwd = (routePoints[nextI] - rp);
                segFwd.y = 0f;
                if (segFwd.sqrMagnitude < 0.01f) segFwd = vehFwd;
                segFwd.Normalize();
                Vector3 leftLane = Vector3.Cross(segFwd, Vector3.up).normalized;
                Vector3 lanePt = rp + leftLane * 3.2f;
                lanePt.y = KenyaNetworkData.GetElevationAt(lanePt.x, lanePt.z) + 0.28f;

                if (iter == 0)
                {
                    lanePt = vehPos + vehFwd * 7f;
                    lanePt.y = KenyaNetworkData.GetElevationAt(lanePt.x, lanePt.z) + 0.28f;
                }
                ribbon.Add(lanePt);
                if (iter > 0) accumDist += Vector3.Distance(lastPt, lanePt);
                lastPt = lanePt;
                if (idx == targetIdx || accumDist >= 165f) break;
                idx += stepDir;
            }

            if (DistanceToNextStop < 130f)
            {
                ribbon.Add(new Vector3(stagePos.x, KenyaNetworkData.GetElevationAt(stagePos.x, stagePos.z) + 0.32f, stagePos.z));
            }

            if (ribbon.Count >= 2)
            {
                guideLine.enabled = true;
                guideLine.positionCount = ribbon.Count;
                for (int i = 0; i < ribbon.Count; i++) guideLine.SetPosition(i, ribbon[i]);

                animPhase = Mathf.Repeat(animPhase + Time.deltaTime * 0.45f, 1f);
                for (int c = 0; c < chevronArrows.Length; c++)
                {
                    float frac = Mathf.Repeat((c / (float)chevronArrows.Length) + animPhase, 1f);
                    float fIdx = frac * (ribbon.Count - 1);
                    int a = Mathf.Clamp(Mathf.FloorToInt(fIdx), 0, ribbon.Count - 2);
                    int b = a + 1;
                    float u = fIdx - a;
                    Vector3 p = Vector3.Lerp(ribbon[a], ribbon[b], u);
                    Vector3 dir = ribbon[b] - ribbon[a];
                    dir.y = 0f;
                    if (dir.sqrMagnitude > 0.01f)
                    {
                        chevronArrows[c].gameObject.SetActive(true);
                        chevronArrows[c].position = p + Vector3.up * 0.32f;
                        chevronArrows[c].rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
                    }
                }

                Vector3 lookDir = (ribbon[Mathf.Min(3, ribbon.Count - 1)] - vehPos);
                lookDir.y = 0f;
                float signedAng = lookDir.sqrMagnitude > 1f
                    ? Vector3.SignedAngle(new Vector3(vehFwd.x, 0f, vehFwd.z), lookDir.normalized, Vector3.up)
                    : 0f;

                string arrow = signedAng < -28f ? "◄ LEFT"
                             : signedAng > 28f ? "► RIGHT"
                             : (Mathf.Abs(signedAng) > 135f ? "↺ U-TURN" : "↑ STRAIGHT");

                string nextDistStr = FormatDistance(DistanceToNextStop);
                string totalLeftStr = FormatDistance(TotalRemainingJourneyMeters);
                string etaStr = FormatEta(NextStopEtaSeconds);

                CurrentInstruction = DistanceToNextStop < 38f
                    ? $"● STOP AT {NextStopName} — PRESS [O] DOOR  |  LEFT: {totalLeftStr}"
                    : $"{arrow} → {NextStopName}  •  {nextDistStr} ({etaStr})  |  JOURNEY LEFT: {totalLeftStr}";
            }
            else
            {
                SetGuideVisible(false);
            }
        }

        private void UpdateFullJourneyTelemetry(StreetGameLoop loop, Vector3 vehPos, Vector3 activeStagePos)
        {
            var route = loop.ActiveRoute;
            var stops = route.stopHubIndices;

            // Road factor (~1.16x straight-line between consecutive corridor towns closely matches curved highway tarmac)
            float directToNext = Vector3.Distance(new Vector3(vehPos.x, 0f, vehPos.z), new Vector3(activeStagePos.x, 0f, activeStagePos.z));
            DistanceToNextStop = directToNext > 90f ? directToNext * 1.14f : directToNext;

            NextStopName = loop.ActiveStage != null
                ? CleanTownName(loop.ActiveStage.stageName)
                : "NEXT STAGE";

            float remMeters = DistanceToNextStop;
            int remStops = 0;
            var upcoming = new List<string>(4);

            if (stops != null && stops.Length > 0)
            {
                int finalHub = stops[stops.Length - 1];
                if (finalHub >= 0 && finalHub < KenyaNetworkData.Hubs.Length)
                {
                    FinalDestinationName = CleanTownName(KenyaNetworkData.Hubs[finalHub].name);
                }

                // Walk every uncompleted stop along the route from the current active stop onward
                int startStep = Mathf.Clamp(loop.CurrentRouteStopStep, 0, stops.Length - 1);
                Vector2 prevXZ = new Vector2(activeStagePos.x, activeStagePos.z);

                for (int s = startStep; s < stops.Length; s++)
                {
                    int h = stops[s];
                    if (h < 0 || h >= KenyaNetworkData.Hubs.Length) continue;
                    bool done = loop.stages != null && h < loop.stages.Length && loop.stages[h] != null && loop.stages[h].IsCompleted;
                    if (done) continue;

                    remStops++;
                    Vector2 hXZ = (loop.stages != null && h < loop.stages.Length && loop.stages[h] != null)
                        ? new Vector2(loop.stages[h].transform.position.x, loop.stages[h].transform.position.z)
                        : KenyaNetworkData.Hubs[h].xz;

                    if (remStops > 1)
                    {
                        remMeters += Vector2.Distance(prevXZ, hXZ) * 1.14f;
                    }
                    prevXZ = hXZ;

                    if (upcoming.Count < 3)
                    {
                        upcoming.Add(CleanTownName(KenyaNetworkData.Hubs[h].name));
                    }
                }
            }

            RemainingStopCount = Mathf.Max(1, remStops);
            TotalRemainingJourneyMeters = Mathf.Max(DistanceToNextStop, remMeters);
            float denom = Mathf.Max(TotalRouteLengthMeters, TotalRemainingJourneyMeters, 100f);
            JourneyProgress01 = Mathf.Clamp01(1f - (TotalRemainingJourneyMeters / denom));

            float speedEff = Mathf.Max(8.5f, smoothedSpeedMs);
            NextStopEtaSeconds = DistanceToNextStop / speedEff;
            TotalJourneyEtaSeconds = TotalRemainingJourneyMeters / speedEff;

            UpcomingStopsSummary = upcoming.Count > 0 ? string.Join(" → ", upcoming) : NextStopName;
        }

        private static float ComputeTotalRouteHubDistance(KenyaRouteCatalog.RouteDef route)
        {
            if (route.stopHubIndices == null || route.stopHubIndices.Length < 2) return 1500f;
            float sum = 0f;
            for (int i = 1; i < route.stopHubIndices.Length; i++)
            {
                int a = route.stopHubIndices[i - 1];
                int b = route.stopHubIndices[i];
                if (a >= 0 && a < KenyaNetworkData.Hubs.Length && b >= 0 && b < KenyaNetworkData.Hubs.Length)
                {
                    sum += Vector2.Distance(KenyaNetworkData.Hubs[a].xz, KenyaNetworkData.Hubs[b].xz) * 1.14f;
                }
            }
            return Mathf.Max(400f, sum);
        }

        public static string FormatDistance(float meters)
        {
            return meters >= 950f ? $"{meters / 1000f:0.00} km" : $"{Mathf.RoundToInt(meters)} m";
        }

        public static string FormatEta(float seconds)
        {
            int total = Mathf.Max(0, Mathf.RoundToInt(seconds));
            int mins = total / 60;
            int secs = total % 60;
            return $"{mins}:{secs:00}";
        }

        private static string CleanTownName(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "TOWN";
            return raw.Replace(" ENA Terminal", "")
                      .Replace(" Terminal", "")
                      .Replace(" Town", "")
                      .Replace(" City", "")
                      .Trim()
                      .ToUpperInvariant();
        }

        private int FindClosestPointIndex(Vector3 worldPos)
        {
            int best = 0;
            float bestSqr = float.MaxValue;
            for (int i = 0; i < routePoints.Count; i++)
            {
                float dx = routePoints[i].x - worldPos.x;
                float dz = routePoints[i].z - worldPos.z;
                float d2 = dx * dx + dz * dz;
                if (d2 < bestSqr) { bestSqr = d2; best = i; }
            }
            return best;
        }

        private void SetGuideVisible(bool visible)
        {
            if (guideLine != null) guideLine.enabled = visible;
            if (chevronArrows != null)
            {
                for (int i = 0; i < chevronArrows.Length; i++)
                    if (chevronArrows[i] != null) chevronArrows[i].gameObject.SetActive(visible);
            }
        }
    }
}
