using System.Collections.Generic;
using UnityEngine;

namespace KenyaStreetSim
{
    /// <summary>
    /// Single source of truth for the Full Connected Kenya ENA Coach Highway & Town Network.
    /// Defines all 10 Kenyan ENA Coach hubs, 5 connected Catmull-Rom highway corridors,
    /// 2 major roundabouts (Nairobi Interchange & Kisii Junction), and deterministic spline sampling.
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

        // World bounds covering all of playable ETS2-scale Kenya (X: -1350..+920, Z: -400..+780)
        public static readonly Vector2 WorldMinXZ = new Vector2(-1340f, -380f);
        public static readonly Vector2 WorldMaxXZ = new Vector2(900f, 760f);

        public static readonly RoundaboutDef[] Roundabouts =
        {
            new RoundaboutDef { name = "Nairobi_Interchange_Roundabout", center = new Vector2(0f, 160f), outerRadius = 29f, innerRadius = 13f },
            new RoundaboutDef { name = "Kisii_Junction_Roundabout",      center = new Vector2(-1160f, 260f), outerRadius = 26f, innerRadius = 11f }
        };

        public static readonly TownHub[] Hubs =
        {
            new TownHub { index = 0, name = "Nairobi CBD",       corridorTag = "Central Hub (Haile Selassie)", xz = new Vector2(   0f,   55f), roadYawDeg =   0f, biome = KenyaBiome.UrbanNairobi,  fareKes = 1200, passengers = 12 },
            new TownHub { index = 1, name = "Narok Town",        corridorTag = "B3 South Nyanza / Mara Gate",  xz = new Vector2(-580f,  118f), roadYawDeg = -96f, biome = KenyaBiome.RiftSavanna,   fareKes = 1500, passengers = 10 },
            new TownHub { index = 2, name = "Bomet Highlands",   corridorTag = "B3 Tea Highlands",             xz = new Vector2(-880f,  200f), roadYawDeg = -75f, biome = KenyaBiome.TeaHighlands,  fareKes = 1600, passengers = 10 },
            new TownHub { index = 3, name = "Kisii Town",        corridorTag = "Gusii Hub / Nyanza Junction",  xz = new Vector2(-1105f, 250f), roadYawDeg = -80f, biome = KenyaBiome.TeaHighlands,  fareKes = 2000, passengers = 14 },
            new TownHub { index = 4, name = "Migori / Sirare",   corridorTag = "A1 Tanzania Border",           xz = new Vector2(-1160f, -75f), roadYawDeg = 180f, biome = KenyaBiome.RiftSavanna,   fareKes = 2200, passengers = 14 },
            new TownHub { index = 5, name = "Nakuru City",       corridorTag = "A104 Great Rift Valley",       xz = new Vector2(-260f,  520f), roadYawDeg = -60f, biome = KenyaBiome.RiftSavanna,   fareKes = 1800, passengers = 12 },
            new TownHub { index = 6, name = "Kericho Town",      corridorTag = "B1 Tea Capital",               xz = new Vector2(-630f,  575f), roadYawDeg = -90f, biome = KenyaBiome.TeaHighlands,  fareKes = 1900, passengers = 11 },
            new TownHub { index = 7, name = "Kisumu Lakeside",   corridorTag = "B1 Lake Victoria Hub",         xz = new Vector2(-960f,  575f), roadYawDeg = -90f, biome = KenyaBiome.LakeVictoria,  fareKes = 2500, passengers = 16 },
            new TownHub { index = 8, name = "Voi / Tsavo",       corridorTag = "A109 Mombasa Road",            xz = new Vector2( 425f,  -85f), roadYawDeg = 124f, biome = KenyaBiome.TsavoRedEarth, fareKes = 2100, passengers = 10 },
            new TownHub { index = 9, name = "Mombasa Coast",     corridorTag = "A109 Moi Ave Tusks & Ocean",   xz = new Vector2( 700f, -230f), roadYawDeg = 118f, biome = KenyaBiome.CoastalMombasa,fareKes = 3000, passengers = 18 }
        };

        public static readonly HighwayDef[] Highways =
        {
            // 0: Nairobi CBD Main Avenue (South Terminal -> Afya Centre -> Nairobi Interchange Roundabout)
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

            // 1: B3 South Nyanza Corridor: Nairobi Rbt -> Mai Mahiu -> Narok -> Bomet -> Kisii Rbt
            new HighwayDef
            {
                id = "B3_South_Nyanza",
                displayName = "B3 Nairobi – Mai Mahiu – Narok – Bomet – Kisii",
                routeCode = "B3",
                mapColor = new Color(0.88f, 0.25f, 0.22f, 1f),
                controlPoints = new[]
                {
                    new Vector2(  -20f, 160f),
                    new Vector2( -140f, 162f),
                    new Vector2( -290f, 170f), // Mai Mahiu Escarpment
                    new Vector2( -440f, 138f),
                    new Vector2( -580f, 118f), // Narok Town
                    new Vector2( -730f, 152f),
                    new Vector2( -880f, 200f), // Bomet Highlands
                    new Vector2(-1030f, 240f),
                    new Vector2(-1140f, 260f)  // Kisii Junction Roundabout
                }
            },

            // 2: A1 Kisii -> Rongo -> Awendo -> Migori -> Sirare Border
            new HighwayDef
            {
                id = "A1_Kisii_Migori_Sirare",
                displayName = "A1 Kisii – Rongo – Migori – Sirare Border",
                routeCode = "A1",
                mapColor = new Color(0.92f, 0.42f, 0.18f, 1f),
                controlPoints = new[]
                {
                    new Vector2(-1160f,  240f),
                    new Vector2(-1165f,  150f),
                    new Vector2(-1152f,   45f), // Rongo / Awendo
                    new Vector2(-1160f,  -75f), // Migori Town
                    new Vector2(-1160f, -185f)  // Sirare Tanzania Border Arch
                }
            },

            // 3: A104 / B1 Western Rift Corridor: Nairobi Rbt -> Naivasha -> Nakuru -> Kericho -> Kisumu Lakeside
            new HighwayDef
            {
                id = "A104_B1_Western_Rift",
                displayName = "A104/B1 Nairobi – Naivasha – Nakuru – Kericho – Kisumu",
                routeCode = "A104",
                mapColor = new Color(0.18f, 0.72f, 0.42f, 1f),
                controlPoints = new[]
                {
                    new Vector2(   0f, 180f),
                    new Vector2( -18f, 285f),
                    new Vector2( -95f, 395f), // Naivasha Rift Valley
                    new Vector2(-260f, 520f), // Nakuru City
                    new Vector2(-445f, 565f),
                    new Vector2(-630f, 575f), // Kericho Tea Estates
                    new Vector2(-810f, 575f),
                    new Vector2(-960f, 575f), // Kisumu Lakeside
                    new Vector2(-1065f, 555f)
                }
            },

            // 4: Nyanza Shuttle Connector: Kisumu -> Katito -> Oyugis -> Kisii Roundabout (closes Western/Nyanza Loop!)
            new HighwayDef
            {
                id = "C19_Kisumu_Kisii_Loop",
                displayName = "A1/C19 Kisumu – Oyugis – Kisii Shuttle Highway",
                routeCode = "C19",
                mapColor = new Color(0.22f, 0.62f, 0.88f, 1f),
                controlPoints = new[]
                {
                    new Vector2(-1055f, 556f),
                    new Vector2(-1120f, 490f),
                    new Vector2(-1145f, 395f), // Oyugis
                    new Vector2(-1160f, 280f)  // Kisii Junction Roundabout
                }
            },

            // 5: A109 Mombasa Road Coastal Expressway: Nairobi Rbt -> Mtito Andei -> Voi -> Mombasa Tusks & Coast
            new HighwayDef
            {
                id = "A109_Mombasa_Road",
                displayName = "A109 Nairobi – Mtito Andei – Voi – Mombasa Coast",
                routeCode = "A109",
                mapColor = new Color(0.16f, 0.76f, 0.78f, 1f),
                controlPoints = new[]
                {
                    new Vector2(  20f,  160f),
                    new Vector2( 130f,  145f),
                    new Vector2( 245f,   65f), // Mtito Andei / Machakos Turn
                    new Vector2( 355f,  -25f),
                    new Vector2( 445f,  -95f), // Voi / Tsavo Red Earth
                    new Vector2( 585f, -172f),
                    new Vector2( 700f, -230f), // Mombasa Tusks & Terminal
                    new Vector2( 785f, -270f)  // Indian Ocean Waterfront Loop
                }
            }
        };

        /// <summary>
        /// Samples a smooth Catmull-Rom polyline spaced roughly every `stepMeters`.
        /// </summary>
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
        /// Finds the closest point and forward highway direction anywhere across the entire Kenya road network
        /// (used for [R] clean lane recenter and terrain road-corridor flattening).
        /// </summary>
        public static bool FindNearestRoadPoint(Vector3 worldPos, out Vector3 nearestPoint, out Vector3 forwardDir, out float distance)
        {
            nearestPoint = new Vector3(-3.5f, 0f, -18f);
            forwardDir = Vector3.forward;
            distance = float.MaxValue;
            Vector2 p = new Vector2(worldPos.x, worldPos.z);

            // Check roundabouts
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
                    // Clockwise LHT Kenyan roundabout tangent
                    forwardDir = new Vector3(radial.y, 0f, -radial.x).normalized;
                }
            }

            // Check all 6 highways
            for (int h = 0; h < Highways.Length; h++)
            {
                var samples = SampleHighway(Highways[h], 10f);
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
    }
}
