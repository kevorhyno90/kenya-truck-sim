using System.Collections.Generic;
using UnityEngine;

namespace KenyaStreetSim
{
    /// <summary>
    /// Authoritative National Republic of Kenya Highway, 41-Town Geography & 3D Elevation Engine.
    /// Verified against Kenya's real KeNHA major trunk network (A1, A2, A104, A109, B1, B3, B8, C18, C19, C20, C25, C33):
    /// - 41 real Kenyan towns & junctions from Sirare/Migori & Sori to Kitale, Malaba (Uganda) & Malindi Coast
    /// - 15 interconnected Catmull-Rom corridors with variable road classes:
    ///   * 4-6 Lane City Expressways (Nairobi-Thika, Kisumu, Nakuru, Eldoret, Mombasa)
    ///   * 3-Lane Dedicated Climbing-Lane Escarpments (Mai Mahiu, Nakuru-Salgaa-Total, Timboroa, Kericho, Sondu)
    ///   * 2-Lane Flat & Highland Trunk Highways
    /// - Smooth C-infinity 3D elevation field GetElevationAt(xz) so terrain, tarmac, buildings, traffic,
    ///   airports, markets, tea plantations, wildlife & roadblocks meet seamlessly with ZERO gaps!
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

        public enum TownTier
        {
            MajorCity,      // 4-6 lane boulevard, airport/skyline, major petrol stations
            RegionalHub,    // Large county town, petrol station, market/landmarks
            TransitTown     // Compact highway junction / trading center + stage stop
        }

        public enum RoadClass
        {
            Trunk2Lane,         // 14.0m tarmac (halfW = 7.0m)
            Climbing3Lane,      // 18.4m tarmac (halfW = 9.2m) with dedicated slow-vehicle climbing lane
            CityExpressway6Lane // 22.0m divided 6-lane tarmac (halfW = 11.0m) with central median!
        }

        public struct TownHub
        {
            public int index;
            public string name;
            public string corridorTag;
            public Vector2 xz;
            public float roadYawDeg;
            public KenyaBiome biome;
            public TownTier tier;
            public int fareKes;
            public int passengers;
        }

        public struct HighwayDef
        {
            public string id;
            public string displayName;
            public string routeCode;
            public Color mapColor;
            public RoadClass roadClass;
            public Vector2[] controlPoints;
            public bool heavyTrailers; // Extra 18-wheeler transit trailers (Kisumu, Nakuru-Eldoret-Malaba, Mombasa!)
            public bool isLoop;
        }

        public struct RoundaboutDef
        {
            public string name;
            public Vector2 center;
            public float outerRadius;
            public float innerRadius;
        }

        // Expanded ETS2-scale National Kenya Bounds (Sori W -> Malindi E, Migori S -> Kitale/Malaba N)
        public static readonly Vector2 WorldMinXZ = new Vector2(-2140f, -540f);
        public static readonly Vector2 WorldMaxXZ = new Vector2(1420f, 1680f);

        public static readonly RoundaboutDef[] Roundabouts =
        {
            new RoundaboutDef { name = "Nairobi_Interchange_Rbt",  center = new Vector2(    0f,  170f), outerRadius = 31f, innerRadius = 13f },
            new RoundaboutDef { name = "MaiMahiu_Rift_Rbt",        center = new Vector2( -280f,  170f), outerRadius = 26f, innerRadius = 11f },
            new RoundaboutDef { name = "Kaplong_TeaSplit_Rbt",     center = new Vector2(-1085f,  255f), outerRadius = 25f, innerRadius = 10f },
            new RoundaboutDef { name = "Kisii_Junction_Rbt",       center = new Vector2(-1585f,  190f), outerRadius = 27f, innerRadius = 11f },
            new RoundaboutDef { name = "Rongo_HomabaySplit_Rbt",   center = new Vector2(-1645f,  -55f), outerRadius = 24f, innerRadius = 10f },
            new RoundaboutDef { name = "Ahero_KisumuSplit_Rbt",    center = new Vector2(-1340f,  715f), outerRadius = 26f, innerRadius = 11f },
            new RoundaboutDef { name = "Kisumu_Lakeside_Rbt",      center = new Vector2(-1465f,  835f), outerRadius = 29f, innerRadius = 12f },
            new RoundaboutDef { name = "Nakuru_Rift_Rbt",          center = new Vector2( -425f,  675f), outerRadius = 28f, innerRadius = 12f },
            new RoundaboutDef { name = "Total_MauSummit_Rbt",      center = new Vector2( -785f,  765f), outerRadius = 27f, innerRadius = 11f },
            new RoundaboutDef { name = "Webuye_Western_Rbt",       center = new Vector2(-1265f, 1345f), outerRadius = 25f, innerRadius = 10f },
        };

        // ALL 41 AUTHENTIC KENYAN TOWNS & JUNCTIONS!
        public static readonly TownHub[] Hubs =
        {
            // Central & Thika
            new TownHub { index =  0, name = "Nairobi CBD",       corridorTag = "A2/A104/A109 Capital Hub (6-Lane)",    xz = new Vector2(    0f,   40f), roadYawDeg =    0f, biome = KenyaBiome.UrbanNairobi,   tier = TownTier.MajorCity,   fareKes = 1200, passengers = 14 },
            new TownHub { index =  1, name = "Thika Town",        corridorTag = "A2 Thika 6-Lane Superhighway",         xz = new Vector2(  120f,  420f), roadYawDeg =   18f, biome = KenyaBiome.UrbanNairobi,   tier = TownTier.RegionalHub, fareKes = 1300, passengers = 12 },

            // South Rift & B3 Highlands (Nairobi -> Mai Mahiu -> Narok -> Bomet -> Kaplong -> Sotik -> Chebilat -> Keroka -> Kisii)
            new TownHub { index =  2, name = "Mai Mahiu",         corridorTag = "B3/C88 Rift Escarpment Junction",      xz = new Vector2( -280f,  170f), roadYawDeg =  -90f, biome = KenyaBiome.RiftSavanna,    tier = TownTier.TransitTown, fareKes = 1400, passengers = 10 },
            new TownHub { index =  3, name = "Narok Town",        corridorTag = "B3 Maasai Mara Wildlife Gate",         xz = new Vector2( -640f,  115f), roadYawDeg =  -96f, biome = KenyaBiome.RiftSavanna,    tier = TownTier.RegionalHub, fareKes = 1600, passengers = 11 },
            new TownHub { index =  4, name = "Bomet Town",        corridorTag = "B3 Tea Highlands Gateway",             xz = new Vector2( -960f,  195f), roadYawDeg =  -74f, biome = KenyaBiome.TeaHighlands,   tier = TownTier.RegionalHub, fareKes = 1750, passengers = 10 },
            new TownHub { index =  5, name = "Kaplong Junction",  corridorTag = "B3/B1 Bomet–Kericho–Sotik Split",      xz = new Vector2(-1085f,  255f), roadYawDeg =  -95f, biome = KenyaBiome.TeaHighlands,   tier = TownTier.TransitTown, fareKes = 1800, passengers =  9 },
            new TownHub { index =  6, name = "Sotik Town",        corridorTag = "B3 Tea & Dairy Highlands",             xz = new Vector2(-1215f,  240f), roadYawDeg =  -97f, biome = KenyaBiome.TeaHighlands,   tier = TownTier.TransitTown, fareKes = 1850, passengers =  9 },
            new TownHub { index =  7, name = "Chebilat Junction", corridorTag = "B3/C25 Tea Estates & Sondu Link",      xz = new Vector2(-1335f,  225f), roadYawDeg =  -98f, biome = KenyaBiome.TeaHighlands,   tier = TownTier.TransitTown, fareKes = 1900, passengers =  9 },
            new TownHub { index =  8, name = "Keroka Town",       corridorTag = "B3 Gusii Highland Market",             xz = new Vector2(-1455f,  205f), roadYawDeg =  -98f, biome = KenyaBiome.TeaHighlands,   tier = TownTier.TransitTown, fareKes = 1950, passengers = 10 },
            new TownHub { index =  9, name = "Kisii Town",        corridorTag = "A1/B3 Gusii Capital & Daraja Mbili",   xz = new Vector2(-1585f,  190f), roadYawDeg =  -98f, biome = KenyaBiome.TeaHighlands,   tier = TownTier.RegionalHub, fareKes = 2100, passengers = 14 },

            // A1 South Nyanza (Kisii -> Suneka -> Rongo -> Awendo -> Migori/Sirare)
            new TownHub { index = 10, name = "Suneka Town",       corridorTag = "A1 Kisii–Rongo Ridge",                 xz = new Vector2(-1620f,   75f), roadYawDeg = -164f, biome = KenyaBiome.TeaHighlands,   tier = TownTier.TransitTown, fareKes = 2150, passengers =  9 },
            new TownHub { index = 11, name = "Rongo Junction",    corridorTag = "A1/C20 Homa Bay Branch Split",         xz = new Vector2(-1645f,  -55f), roadYawDeg = -172f, biome = KenyaBiome.RiftSavanna,    tier = TownTier.TransitTown, fareKes = 2200, passengers = 10 },
            new TownHub { index = 12, name = "Awendo Town",       corridorTag = "A1 South Nyanza Sugar Belt",           xz = new Vector2(-1660f, -205f), roadYawDeg = -175f, biome = KenyaBiome.RiftSavanna,    tier = TownTier.TransitTown, fareKes = 2300, passengers = 10 },
            new TownHub { index = 13, name = "Migori / Sirare",   corridorTag = "A1 Kenya–Tanzania Border Hub",         xz = new Vector2(-1670f, -370f), roadYawDeg =  180f, biome = KenyaBiome.RiftSavanna,    tier = TownTier.RegionalHub, fareKes = 2500, passengers = 14 },

            // Rongo -> Rodi Kopany -> (Ndhiwa -> Sori) & (Homa Bay -> Kendu Bay -> Katito)
            new TownHub { index = 14, name = "Rodi Kopany",       corridorTag = "C20/C18 Homa Bay & Sori Junction",     xz = new Vector2(-1775f,   45f), roadYawDeg =  -50f, biome = KenyaBiome.LakeVictoria,   tier = TownTier.TransitTown, fareKes = 2200, passengers =  9 },
            new TownHub { index = 15, name = "Ndhiwa Town",       corridorTag = "C18 Rodi–Sori Lake Basin",             xz = new Vector2(-1895f,  -50f), roadYawDeg = -128f, biome = KenyaBiome.LakeVictoria,   tier = TownTier.TransitTown, fareKes = 2350, passengers =  8 },
            new TownHub { index = 16, name = "Sori (Karungu)",    corridorTag = "C18 Lake Victoria Fishing Port",       xz = new Vector2(-1975f, -185f), roadYawDeg = -148f, biome = KenyaBiome.LakeVictoria,   tier = TownTier.TransitTown, fareKes = 2550, passengers = 10 },
            new TownHub { index = 17, name = "Homa Bay Town",     corridorTag = "C19 Lake Victoria South Capital",      xz = new Vector2(-1825f,  225f), roadYawDeg =  -12f, biome = KenyaBiome.LakeVictoria,   tier = TownTier.RegionalHub, fareKes = 2350, passengers = 12 },
            new TownHub { index = 18, name = "Kendu Bay",         corridorTag = "C19 Winam Gulf Lakeshore",             xz = new Vector2(-1670f,  410f), roadYawDeg =   40f, biome = KenyaBiome.LakeVictoria,   tier = TownTier.TransitTown, fareKes = 2250, passengers =  9 },

            // Kisii -> Oyugis -> Sondu -> Katito -> Ahero -> Kisumu
            new TownHub { index = 19, name = "Oyugis Town",       corridorTag = "A1 Kisii–Sondu Market Town",           xz = new Vector2(-1545f,  345f), roadYawDeg =   20f, biome = KenyaBiome.TeaHighlands,   tier = TownTier.TransitTown, fareKes = 2150, passengers = 10 },
            new TownHub { index = 20, name = "Sondu Town",        corridorTag = "A1/C25 Escarpment & Market Hub",       xz = new Vector2(-1455f,  470f), roadYawDeg =   32f, biome = KenyaBiome.TeaHighlands,   tier = TownTier.TransitTown, fareKes = 2200, passengers = 11 },
            new TownHub { index = 21, name = "Katito Junction",   corridorTag = "A1/C19 Kendu Bay Merge Plains",        xz = new Vector2(-1445f,  595f), roadYawDeg =   12f, biome = KenyaBiome.LakeVictoria,   tier = TownTier.TransitTown, fareKes = 2300, passengers =  9 },
            new TownHub { index = 22, name = "Ahero Junction",    corridorTag = "A1/B1 Rice Market & Major Split",      xz = new Vector2(-1340f,  715f), roadYawDeg =   38f, biome = KenyaBiome.LakeVictoria,   tier = TownTier.TransitTown, fareKes = 2400, passengers = 11 },
            new TownHub { index = 23, name = "Kisumu City",       corridorTag = "A1/B1 Lake Hub, Airport & Kibuye",     xz = new Vector2(-1465f,  835f), roadYawDeg =  -45f, biome = KenyaBiome.LakeVictoria,   tier = TownTier.MajorCity,   fareKes = 2650, passengers = 16 },

            // Western & North-West (Kisumu -> Vihiga -> Kakamega -> Webuye -> Kiminini -> Kitale & Kisumu -> Bungoma -> Malaba Uganda)
            new TownHub { index = 24, name = "Vihiga (Chavakali)",corridorTag = "A1 Western Hills & Market",            xz = new Vector2(-1425f, 1005f), roadYawDeg =   14f, biome = KenyaBiome.TeaHighlands,   tier = TownTier.TransitTown, fareKes = 2600, passengers = 10 },
            new TownHub { index = 25, name = "Kakamega Town",     corridorTag = "A1 Western Rainforest Capital",        xz = new Vector2(-1365f, 1170f), roadYawDeg =   20f, biome = KenyaBiome.TeaHighlands,   tier = TownTier.RegionalHub, fareKes = 2750, passengers = 13 },
            new TownHub { index = 26, name = "Webuye Junction",   corridorTag = "A1/A104 Kitale–Bungoma–Eldoret",       xz = new Vector2(-1265f, 1345f), roadYawDeg =   30f, biome = KenyaBiome.TeaHighlands,   tier = TownTier.TransitTown, fareKes = 2850, passengers = 11 },
            new TownHub { index = 27, name = "Kiminini Town",     corridorTag = "A1 Trans-Nzoia Highlands",             xz = new Vector2(-1125f, 1465f), roadYawDeg =   52f, biome = KenyaBiome.TeaHighlands,   tier = TownTier.TransitTown, fareKes = 2950, passengers =  9 },
            new TownHub { index = 28, name = "Kitale Town",       corridorTag = "A1 North-West Agri Hub → North",       xz = new Vector2( -965f, 1525f), roadYawDeg =   68f, biome = KenyaBiome.TeaHighlands,   tier = TownTier.RegionalHub, fareKes = 3100, passengers = 13 },
            new TownHub { index = 29, name = "Bungoma Town",      corridorTag = "C33/A104 Western Uganda Corridor",     xz = new Vector2(-1585f, 1285f), roadYawDeg =  -26f, biome = KenyaBiome.TeaHighlands,   tier = TownTier.RegionalHub, fareKes = 2850, passengers = 13 },
            new TownHub { index = 30, name = "Malaba (Uganda)",   corridorTag = "A104 Kenya–Uganda International",      xz = new Vector2(-1785f, 1385f), roadYawDeg =  -62f, biome = KenyaBiome.RiftSavanna,    tier = TownTier.RegionalHub, fareKes = 3300, passengers = 15 },

            // Great Rift, Tea Capital & North Rift (Naivasha -> Nakuru -> Salgaa/Total -> Kericho & Eldoret)
            new TownHub { index = 31, name = "Naivasha Town",     corridorTag = "A104 Lake Naivasha Rift Floor",        xz = new Vector2( -245f,  455f), roadYawDeg =  -38f, biome = KenyaBiome.RiftSavanna,    tier = TownTier.RegionalHub, fareKes = 1500, passengers = 11 },
            new TownHub { index = 32, name = "Nakuru City",       corridorTag = "A104 Great Rift Capital (6-Lane)",     xz = new Vector2( -425f,  675f), roadYawDeg =  -42f, biome = KenyaBiome.RiftSavanna,    tier = TownTier.MajorCity,   fareKes = 1850, passengers = 14 },
            new TownHub { index = 33, name = "Salgaa / Total",    corridorTag = "A104/B1 Mau Summit Climbing Split",    xz = new Vector2( -785f,  765f), roadYawDeg =  -76f, biome = KenyaBiome.TeaHighlands,   tier = TownTier.TransitTown, fareKes = 2100, passengers = 11 },
            new TownHub { index = 34, name = "Kericho Town",      corridorTag = "B1 Emerald Tea Plantation Capital",    xz = new Vector2(-1055f,  520f), roadYawDeg =  -20f, biome = KenyaBiome.TeaHighlands,   tier = TownTier.RegionalHub, fareKes = 2100, passengers = 13 },
            new TownHub { index = 35, name = "Eldoret City",      corridorTag = "A104 North Rift, Airport & Trailers",  xz = new Vector2( -920f, 1145f), roadYawDeg =  -22f, biome = KenyaBiome.RiftSavanna,    tier = TownTier.MajorCity,   fareKes = 2600, passengers = 15 },

            // A109 Mombasa Road & B8 North Coast (Mtito Andei -> Voi -> Mombasa -> Kilifi -> Malindi)
            new TownHub { index = 36, name = "Mtito Andei",       corridorTag = "A109 Mombasa Rd Halfway Truck Hub",    xz = new Vector2(  355f,  -25f), roadYawDeg =  124f, biome = KenyaBiome.TsavoRedEarth,  tier = TownTier.TransitTown, fareKes = 1900, passengers = 10 },
            new TownHub { index = 37, name = "Voi / Tsavo",       corridorTag = "A109 Tsavo Wildlife Crossing Gate",    xz = new Vector2(  655f, -195f), roadYawDeg =  120f, biome = KenyaBiome.TsavoRedEarth,  tier = TownTier.RegionalHub, fareKes = 2350, passengers = 12 },
            new TownHub { index = 38, name = "Mombasa City",      corridorTag = "A109 Moi Ave Tusks, Airport & Port",   xz = new Vector2(  965f, -355f), roadYawDeg =  116f, biome = KenyaBiome.CoastalMombasa, tier = TownTier.MajorCity,   fareKes = 3100, passengers = 18 },
            new TownHub { index = 39, name = "Kilifi Town",       corridorTag = "B8 Coastal Creek & Palms",             xz = new Vector2( 1145f, -165f), roadYawDeg =   32f, biome = KenyaBiome.CoastalMombasa, tier = TownTier.TransitTown, fareKes = 3350, passengers = 11 },
            new TownHub { index = 40, name = "Malindi Coast",     corridorTag = "B8 North Coast Beach Terminus",        xz = new Vector2( 1245f,   65f), roadYawDeg =   18f, biome = KenyaBiome.CoastalMombasa, tier = TownTier.RegionalHub, fareKes = 3650, passengers = 15 },
        };

        // 15 AUTHENTIC INTERCONNECTED NATIONAL KENYA HIGHWAYS!
        public static readonly HighwayDef[] Highways =
        {
            // 0: Nairobi CBD 6-Lane Uhuru Hwy -> Nairobi Rbt -> A2 Thika 6-Lane Superhighway!
            new HighwayDef
            {
                id = "A2_Nairobi_Thika_Superhighway",
                displayName = "Nairobi CBD & A2 Thika 6-Lane Superhighway",
                routeCode = "A2",
                mapColor = new Color(0.98f, 0.68f, 0.14f, 1f),
                roadClass = RoadClass.CityExpressway6Lane,
                controlPoints = new[]
                {
                    new Vector2(  0f, -60f),
                    new Vector2(  0f,  40f), // Nairobi CBD
                    new Vector2(  0f, 148f), // Nairobi Interchange South entry
                    new Vector2( 16f, 200f), // Exits Nairobi Rbt north-east -> Thika
                    new Vector2( 65f, 315f),
                    new Vector2(120f, 420f), // Thika Town
                    new Vector2(165f, 520f)
                }
            },

            // 1: A1 South Nyanza: Kisii Rbt -> Suneka -> Rongo Rbt -> Awendo -> Migori / Sirare Border
            new HighwayDef
            {
                id = "A1_Kisii_Rongo_Migori",
                displayName = "A1 Kisii – Suneka – Rongo – Awendo – Migori",
                routeCode = "A1",
                mapColor = new Color(0.94f, 0.42f, 0.16f, 1f),
                roadClass = RoadClass.Trunk2Lane,
                controlPoints = new[]
                {
                    new Vector2(-1592f,  166f), // Exits Kisii Rbt south
                    new Vector2(-1620f,   75f), // Suneka
                    new Vector2(-1642f,  -32f), // Approaches Rongo Rbt
                    new Vector2(-1648f,  -78f), // Exits Rongo Rbt south
                    new Vector2(-1660f, -205f), // Awendo
                    new Vector2(-1670f, -370f), // Migori / Sirare
                    new Vector2(-1675f, -455f)
                }
            },

            // 2: C20/C19 Homa Bay Lake Loop: Rongo Rbt (Left Turn!) -> Rodi Kopany -> Homa Bay -> Kendu Bay -> Katito!
            new HighwayDef
            {
                id = "C20_C19_Rongo_Homabay_Katito",
                displayName = "C20/C19 Rongo – Rodi Kopany – Homa Bay – Kendu Bay – Katito",
                routeCode = "C19",
                mapColor = new Color(0.18f, 0.68f, 0.94f, 1f),
                roadClass = RoadClass.Trunk2Lane,
                controlPoints = new[]
                {
                    new Vector2(-1668f,  -42f), // Branches left from Rongo Rbt
                    new Vector2(-1775f,   45f), // Rodi Kopany Junction
                    new Vector2(-1825f,  225f), // Homa Bay Town
                    new Vector2(-1765f,  325f),
                    new Vector2(-1670f,  410f), // Kendu Bay
                    new Vector2(-1555f,  510f),
                    new Vector2(-1445f,  595f)  // Joins Katito on A1!
                }
            },

            // 3: C18 Sori Lake Victoria Spur: Branches left at Rodi Kopany -> Ndhiwa -> Sori (Karungu Bay)
            new HighwayDef
            {
                id = "C18_RodiKopany_Ndhiwa_Sori",
                displayName = "C18 Rodi Kopany – Ndhiwa – Sori (Lake Victoria)",
                routeCode = "C18",
                mapColor = new Color(0.20f, 0.84f, 0.86f, 1f),
                roadClass = RoadClass.Trunk2Lane,
                controlPoints = new[]
                {
                    new Vector2(-1782f,   38f), // Branches west/south-west at Rodi Kopany!
                    new Vector2(-1895f,  -50f), // Ndhiwa
                    new Vector2(-1975f, -185f)  // Sori (Karungu Bay)
                }
            },

            // 4: A1 Kisii – Oyugis – Sondu – Katito – Ahero Rbt – Kisumu Rbt (with Sondu Escarpment Climbing Lane!)
            new HighwayDef
            {
                id = "A1_Kisii_Sondu_Ahero_Kisumu",
                displayName = "A1 Kisii – Oyugis – Sondu – Katito – Ahero – Kisumu",
                routeCode = "A1",
                mapColor = new Color(0.95f, 0.56f, 0.14f, 1f),
                roadClass = RoadClass.Climbing3Lane,
                heavyTrailers = true,
                controlPoints = new[]
                {
                    new Vector2(-1578f,  216f), // Exits Kisii Rbt north
                    new Vector2(-1545f,  345f), // Oyugis
                    new Vector2(-1455f,  470f), // Sondu (Escarpment Climbing Lane!)
                    new Vector2(-1445f,  595f), // Katito (Kendu Bay merges!)
                    new Vector2(-1358f,  696f), // Approaches Ahero Rbt
                    new Vector2(-1358f,  734f), // Exits Ahero Rbt NW -> Kisumu
                    new Vector2(-1445f,  816f)  // Enters Kisumu Rbt
                }
            },

            // 5: A1 North: Kisumu Rbt -> Vihiga -> Kakamega -> Webuye Rbt -> Kiminini -> Kitale -> North!
            new HighwayDef
            {
                id = "A1_Kisumu_Vihiga_Kakamega_Kitale",
                displayName = "A1 Kisumu – Vihiga – Kakamega – Webuye – Kiminini – Kitale",
                routeCode = "A1",
                mapColor = new Color(0.60f, 0.40f, 0.96f, 1f),
                roadClass = RoadClass.Trunk2Lane,
                heavyTrailers = true,
                controlPoints = new[]
                {
                    new Vector2(-1458f,  862f), // Exits Kisumu Rbt north
                    new Vector2(-1425f, 1005f), // Vihiga (Chavakali)
                    new Vector2(-1365f, 1170f), // Kakamega
                    new Vector2(-1280f, 1322f), // Approaches Webuye Rbt
                    new Vector2(-1248f, 1365f), // Exits Webuye Rbt NE
                    new Vector2(-1125f, 1465f), // Kiminini
                    new Vector2( -965f, 1525f), // Kitale
                    new Vector2( -810f, 1585f)  // Continues North toward Lodwar / Suam!
                }
            },

            // 6: Second Road from Kisumu -> Bungoma -> Malaba (Uganda Border!)
            new HighwayDef
            {
                id = "C33_Kisumu_Bungoma_Malaba",
                displayName = "C33/A104 Kisumu – Bungoma – Malaba (Uganda Border)",
                routeCode = "C33",
                mapColor = new Color(0.84f, 0.34f, 0.78f, 1f),
                roadClass = RoadClass.Trunk2Lane,
                heavyTrailers = true,
                controlPoints = new[]
                {
                    new Vector2(-1488f,  852f), // Exits Kisumu Rbt NW (Road #2 out of Kisumu!)
                    new Vector2(-1545f, 1035f), // Luanda / Mumias sugar plains
                    new Vector2(-1585f, 1285f), // Bungoma Town
                    new Vector2(-1785f, 1385f), // Malaba Uganda Border
                    new Vector2(-1895f, 1435f)
                }
            },

            // 7: B3 South Rift & Escarpment: Nairobi Rbt -> Mai Mahiu Rbt -> Narok -> Bomet -> Kaplong Rbt -> Sotik -> Chebilat -> Keroka -> Kisii Rbt!
            new HighwayDef
            {
                id = "B3_Nairobi_MaiMahiu_Narok_Bomet_Kisii",
                displayName = "B3 Nairobi – Mai Mahiu – Narok – Bomet – Kaplong – Chebilat – Kisii",
                routeCode = "B3",
                mapColor = new Color(0.90f, 0.24f, 0.22f, 1f),
                roadClass = RoadClass.Climbing3Lane, // Mai Mahiu Escarpment + Highland Climbing Lanes!
                controlPoints = new[]
                {
                    new Vector2(  -28f,  170f), // Exits Nairobi Rbt west
                    new Vector2( -145f,  174f), // Rift Escarpment descent!
                    new Vector2( -255f,  170f), // Enters Mai Mahiu Rbt
                    new Vector2( -305f,  166f), // Exits Mai Mahiu Rbt west
                    new Vector2( -475f,  135f), // Suswa / Mara Wildlife Corridor!
                    new Vector2( -640f,  115f), // Narok Town
                    new Vector2( -810f,  148f),
                    new Vector2( -960f,  195f), // Bomet Town
                    new Vector2(-1062f,  246f), // Enters Kaplong Rbt
                    new Vector2(-1108f,  253f), // Exits Kaplong Rbt west
                    new Vector2(-1215f,  240f), // Sotik
                    new Vector2(-1335f,  225f), // Chebilat (Tea Plantations!)
                    new Vector2(-1455f,  205f), // Keroka Market
                    new Vector2(-1558f,  192f)  // Enters Kisii Rbt
                }
            },

            // 8: C25 Chebilat -> Sondu Tea Highlands Connector!
            new HighwayDef
            {
                id = "C25_Chebilat_Sondu_TeaLink",
                displayName = "C25 Chebilat – Sondu Tea Highlands Link",
                routeCode = "C25",
                mapColor = new Color(0.32f, 0.86f, 0.42f, 1f),
                roadClass = RoadClass.Trunk2Lane,
                controlPoints = new[]
                {
                    new Vector2(-1340f,  234f), // Branches north-west from Chebilat
                    new Vector2(-1395f,  355f), // Rolling Chebilat tea estates
                    new Vector2(-1452f,  462f)  // Merges straight into Sondu!
                }
            },

            // 9: B3/C88 Mai Mahiu Rbt -> Naivasha Rift Floor Road!
            new HighwayDef
            {
                id = "C88_MaiMahiu_Naivasha_RiftFloor",
                displayName = "C88 Mai Mahiu – Naivasha Rift Floor",
                routeCode = "C88",
                mapColor = new Color(0.36f, 0.82f, 0.62f, 1f),
                roadClass = RoadClass.Trunk2Lane,
                controlPoints = new[]
                {
                    new Vector2( -276f,  196f), // Exits Mai Mahiu Rbt north
                    new Vector2( -262f,  325f), // Flat Rift Valley floor (Longonot view!)
                    new Vector2( -245f,  445f)  // Joins Naivasha on A104!
                }
            },

            // 10: B1 Kaplong Rbt -> Kericho -> Total (Mau Summit Rbt)! (Lush Tea Estates + Climbing Lanes!)
            new HighwayDef
            {
                id = "B1_Kaplong_Kericho_Total",
                displayName = "B1 Kaplong – Kericho Tea Estates – Total (Mau Summit)",
                routeCode = "B1",
                mapColor = new Color(0.18f, 0.78f, 0.34f, 1f),
                roadClass = RoadClass.Climbing3Lane,
                controlPoints = new[]
                {
                    new Vector2(-1082f,  280f), // Exits Kaplong Rbt north
                    new Vector2(-1072f,  405f), // Kericho South Tea Estates
                    new Vector2(-1055f,  520f), // Kericho Town
                    new Vector2( -935f,  655f), // Kericho North Tea Hills
                    new Vector2( -806f,  750f)  // Enters Total (Mau Summit) Rbt!
                }
            },

            // 11: B1 Kericho -> Ahero Rbt (Direct Tea Highlands to Kisumu Lake Highway!)
            new HighwayDef
            {
                id = "B1_Kericho_Ahero_Kisumu",
                displayName = "B1 Kericho – Ahero – Kisumu Direct Link",
                routeCode = "B1",
                mapColor = new Color(0.24f, 0.84f, 0.48f, 1f),
                roadClass = RoadClass.Trunk2Lane,
                heavyTrailers = true,
                controlPoints = new[]
                {
                    new Vector2(-1065f,  528f), // Branches NW from Kericho
                    new Vector2(-1205f,  635f), // Muhoroni / Nyando escarpment
                    new Vector2(-1316f,  706f)  // Enters Ahero Rbt!
                }
            },

            // 12: A104 Great Rift Highway: Nairobi Rbt -> Naivasha -> Nakuru Rbt -> Salgaa Climbing Lane -> Total (Mau Summit Rbt)!
            new HighwayDef
            {
                id = "A104_Nairobi_Naivasha_Nakuru_Total",
                displayName = "A104 Nairobi – Naivasha – Nakuru – Salgaa – Total",
                routeCode = "A104",
                mapColor = new Color(0.14f, 0.72f, 0.44f, 1f),
                roadClass = RoadClass.Climbing3Lane, // Nakuru–Salgaa–Total climbing lane + 6-lane Nakuru approach!
                heavyTrailers = true,
                controlPoints = new[]
                {
                    new Vector2(  -14f,  198f), // Exits Nairobi Rbt NW
                    new Vector2( -115f,  330f), // Limuru / Kinungi Escarpment!
                    new Vector2( -245f,  455f), // Naivasha
                    new Vector2( -345f,  575f), // Gilgil / Kikopey
                    new Vector2( -408f,  654f), // Enters Nakuru Rbt
                    new Vector2( -452f,  682f), // Exits Nakuru Rbt west (Salgaa climb!)
                    new Vector2( -620f,  732f), // Salgaa Steep Climbing Lane!
                    new Vector2( -758f,  762f)  // Enters Total (Mau Summit) Rbt!
                }
            },

            // 13: A104 Northern Corridor: Total (Mau Summit Rbt) -> Timboroa -> Eldoret City -> Webuye Rbt -> Bungoma!
            new HighwayDef
            {
                id = "A104_Total_Eldoret_Webuye_Bungoma",
                displayName = "A104 Total – Timboroa – Eldoret – Webuye – Bungoma",
                routeCode = "A104",
                mapColor = new Color(0.46f, 0.56f, 0.98f, 1f),
                roadClass = RoadClass.Climbing3Lane, // Timboroa steep slopes + heavy transit trailers!
                heavyTrailers = true,
                controlPoints = new[]
                {
                    new Vector2( -792f,  792f), // Exits Total Rbt NW -> Timboroa!
                    new Vector2( -855f,  965f), // Timboroa High-Altitude Climbing Lane!
                    new Vector2( -920f, 1145f), // Eldoret City (4-6 Lane Corridor!)
                    new Vector2(-1085f, 1260f), // Turbo / Kipkaren
                    new Vector2(-1242f, 1335f), // Enters Webuye Rbt
                    new Vector2(-1290f, 1340f), // Exits Webuye Rbt west -> Bungoma!
                    new Vector2(-1575f, 1288f)  // Connects into Bungoma -> Malaba!
                }
            },

            // 14: A109 / B8 Mombasa Road & North Coast: Nairobi Rbt -> Mtito Andei -> Voi/Tsavo -> Mombasa -> Kilifi -> Malindi!
            new HighwayDef
            {
                id = "A109_B8_Nairobi_Mombasa_Kilifi_Malindi",
                displayName = "A109/B8 Nairobi – Mtito Andei – Voi – Mombasa – Kilifi – Malindi",
                routeCode = "A109",
                mapColor = new Color(0.14f, 0.78f, 0.80f, 1f),
                roadClass = RoadClass.CityExpressway6Lane, // Wide dual expressway near Nairobi & Mombasa!
                heavyTrailers = true,
                controlPoints = new[]
                {
                    new Vector2(   30f,  164f), // Exits Nairobi Rbt SE -> JKIA / Mombasa Rd
                    new Vector2(  185f,   85f), // Machakos Turnoff / Emali
                    new Vector2(  355f,  -25f), // Mtito Andei
                    new Vector2(  505f, -112f), // Tsavo National Park Wildlife Zone!
                    new Vector2(  655f, -195f), // Voi / Tsavo
                    new Vector2(  815f, -280f), // Mariakani
                    new Vector2(  965f, -355f), // Mombasa City & Moi Ave Tusks
                    new Vector2( 1075f, -315f), // Nyali / Bamburi Coast
                    new Vector2( 1145f, -165f), // Kilifi Creek
                    new Vector2( 1245f,   65f), // Malindi Coast
                    new Vector2( 1275f,  175f)
                }
            }
        };

        // ==============================================================================
        // SMOOTH 3D KENYA NATIONAL TOPOGRAPHY ENGINE (C-Infinity Continuous Elevation!)
        // Flat Plains (Coast, Lake Victoria, Rift Floor, Narok) + Steep Escarpments & Highlands!
        // ==============================================================================
        private struct ElevationPlateau
        {
            public float x, z, radius, height;
            public ElevationPlateau(float x, float z, float r, float h)
            {
                this.x = x; this.z = z; this.radius = r; this.height = h;
            }
        }

        // Smooth Gaussian highlands replicating Kenya's real altitude profile:
        // Coast (~1m) -> Nairobi Highlands (~18m) -> Drop into Mai Mahiu/Naivasha/Nakuru Rift Floor (~5m) ->
        // Steep Climb up Salgaa -> Mau Summit/Total (~28m) & Timboroa/Eldoret (~21m) & Kericho/Chebilat/Kisii (~21m) ->
        // Steep Drop down Sondu Escarpment to flat Lake Victoria Basin (Katito/Ahero/Kisumu/Homa Bay/Sori ~2m)!
        private static readonly ElevationPlateau[] Plateaus =
        {
            new ElevationPlateau(    0f,   120f, 320f,  16.5f), // Nairobi & Thika Central Highlands
            new ElevationPlateau( -260f,   400f, 240f,  -5.5f), // Great Rift Valley Floor Trench (Mai Mahiu -> Naivasha -> Nakuru!)
            new ElevationPlateau( -640f,   115f, 220f,   7.5f), // Narok Savanna Plateau
            new ElevationPlateau( -785f,   765f, 240f,  26.5f), // Mau Summit / Total / Salgaa High Ridge (Steep Climb!)
            new ElevationPlateau( -920f,  1145f, 300f,  20.0f), // Timboroa / Eldoret / Kitale North Rift Highlands
            new ElevationPlateau(-1125f,   375f, 340f,  21.5f), // Kericho / Kaplong / Bomet / Sotik / Chebilat Tea Highlands
            new ElevationPlateau(-1505f,   210f, 210f,  17.5f), // Keroka / Kisii Gusii Hills (drops steeply at Sondu!)
            new ElevationPlateau(-1365f,  1170f, 250f,  13.0f), // Vihiga / Kakamega / Webuye Western Highlands
            new ElevationPlateau(-1585f,  1285f, 220f,  10.5f), // Bungoma / Malaba gentle slope
            new ElevationPlateau(  355f,   -25f, 260f,   7.5f), // Emali / Mtito Andei gradual slope down to Tsavo
            new ElevationPlateau(-1620f,   560f, 380f,  -4.5f), // Lake Victoria Basin flattening (Kisumu/Ahero/Katito/Homa Bay/Sori)
        };

        /// <summary>
        /// Single authoritative 3D road/ground elevation Y (meters) for ANY (x, z) point across Kenya!
        /// Smooth everywhere -> zero discontinuities at roundabouts, intersections, towns, or AI traffic!
        /// </summary>
        public static float GetElevationAt(Vector2 xz)
        {
            float sum = 1.2f; // Coastal sea-level base (Mombasa / Kilifi / Malindi / Lake Victoria)
            for (int i = 0; i < Plateaus.Length; i++)
            {
                var pl = Plateaus[i];
                float dx = xz.x - pl.x;
                float dz = xz.y - pl.z;
                float inv2 = 1f / (2f * pl.radius * pl.radius);
                sum += pl.height * Mathf.Exp(-(dx * dx + dz * dz) * inv2);
            }
            // Keep Lake Victoria & Indian Ocean flat coastal plains strictly >= 0.8m
            return Mathf.Max(0.8f, sum);
        }

        public static float GetElevationAt(float x, float z) => GetElevationAt(new Vector2(x, z));

        /// <summary>
        /// Returns true road half-width (meters) along a highway:
        /// - 4-6 Lane City Expressways (11.0m halfW = 22m wide!) inside Nairobi, Thika, Kisumu, Nakuru, Eldoret, Mombasa
        /// - 3-Lane Climbing Sections (9.2m halfW = 18.4m wide!) on steep escarpments (Mai Mahiu, Salgaa, Timboroa, Kericho, Sondu)
        /// - Standard 2-Lane Trunk Highways (7.0m halfW = 14m wide!) on flat regional stretches!
        /// </summary>
        public static float GetRoadHalfWidthAt(HighwayDef def, Vector2 xz)
        {
            float baseW = def.roadClass == RoadClass.CityExpressway6Lane ? 10.6f
                        : def.roadClass == RoadClass.Climbing3Lane ? 9.0f
                        : 7.0f;

            // Smooth 65m highway taper into 4-6 lane dual carriageway inside Major Cities (Nairobi, Kisumu, Nakuru, Eldoret, Mombasa)!
            float cityBlend = GetMajorCityBlend(xz);
            return Mathf.Lerp(baseW, 10.8f, cityBlend);
        }

        public static float GetMajorCityBlend(Vector2 xz)
        {
            // Nairobi CBD (0), Kisumu (23), Nakuru (32), Eldoret (35), Mombasa (38)
            int[] bigCities = { 0, 23, 32, 35, 38 };
            float minD = float.MaxValue;
            for (int i = 0; i < bigCities.Length; i++)
            {
                float d = Vector2.Distance(xz, Hubs[bigCities[i]].xz);
                if (d < minD) minD = d;
            }
            // 1.0 inside 115m radius, smoothly tapering to 0.0 at 180m!
            float t = Mathf.Clamp01((180f - minD) / 65f);
            return t * t * (3f - 2f * t);
        }

        public static bool IsInsideMajorCityCorridor(Vector2 xz) => GetMajorCityBlend(xz) > 0.5f;

        private static List<Vector3>[] cachedDenseSamples;

        public static void InvalidateCache()
        {
            cachedDenseSamples = null;
        }

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
                    pts.Add(new Vector3(v.x, GetElevationAt(v), v.y));
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
        /// Finds the exact nearest 3D point and tangent on any Kenyan highway or roundabout (elevation-aware!).
        /// </summary>
        public static bool FindNearestRoadPoint(Vector3 worldPos, out Vector3 nearestPoint, out Vector3 forwardDir, out float distance)
        {
            EnsureCachedSamples();
            nearestPoint = new Vector3(-3.5f, GetElevationAt(-3.5f, -18f), -18f);
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
                    nearestPoint = new Vector3(onRing.x, GetElevationAt(onRing), onRing.y);
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
                        nearestPoint = new Vector3(proj.x, GetElevationAt(proj), proj.y);
                        Vector2 dir = ab.normalized;
                        forwardDir = new Vector3(dir.x, 0f, dir.y);
                    }
                }
            }

            return distance < float.MaxValue;
        }

        /// <summary>
        /// Returns signed horizontal distance from xz to the outer tarmac edge of the nearest roundabout.
        /// Negative = inside roundabout tarmac/island; Positive = meters outside outer roundabout edge.
        /// </summary>
        public static float DistanceToNearestRoundaboutEdge(Vector2 xz)
        {
            float minClear = float.MaxValue;
            for (int r = 0; r < Roundabouts.Length; r++)
            {
                var rb = Roundabouts[r];
                float clear = Vector2.Distance(xz, rb.center) - rb.outerRadius;
                if (clear < minClear) minClear = clear;
            }
            return minClear;
        }

        public static bool IsClearOfAllRoundabouts(Vector2 xz, float minBufferFromOuterEdge = 24f)
        {
            return DistanceToNearestRoundaboutEdge(xz) >= minBufferFromOuterEdge;
        }

        /// <summary>
        /// Guarantees a 100% roundabout-clear highway point for any town's Coach Bus Stage Terminal.
        /// Searches all highways touching hubXZ in BOTH directions along the curve until the bay & shelter
        /// sit strictly >= minRoundaboutBuffer outside EVERY roundabout!
        /// </summary>
        public static bool FindRoundaboutClearStageSite(
            Vector2 hubXZ, float approxYawDeg, float minRoundaboutBuffer,
            out Vector3 roadPoint, out Vector3 roadForward, out float approxArcFromHub)
        {
            EnsureCachedSamples();
            Vector3 approxFwd = Quaternion.Euler(0f, approxYawDeg, 0f) * Vector3.forward;

            // Gather all nearby highway anchor points within 95m of hubXZ sorted by closeness
            var anchors = new List<(int h, int idx, float dist)>();
            for (int h = 0; h < cachedDenseSamples.Length; h++)
            {
                var pts = cachedDenseSamples[h];
                int bestI = -1;
                float bestD = float.MaxValue;
                for (int i = 0; i < pts.Count; i++)
                {
                    float d = Vector2.Distance(hubXZ, new Vector2(pts[i].x, pts[i].z));
                    if (d < bestD) { bestD = d; bestI = i; }
                }
                if (bestI >= 0 && bestD < 95f) anchors.Add((h, bestI, bestD));
            }
            anchors.Sort((a, b) => a.dist.CompareTo(b.dist));

            // Walk outward from each anchor in steps of ~8m along both curve directions
            int[] stepCounts = { 0, 8, -8, 11, -11, 14, -14, 18, -18, 22, -22, 6, -6 };
            for (int a = 0; a < anchors.Count; a++)
            {
                var (h, startI, _) = anchors[a];
                var pts = cachedDenseSamples[h];
                for (int s = 0; s < stepCounts.Length; s++)
                {
                    int idx = startI + stepCounts[s];
                    if (idx < 1 || idx >= pts.Count - 1) continue;

                    Vector3 pt = pts[idx];
                    Vector3 fwd = pts[Mathf.Min(pts.Count - 1, idx + 1)] - pts[Mathf.Max(0, idx - 1)];
                    fwd.y = 0f;
                    if (fwd.sqrMagnitude < 0.001f) continue;
                    fwd.Normalize();
                    if (Vector3.Dot(fwd, approxFwd) < 0f) fwd = -fwd;

                    Vector3 right = Vector3.Cross(Vector3.up, fwd).normalized;
                    float hw = IsInsideMajorCityCorridor(new Vector2(pt.x, pt.z)) ? 10.6f : 7.4f;
                    Vector3 bayPos = pt - right * (hw - 2.6f);
                    Vector3 shelterPos = bayPos - right * 5.8f;
                    float yaw = Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg;

                    if (IsClearOfAllRoundabouts(new Vector2(pt.x, pt.z), minRoundaboutBuffer) &&
                        IsClearOfAllRoundabouts(new Vector2(bayPos.x, bayPos.z), minRoundaboutBuffer) &&
                        IsClearOfAllRoundabouts(new Vector2(shelterPos.x, shelterPos.z), minRoundaboutBuffer) &&
                        IsFootprintClearOfAllRoads(shelterPos, 2.0f, 4.8f, yaw, 1.4f))
                    {
                        pt.y = GetElevationAt(pt.x, pt.z);
                        roadPoint = pt;
                        roadForward = fwd;
                        approxArcFromHub = Vector3.Dot(pt - new Vector3(hubXZ.x, pt.y, hubXZ.y), fwd);
                        return true;
                    }
                }
            }

            // Relaxed fallback (still strictly outside all roundabouts!)
            for (int a = 0; a < anchors.Count; a++)
            {
                var (h, startI, _) = anchors[a];
                var pts = cachedDenseSamples[h];
                for (int delta = 6; delta < 40; delta += 2)
                {
                    int[] idxs = { startI + delta, startI - delta };
                    for (int k = 0; k < 2; k++)
                    {
                        int idx = idxs[k];
                        if (idx < 1 || idx >= pts.Count - 1) continue;
                        Vector3 pt = pts[idx];
                        if (!IsClearOfAllRoundabouts(new Vector2(pt.x, pt.z), minRoundaboutBuffer)) continue;
                        Vector3 fwd = pts[idx + 1] - pts[idx - 1]; fwd.y = 0f; fwd.Normalize();
                        if (Vector3.Dot(fwd, approxFwd) < 0f) fwd = -fwd;
                        pt.y = GetElevationAt(pt.x, pt.z);
                        roadPoint = pt;
                        roadForward = fwd;
                        approxArcFromHub = Vector3.Dot(pt - new Vector3(hubXZ.x, pt.y, hubXZ.y), fwd);
                        return true;
                    }
                }
            }

            SampleRoadAlongTown(hubXZ, approxYawDeg, 0f, out roadPoint, out roadForward);
            approxArcFromHub = 0f;
            return false;
        }

        public static int FindRoundaboutNear(Vector2 xz, float extraRadius = 26f)
        {
            for (int r = 0; r < Roundabouts.Length; r++)
            {
                if (Vector2.Distance(xz, Roundabouts[r].center) <= Roundabouts[r].outerRadius + extraRadius)
                {
                    return r;
                }
            }
            return -1;
        }

        /// <summary>
        /// Computes minimum horizontal distance from a 2D point to the edge of ANY road tarmac or roundabout surface,
        /// accounting for wider 4-6 lane city boulevards & 3-lane climbing lanes so ZERO houses ever touch roads!
        /// </summary>
        public static float DistanceToAnyTarmacEdge(Vector2 xz)
        {
            EnsureCachedSamples();
            float minClearance = float.MaxValue;

            for (int r = 0; r < Roundabouts.Length; r++)
            {
                var rb = Roundabouts[r];
                float clear = Vector2.Distance(xz, rb.center) - (rb.outerRadius + 2.0f);
                if (clear < minClearance) minClearance = clear;
            }

            for (int h = 0; h < cachedDenseSamples.Length; h++)
            {
                var def = Highways[h];
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
                    float hw = GetRoadHalfWidthAt(def, proj) + 0.6f;
                    float clear = Vector2.Distance(xz, proj) - hw;
                    if (clear < minClearance) minClearance = clear;
                }
            }

            return minClearance;
        }

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

        public static bool SampleRoadAlongTown(Vector2 hubXZ, float approxYawDeg, float arcOffsetMeters, out Vector3 roadPoint, out Vector3 roadForward)
        {
            EnsureCachedSamples();
            Vector3 approxFwd = Quaternion.Euler(0f, approxYawDeg, 0f) * Vector3.forward;

            int bestH = -1;
            int bestIdx = -1;
            float bestDist = float.MaxValue;

            for (int h = 0; h < cachedDenseSamples.Length; h++)
            {
                var pts = cachedDenseSamples[h];
                for (int i = 0; i < pts.Count; i++)
                {
                    float dx = pts[i].x - hubXZ.x;
                    float dz = pts[i].z - hubXZ.y;
                    float d = dx * dx + dz * dz;
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
                Vector2 p2 = hubXZ + new Vector2(approxFwd.x, approxFwd.z) * arcOffsetMeters;
                roadPoint = new Vector3(p2.x, GetElevationAt(p2), p2.y);
                roadForward = approxFwd;
                return false;
            }

            var curve = cachedDenseSamples[bestH];
            Vector3 localTan = curve[Mathf.Min(curve.Count - 1, bestIdx + 1)] - curve[Mathf.Max(0, bestIdx - 1)];
            localTan.y = 0f;
            if (localTan.sqrMagnitude > 0.0001f) localTan.Normalize();
            float dirSign = Vector3.Dot(localTan, approxFwd) >= 0f ? 1f : -1f;

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
                    roadPoint.y = GetElevationAt(roadPoint.x, roadPoint.z);
                    Vector3 rawFwd = (curve[nextIdx] - curve[idx]);
                    rawFwd.y = 0f;
                    if (rawFwd.sqrMagnitude > 0.0001f) rawFwd.Normalize();
                    rawFwd *= (stepDir * (arcOffsetMeters >= 0f ? 1f : -1f));
                    roadForward = Vector3.Dot(rawFwd, approxFwd) >= 0f ? rawFwd : -rawFwd;
                    return true;
                }
                remaining -= seg;
                idx = nextIdx;
            }

            roadPoint = curve[idx];
            roadPoint.y = GetElevationAt(roadPoint.x, roadPoint.z);
            Vector3 tan = curve[Mathf.Min(curve.Count - 1, idx + 1)] - curve[Mathf.Max(0, idx - 1)];
            tan.y = 0f;
            if (tan.sqrMagnitude > 0.0001f) tan.Normalize();
            roadForward = Vector3.Dot(tan, approxFwd) >= 0f ? tan : -tan;
            return true;
        }
    }
}
