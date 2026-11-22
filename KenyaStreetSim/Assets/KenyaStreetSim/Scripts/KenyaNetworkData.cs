using System.Collections.Generic;
using UnityEngine;

namespace KenyaStreetSim
{
    /// <summary>
    /// Single source of truth for the Full Connected 16-Town Kenya ENA Coach Highway Network.
    /// Defines all 16 Kenyan ENA Coach towns, 8 connected Catmull-Rom highway corridors,
    /// 3 major circular roundabouts (Nairobi Interchange, Kisii Junction, Nakuru Rift),
    /// fast cached spline sampling, and mathematical road-clearance verification so ZERO houses touch roads.
    /// </summary>
    public static class KenyaNetworkData
    {
        public enum KenyaBiome
        {
            UrbanNairobi,
            RiftSavanna,
            TeaHighlands,
            LakeVictoria,
            TsavoRedEarth,
            CoastalMombasa
        }

        public struct TownHub
        {
            public int index;
            public string name;
            public string corridorTag;
            public Vector2 xz;
            public float roadYawDeg;
            public KenyaBiome biome;
            public int fareKes;
            public int passengers;
        }

        public struct HighwayDef
        {
            public string id;
            public string displayName;
            public string routeCode;
            public Color mapColor;
            public Vector2[] controlPoints;
            public bool isLoop;
        }

        public struct RoundaboutDef
        {
            public string name;
            public Vector2 center;
            public float outerRadius;
            public float innerRadius;
        }

        // World bounds covering all 16 ETS2-scale Kenyan towns (Eldoret N -> Migori S, Lake Victoria W -> Malindi E)
        public static readonly Vector2 WorldMinXZ = new Vector2(-1380f, -390f);
        public static readonly Vector2 WorldMaxXZ = new Vector2(1060f, 920f);

        public static readonly RoundaboutDef[] Roundabouts =
        {
            new RoundaboutDef { name = "Nairobi_Interchange_Roundabout", center = new Vector2(   0f, 160f), outerRadius = 29f, innerRadius = 13f },
            new RoundaboutDef { name = "Kisii_Junction_Roundabout",      center = new Vector2(-1160f, 260f), outerRadius = 26f, innerRadius = 11f },
            new RoundaboutDef { name = "Nakuru_Rift_Roundabout",         center = new Vector2( -260f, 520f), outerRadius = 27f, innerRadius = 12f }
        };

        public static readonly TownHub[] Hubs =
        {
            new TownHub { index =  0, name = "Nairobi CBD",     corridorTag = "Central Hub (Haile Selassie)", xz = new Vector2(    0f,   55f), roadYawDeg =    0f, biome = KenyaBiome.UrbanNairobi,   fareKes = 1200, passengers = 12 },
            new TownHub { index =  1, name = "Narok Town",      corridorTag = "B3 South Nyanza / Mara Gate",  xz = new Vector2( -580f,  118f), roadYawDeg =  -96f, biome = KenyaBiome.RiftSavanna,    fareKes = 1500, passengers = 10 },
            new TownHub { index =  2, name = "Bomet Highlands", corridorTag = "B3 Tea Highlands",             xz = new Vector2( -880f,  200f), roadYawDeg =  -75f, biome = KenyaBiome.TeaHighlands,   fareKes = 1600, passengers = 10 },
            new TownHub { index =  3, name = "Kisii Town",      corridorTag = "Gusii Hub / Nyanza Junction",  xz = new Vector2(-1055f,  244f), roadYawDeg =  -78f, biome = KenyaBiome.TeaHighlands,   fareKes = 2000, passengers = 14 },
            new TownHub { index =  4, name = "Migori / Sirare", corridorTag = "A1 Tanzania Border",           xz = new Vector2(-1160f,  -75f), roadYawDeg =  180f, biome = KenyaBiome.RiftSavanna,    fareKes = 2200, passengers = 14 },
            new TownHub { index =  5, name = "Naivasha Town",   corridorTag = "A104 Lake Naivasha Rift",      xz = new Vector2(  -95f,  385f), roadYawDeg =  -44f, biome = KenyaBiome.RiftSavanna,    fareKes = 1400, passengers = 10 },
            new TownHub { index =  6, name = "Nakuru City",     corridorTag = "A104 Great Rift Valley",       xz = new Vector2( -192f,  474f), roadYawDeg =  -50f, biome = KenyaBiome.RiftSavanna,    fareKes = 1800, passengers = 12 },
            new TownHub { index =  7, name = "Kericho Town",    corridorTag = "B1 Tea Capital",               xz = new Vector2( -630f,  575f), roadYawDeg =  -90f, biome = KenyaBiome.TeaHighlands,   fareKes = 1900, passengers = 11 },
            new TownHub { index =  8, name = "Kisumu Lakeside", corridorTag = "B1 Lake Victoria Hub",         xz = new Vector2( -955f,  575f), roadYawDeg =  -90f, biome = KenyaBiome.LakeVictoria,   fareKes = 2500, passengers = 16 },
            new TownHub { index =  9, name = "Eldoret City",    corridorTag = "A104 North Rift Athletics Hub",xz = new Vector2( -545f,  814f), roadYawDeg =  -84f, biome = KenyaBiome.RiftSavanna,    fareKes = 2400, passengers = 15 },
            new TownHub { index = 10, name = "Kakamega Town",   corridorTag = "C39 Western Rainforest",       xz = new Vector2( -895f,  762f), roadYawDeg = -124f, biome = KenyaBiome.TeaHighlands,   fareKes = 2300, passengers = 12 },
            new TownHub { index = 11, name = "Homa Bay Town",   corridorTag = "C19 Lake Victoria South",      xz = new Vector2(-1182f,  405f), roadYawDeg = -170f, biome = KenyaBiome.LakeVictoria,   fareKes = 2100, passengers = 11 },
            new TownHub { index = 12, name = "Mtito Andei",     corridorTag = "A109 Mombasa Rd Halfway Hub",  xz = new Vector2(  255f,   55f), roadYawDeg =  130f, biome = KenyaBiome.TsavoRedEarth,  fareKes = 1700, passengers =  9 },
            new TownHub { index = 13, name = "Voi / Tsavo",     corridorTag = "A109 Tsavo National Gate",     xz = new Vector2(  495f, -118f), roadYawDeg =  122f, biome = KenyaBiome.TsavoRedEarth,  fareKes = 2100, passengers = 11 },
            new TownHub { index = 14, name = "Mombasa Coast",   corridorTag = "A109 Moi Ave Tusks & Ocean",   xz = new Vector2(  716f, -234f), roadYawDeg =  116f, biome = KenyaBiome.CoastalMombasa, fareKes = 3000, passengers = 18 },
            new TownHub { index = 15, name = "Malindi Coast",   corridorTag = "B8 North Coast Beach Resort",  xz = new Vector2(  942f,  -15f), roadYawDeg =   18f, biome = KenyaBiome.CoastalMombasa, fareKes = 3400, passengers = 15 }
        };

        public static readonly HighwayDef[] Highways =
        {
            // 0: Nairobi CBD Main Avenue
            new HighwayDef
            {
                id = "Nairobi_CBD_Ave",
                displayName = "Nairobi Haile Selassie & Uhuru Hwy",
                routeCode = "CBD",
                mapColor = new Color(0.96f, 0.65f, 0.14f, 1f),
                controlPoints = new[]
                {
                    new Vector2(0f, -55f),
                    new Vector2(0f,   0f),
                    new Vector2(0f,  75f),
                    new Vector2(0f, 140f)
                }
            },

            // 1: B3 South Nyanza Corridor: Nairobi Rbt -> Mai Mahiu -> Narok -> Bomet -> Kisii -> Kisii Rbt
            new HighwayDef
            {
                id = "B3_South_Nyanza",
                displayName = "B3 Nairobi – Narok – Bomet – Kisii",
                routeCode = "B3",
                mapColor = new Color(0.88f, 0.25f, 0.22f, 1f),
                controlPoints = new[]
                {
                    new Vector2(  -20f, 160f),
                    new Vector2( -140f, 162f),
                    new Vector2( -290f, 170f),
                    new Vector2( -440f, 138f),
                    new Vector2( -580f, 118f),
                    new Vector2( -730f, 152f),
                    new Vector2( -880f, 200f),
                    new Vector2(-1015f, 236f),
                    new Vector2(-1095f, 252f),
                    new Vector2(-1140f, 260f)
                }
            },

            // 2: A1 Kisii Rbt -> Rongo -> Migori -> Sirare Tanzania Border
            new HighwayDef
            {
                id = "A1_Kisii_Migori_Sirare",
                displayName = "A1 Kisii – Migori – Sirare Border",
                routeCode = "A1",
                mapColor = new Color(0.92f, 0.42f, 0.18f, 1f),
                controlPoints = new[]
                {
                    new Vector2(-1160f,  240f),
                    new Vector2(-1164f,  145f),
                    new Vector2(-1156f,   40f),
                    new Vector2(-1160f,  -75f),
                    new Vector2(-1160f, -195f)
                }
            },

            // 3: A104 Great Rift Highway: Nairobi Rbt -> Naivasha -> Nakuru -> Nakuru Rbt
            new HighwayDef
            {
                id = "A104_Nairobi_Naivasha_Nakuru",
                displayName = "A104 Nairobi – Naivasha – Nakuru",
                routeCode = "A104",
                mapColor = new Color(0.18f, 0.72f, 0.42f, 1f),
                controlPoints = new[]
                {
                    new Vector2(   0f, 180f),
                    new Vector2( -20f, 285f),
                    new Vector2( -95f, 385f),
                    new Vector2(-170f, 456f),
                    new Vector2(-242f, 508f)
                }
            },

            // 4: B1 Tea Highlands & Lake Highway: Nakuru Rbt -> Kericho -> Kisumu Lakeside
            new HighwayDef
            {
                id = "B1_Nakuru_Kericho_Kisumu",
                displayName = "B1 Nakuru – Kericho – Kisumu",
                routeCode = "B1",
                mapColor = new Color(0.28f, 0.80f, 0.36f, 1f),
                controlPoints = new[]
                {
                    new Vector2( -282f, 526f),
                    new Vector2( -445f, 565f),
                    new Vector2( -630f, 575f),
                    new Vector2( -810f, 575f),
                    new Vector2( -955f, 575f),
                    new Vector2(-1060f, 556f)
                }
            },

            // 5: A104/C39 North Rift & Western Loop: Nakuru Rbt -> Eldoret -> Kakamega -> Kisumu!
            new HighwayDef
            {
                id = "A104_C39_Eldoret_Kakamega",
                displayName = "A104/C39 Nakuru – Eldoret – Kakamega – Kisumu",
                routeCode = "C39",
                mapColor = new Color(0.62f, 0.42f, 0.94f, 1f),
                controlPoints = new[]
                {
                    new Vector2( -260f, 542f),
                    new Vector2( -305f, 675f),
                    new Vector2( -415f, 790f),
                    new Vector2( -545f, 814f), // Eldoret City
                    new Vector2( -725f, 816f),
                    new Vector2( -895f, 762f), // Kakamega Town
                    new Vector2( -995f, 682f),
                    new Vector2(-1055f, 564f)  // Joins Kisumu West
                }
            },

            // 6: C19 Lake Victoria South Shuttle: Kisumu -> Homa Bay -> Kisii Rbt
            new HighwayDef
            {
                id = "C19_Kisumu_Homabay_Kisii",
                displayName = "C19 Kisumu – Homa Bay – Kisii Loop",
                routeCode = "C19",
                mapColor = new Color(0.22f, 0.62f, 0.88f, 1f),
                controlPoints = new[]
                {
                    new Vector2(-1058f, 556f),
                    new Vector2(-1135f, 495f),
                    new Vector2(-1182f, 405f), // Homa Bay Town
                    new Vector2(-1174f, 330f),
                    new Vector2(-1160f, 280f)
                }
            },

            // 7: A109/B8 Coastal Expressway: Nairobi Rbt -> Mtito Andei -> Voi -> Mombasa -> Malindi Coast!
            new HighwayDef
            {
                id = "A109_B8_Mombasa_Malindi",
                displayName = "A109/B8 Nairobi – Mtito Andei – Voi – Mombasa – Malindi",
                routeCode = "A109",
                mapColor = new Color(0.16f, 0.76f, 0.78f, 1f),
                controlPoints = new[]
                {
                    new Vector2(  20f,  160f),
                    new Vector2( 135f,  140f),
                    new Vector2( 255f,   55f), // Mtito Andei
                    new Vector2( 375f,  -40f),
                    new Vector2( 495f, -118f), // Voi / Tsavo
                    new Vector2( 615f, -184f),
                    new Vector2( 716f, -234f), // Mombasa Coast & Tusks
                    new Vector2( 812f, -242f),
                    new Vector2( 898f, -145f), // Kilifi / North Coast Curve
                    new Vector2( 942f,  -15f), // Malindi Coast
                    new Vector2( 965f,  105f)
                }
            }
        };

        private static List<Vector3>[] cachedDenseSamples;

        private static void EnsureCachedSamples()
        {
            if (cachedDenseSamples != null && cachedDenseSamples.Length == Highways.Length) return;
            cachedDenseSamples = new List<Vector3>[Highways.Length];
            for (int h = 0; h < Highways.Length; h++)
            {
                cachedDenseSamples[h] = SampleHighway(Highways[h], 8f);
            }
        }

        public static List<Vector3> SampleHighway(HighwayDef def, float stepMeters = 6f)
        {
            var pts = new List<Vector3>();
            var cp = def.controlPoints;
            if (cp == null || cp.Length < 2) return pts;

            for (int i = 0; i < cp.Length - 1; i++)
            {
                Vector2 p0 = cp[Mathf.Max(0, i - 1)];
                Vector2 p1 = cp[i];
                Vector2 p2 = cp[i + 1];
                Vector2 p3 = cp[Mathf.Min(cp.Length - 1, i + 2)];

                float segLen = Vector2.Distance(p1, p2);
                int steps = Mathf.Max(4, Mathf.CeilToInt(segLen / stepMeters));

                int startK = (i == 0) ? 0 : 1;
                for (int k = startK; k <= steps; k++)
                {
                    float t = (float)k / steps;
                    Vector2 v = CatmullRom(p0, p1, p2, p3, t);
                    pts.Add(new Vector3(v.x, 0f, v.y));
                }
            }

            return pts;
        }

        private static Vector2 CatmullRom(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;
            return 0.5f * (
                (2f * p1) +
                (-p0 + p2) * t +
                (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
                (-p0 + 3f * p1 - 3f * p2 + p3) * t3
            );
        }

        /// <summary>
        /// Finds the exact nearest point and tangent on any Kenyan highway or roundabout.
        /// Uses cached 8m polylines for fast Editor building clearance auditing & runtime [R] lane recenter.
        /// </summary>
        public static bool FindNearestRoadPoint(Vector3 worldPos, out Vector3 nearestPoint, out Vector3 forwardDir, out float distance)
        {
            EnsureCachedSamples();
            nearestPoint = new Vector3(-3.5f, 0f, -18f);
            forwardDir = Vector3.forward;
            distance = float.MaxValue;
            Vector2 p = new Vector2(worldPos.x, worldPos.z);

            for (int r = 0; r < Roundabouts.Length; r++)
            {
                var rb = Roundabouts[r];
                float dToCenter = Vector2.Distance(p, rb.center);
                float midR = (rb.outerRadius + rb.innerRadius) * 0.5f;
                float dRing = Mathf.Abs(dToCenter - midR);
                if (dRing < distance)
                {
                    distance = dRing;
                    Vector2 radial = (p - rb.center).sqrMagnitude > 0.01f ? (p - rb.center).normalized : Vector2.right;
                    Vector2 onRing = rb.center + radial * midR;
                    nearestPoint = new Vector3(onRing.x, 0f, onRing.y);
                    forwardDir = new Vector3(radial.y, 0f, -radial.x).normalized;
                }
            }

            for (int h = 0; h < cachedDenseSamples.Length; h++)
            {
                var samples = cachedDenseSamples[h];
                for (int i = 0; i < samples.Count - 1; i++)
                {
                    Vector2 a = new Vector2(samples[i].x, samples[i].z);
                    Vector2 b = new Vector2(samples[i + 1].x, samples[i + 1].z);
                    Vector2 ab = b - a;
                    float lenSq = ab.sqrMagnitude;
                    if (lenSq < 0.001f) continue;

                    float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / lenSq);
                    Vector2 proj = a + ab * t;
                    float d = Vector2.Distance(p, proj);
                    if (d < distance)
                    {
                        distance = d;
                        nearestPoint = new Vector3(proj.x, 0f, proj.y);
                        Vector2 dir = ab.normalized;
                        forwardDir = new Vector3(dir.x, 0f, dir.y);
                    }
                }
            }

            return distance < float.MaxValue;
        }

        /// <summary>
        /// Computes minimum distance from a 2D point to the edge of ANY road tarmac or roundabout surface.
        /// Returns <= 0 if the point is directly ON tarmac or inside a roundabout!
        /// </summary>
        public static float DistanceToAnyTarmacEdge(Vector2 xz)
        {
            EnsureCachedSamples();
            const float highwayHalfWidth = 7.2f; // 14m tarmac + curb margin
            float minClearance = float.MaxValue;

            for (int r = 0; r < Roundabouts.Length; r++)
            {
                var rb = Roundabouts[r];
                float dCenter = Vector2.Distance(xz, rb.center);
                // Entire roundabout disk (outerRadius + curb) is forbidden for buildings
                float clear = dCenter - (rb.outerRadius + 1.5f);
                if (clear < minClearance) minClearance = clear;
            }

            for (int h = 0; h < cachedDenseSamples.Length; h++)
            {
                var samples = cachedDenseSamples[h];
                for (int i = 0; i < samples.Count - 1; i++)
                {
                    Vector2 a = new Vector2(samples[i].x, samples[i].z);
                    Vector2 b = new Vector2(samples[i + 1].x, samples[i + 1].z);
                    Vector2 ab = b - a;
                    float lenSq = ab.sqrMagnitude;
                    if (lenSq < 0.001f) continue;

                    float t = Mathf.Clamp01(Vector2.Dot(xz - a, ab) / lenSq);
                    Vector2 proj = a + ab * t;
                    float clear = Vector2.Distance(xz, proj) - highwayHalfWidth;
                    if (clear < minClearance) minClearance = clear;
                }
            }

            return minClearance;
        }

        /// <summary>
        /// Checks all 9 footprint sample points (center, 4 corners, 4 edge midpoints) of a rotated building
        /// against ALL 8 highways & ALL 3 roundabouts so ZERO houses can ever touch or encroach on any road!
        /// </summary>
        public static bool IsFootprintClearOfAllRoads(Vector3 worldCenter, float halfWidth, float halfDepth, float yawDeg, float minBufferFromTarmacEdge = 3.2f)
        {
            Quaternion rot = Quaternion.Euler(0f, yawDeg, 0f);
            float[] sx = { -1f, 0f, 1f };
            float[] sz = { -1f, 0f, 1f };

            for (int ix = 0; ix < 3; ix++)
            {
                for (int iz = 0; iz < 3; iz++)
                {
                    Vector3 local = new Vector3(sx[ix] * halfWidth, 0f, sz[iz] * halfDepth);
                    Vector3 wp = worldCenter + rot * local;
                    if (DistanceToAnyTarmacEdge(new Vector2(wp.x, wp.z)) < minBufferFromTarmacEdge)
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        /// <summary>
        /// Walks along the exact curved highway passing through `hubXZ` by `arcOffsetMeters`
        /// so town buildings follow curves seamlessly instead of cutting across corners!
        /// </summary>
        public static bool SampleRoadAlongTown(Vector2 hubXZ, float approxYawDeg, float arcOffsetMeters, out Vector3 roadPoint, out Vector3 roadForward)
        {
            EnsureCachedSamples();
            Vector3 approxFwd = Quaternion.Euler(0f, approxYawDeg, 0f) * Vector3.forward;

            int bestH = -1;
            int bestIdx = -1;
            float bestDist = float.MaxValue;
            Vector3 hub3 = new Vector3(hubXZ.x, 0f, hubXZ.y);

            for (int h = 0; h < cachedDenseSamples.Length; h++)
            {
                var pts = cachedDenseSamples[h];
                for (int i = 0; i < pts.Count; i++)
                {
                    float d = Vector3.Distance(pts[i], hub3);
                    if (d < bestDist)
                    {
                        bestDist = d;
                        bestH = h;
                        bestIdx = i;
                    }
                }
            }

            if (bestH < 0)
            {
                roadPoint = hub3 + approxFwd * arcOffsetMeters;
                roadForward = approxFwd;
                return false;
            }

            var curve = cachedDenseSamples[bestH];
            Vector3 localTan = (curve[Mathf.Min(curve.Count - 1, bestIdx + 1)] - curve[Mathf.Max(0, bestIdx - 1)]).normalized;
            float dirSign = Vector3.Dot(localTan, approxFwd) >= 0f ? 1f : -1f;

            // Walk along curve segments by exact arc distance
            int idx = bestIdx;
            float remaining = Mathf.Abs(arcOffsetMeters);
            int stepDir = (arcOffsetMeters * dirSign) >= 0f ? 1 : -1;

            while (remaining > 0.01f)
            {
                int nextIdx = idx + stepDir;
                if (nextIdx < 0 || nextIdx >= curve.Count) break;
                float seg = Vector3.Distance(curve[idx], curve[nextIdx]);
                if (seg >= remaining)
                {
                    float frac = remaining / Mathf.Max(0.001f, seg);
                    roadPoint = Vector3.Lerp(curve[idx], curve[nextIdx], frac);
                    Vector3 rawFwd = (curve[nextIdx] - curve[idx]).normalized * (stepDir * (arcOffsetMeters >= 0f ? 1f : -1f));
                    roadForward = Vector3.Dot(rawFwd, approxFwd) >= 0f ? rawFwd : -rawFwd;
                    return true;
                }
                remaining -= seg;
                idx = nextIdx;
            }

            roadPoint = curve[idx];
            Vector3 tan = (curve[Mathf.Min(curve.Count - 1, idx + 1)] - curve[Mathf.Max(0, idx - 1)]).normalized;
            roadForward = Vector3.Dot(tan, approxFwd) >= 0f ? tan : -tan;
            return true;
        }
    }
}
