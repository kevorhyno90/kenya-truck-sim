using System.Collections.Generic;
using UnityEngine;

namespace KenyaStreetSim
{
    /// <summary>
    /// Live 3D ETS2-style Kenyan Route GPS Guide:
    /// Draws a glowing cyan/gold highway ribbon right above the tarmac ahead of your vehicle along
    /// your selected route toward the active Passenger Pickup or FRR Parcel Depot, plus computes
    /// real-time turn-by-turn guidance text ("FOLLOW A104 -> NAKURU • 340m • PAX PICKUP").
    /// </summary>
    public class KenyaGpsRouteGuide : MonoBehaviour
    {
        public static KenyaGpsRouteGuide Instance { get; private set; }

        public string CurrentInstruction { get; private set; } = "FOLLOW ROUTE TO NAIROBI CBD";
        public float DistanceToNextStop { get; private set; }
        public string ActiveStopPointType { get; private set; } = "PASSENGER PICKUP";

        private LineRenderer guideLine;
        private Transform[] chevronArrows;
        private List<Vector3> routePoints = new List<Vector3>();
        private int cachedRouteIdx = -1;
        private Material lineMat;
        private Material chevronMat;
        private float animPhase;

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
                leftArm.GetComponent<Renderer>().sharedMaterial = chevronMat;

                var rightArm = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(rightArm.GetComponent<Collider>());
                rightArm.transform.SetParent(holder, false);
                rightArm.transform.localPosition = new Vector3(0.34f, 0f, -0.18f);
                rightArm.transform.localRotation = Quaternion.Euler(0f, -38f, 0f);
                rightArm.transform.localScale = new Vector3(0.18f, 0.08f, 0.95f);
                rightArm.GetComponent<Renderer>().sharedMaterial = chevronMat;

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
            DistanceToNextStop = Vector3.Distance(vehPos, stagePos);

            // Determine point type label based on mode / vehicle (Passenger Pickup vs FRR Parcel Depot vs Police Patrol)
            bool isCargo = loop.ActiveMode == StreetGameLoop.GameMode.IsuzuCargoHaul
                           || (!loop.ActiveVehicle.isCoach && loop.ActiveVehicle.plateNumber != "KCP 380D")
                           || KenyaRouteCatalog.GetRoute(loop.ActiveRouteIndex).kind == KenyaRouteCatalog.RouteKind.FrrParcelCargo;
            ActiveStopPointType = loop.ActiveMode == StreetGameLoop.GameMode.KenyaPolicePatrol
                ? "POLICE CHECKPOINT"
                : (isCargo ? "FRR PARCEL DEPOT" : "PASSENGER PICKUP");

            // Find closest route spline point ahead toward active stage
            int nearIdx = FindClosestPointIndex(vehPos);
            int targetIdx = FindClosestPointIndex(stagePos);
            int stepDir = targetIdx >= nearIdx ? 1 : -1;

            // Build 180m ahead ribbon along the exact selected highway spline (offset into Kenya Left-Hand lane!)
            var ribbon = new List<Vector3>(24);
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
                Vector3 lanePt = rp + leftLane * 3.2f + Vector3.up * 0.24f;

                if (iter == 0)
                {
                    lanePt = vehPos + vehFwd * 7f;
                    lanePt.y = 0.24f;
                }
                ribbon.Add(lanePt);
                if (iter > 0) accumDist += Vector3.Distance(lastPt, lanePt);
                lastPt = lanePt;
                if (idx == targetIdx || accumDist >= 150f) break;
                idx += stepDir;
            }

            // Always terminate cleanly toward the active stage beacon when close (< 130m)
            if (DistanceToNextStop < 130f)
            {
                ribbon.Add(new Vector3(stagePos.x, 0.30f, stagePos.z));
            }

            if (ribbon.Count >= 2)
            {
                guideLine.enabled = true;
                guideLine.positionCount = ribbon.Count;
                for (int i = 0; i < ribbon.Count; i++) guideLine.SetPosition(i, ribbon[i]);

                // Animate 5 glowing golden directional chevrons sliding down the guided lane!
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

                // Compute Turn-by-Turn directional prompt relative to vehicle forward
                Vector3 lookDir = (ribbon[Mathf.Min(3, ribbon.Count - 1)] - vehPos);
                lookDir.y = 0f;
                float signedAng = lookDir.sqrMagnitude > 1f
                    ? Vector3.SignedAngle(new Vector3(vehFwd.x, 0f, vehFwd.z), lookDir.normalized, Vector3.up)
                    : 0f;

                string arrow = signedAng < -28f ? "◄ LEFT"
                             : signedAng > 28f ? "► RIGHT"
                             : (Mathf.Abs(signedAng) > 135f ? "↺ U-TURN" : "↑ STRAIGHT");
                string distStr = DistanceToNextStop >= 950f
                    ? $"{DistanceToNextStop / 1000f:0.2} km"
                    : $"{Mathf.RoundToInt(DistanceToNextStop)}m";
                string shortStage = stage.stageName.Replace(" ENA Terminal", "").Replace(" Terminal", "").ToUpperInvariant();
                string shortPoint = ActiveStopPointType == "FRR PARCEL DEPOT" ? "FRR PARCEL"
                                  : (ActiveStopPointType == "POLICE CHECKPOINT" ? "POLICE CHECK" : "PAX PICKUP");

                CurrentInstruction = DistanceToNextStop < 38f
                    ? $"● STOP AT {shortStage} — PRESS [O] DOOR  ({shortPoint})"
                    : $"{arrow} → {shortStage}  •  {distStr}  •  {shortPoint}";
            }
            else
            {
                SetGuideVisible(false);
            }
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
