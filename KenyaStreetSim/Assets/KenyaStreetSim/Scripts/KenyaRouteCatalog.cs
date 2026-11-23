using System.Collections.Generic;
using UnityEngine;

namespace KenyaStreetSim
{
    /// <summary>
    /// Authentic National Kenya Bus & Parcel Routes Catalog across all 41 Kenyan Towns & 15 Highways.
    /// Drives selectable route progression, [M] Map highlighted route overlays, Passenger/Parcel pins,
    /// and 3D Turn-by-Turn GPS highway guidance.
    /// </summary>
    public static class KenyaRouteCatalog
    {
        public enum RouteKind
        {
            BusPassenger,
            FrrParcelCargo,
            GrandKenyaTour
        }

        public struct RouteDef
        {
            public int id;
            public string shortName;
            public string fullTitle;
            public string highwaySummary;
            public RouteKind kind;
            public int[] stopHubIndices;
            public int[] highwayIndices;
            public int bonusKes;
        }

        public static readonly RouteDef[] Routes =
        {
            new RouteDef
            {
                id = 0,
                shortName = "NAIROBI – KISII – MIGORI",
                fullTitle = "B3/A1 South Rift & Gusii Express",
                highwaySummary = "Nairobi → Mai Mahiu → Narok → Bomet → Kaplong → Sotik → Chebilat → Keroka → Kisii → Suneka → Rongo → Awendo → Migori",
                kind = RouteKind.BusPassenger,
                stopHubIndices = new[] { 0, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13 },
                highwayIndices = new[] { 0, 7, 1 },
                bonusKes = 6200
            },
            new RouteDef
            {
                id = 1,
                shortName = "MIGORI – HOMA BAY – KISUMU",
                fullTitle = "A1/C20/C19 Lake Victoria Basin Shuttle",
                highwaySummary = "Migori → Awendo → Rongo → Rodi Kopany → Homa Bay → Kendu Bay → Katito → Ahero → Kisumu",
                kind = RouteKind.BusPassenger,
                stopHubIndices = new[] { 13, 12, 11, 14, 17, 18, 21, 22, 23 },
                highwayIndices = new[] { 1, 2, 4 },
                bonusKes = 5400
            },
            new RouteDef
            {
                id = 2,
                shortName = "KISII – SONDU – KISUMU – KITALE",
                fullTitle = "A1 Nyanza to Western & Trans-Nzoia Express",
                highwaySummary = "Kisii → Oyugis → Sondu → Katito → Ahero → Kisumu → Vihiga → Kakamega → Webuye → Kiminini → Kitale",
                kind = RouteKind.BusPassenger,
                stopHubIndices = new[] { 9, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28 },
                highwayIndices = new[] { 4, 5 },
                bonusKes = 6800
            },
            new RouteDef
            {
                id = 3,
                shortName = "NAIROBI – ELDORET – MALABA",
                fullTitle = "A104 Great Rift & Uganda Border Corridor",
                highwaySummary = "Nairobi → Naivasha → Nakuru → Salgaa/Total → Eldoret → Webuye → Bungoma → Malaba (Uganda)",
                kind = RouteKind.BusPassenger,
                stopHubIndices = new[] { 0, 31, 32, 33, 35, 26, 29, 30 },
                highwayIndices = new[] { 0, 12, 13, 6 },
                bonusKes = 7400
            },
            new RouteDef
            {
                id = 4,
                shortName = "NAIROBI – KERICHO – KISUMU",
                fullTitle = "A104/B1 Tea Highlands & Lake Express",
                highwaySummary = "Nairobi → Mai Mahiu → Naivasha → Nakuru → Total → Kericho → Ahero → Kisumu → Bungoma",
                kind = RouteKind.BusPassenger,
                stopHubIndices = new[] { 0, 2, 31, 32, 33, 34, 22, 23, 29 },
                highwayIndices = new[] { 0, 7, 9, 12, 10, 11, 4, 6 },
                bonusKes = 6600
            },
            new RouteDef
            {
                id = 5,
                shortName = "THIKA – NAIROBI – MOMBASA – MALINDI",
                fullTitle = "A2/A109/B8 Superhighway to Indian Ocean",
                highwaySummary = "Thika → Nairobi → Mtito Andei → Voi/Tsavo → Mombasa → Kilifi → Malindi",
                kind = RouteKind.BusPassenger,
                stopHubIndices = new[] { 1, 0, 36, 37, 38, 39, 40 },
                highwayIndices = new[] { 0, 14 },
                bonusKes = 7800
            },
            new RouteDef
            {
                id = 6,
                shortName = "PARCEL: NAIROBI – ELDORET – MALABA",
                fullTitle = "Savanna Parcel Northern Corridor Haul",
                highwaySummary = "Nairobi Depot → Naivasha → Nakuru → Salgaa/Total → Eldoret → Bungoma → Malaba",
                kind = RouteKind.FrrParcelCargo,
                stopHubIndices = new[] { 0, 31, 32, 33, 35, 29, 30 },
                highwayIndices = new[] { 0, 12, 13, 6 },
                bonusKes = 7600
            },
            new RouteDef
            {
                id = 7,
                shortName = "PARCEL: NAIROBI – MOMBASA – KILIFI",
                fullTitle = "Savanna Parcel Coast Port Express",
                highwaySummary = "Nairobi Depot → Mtito Andei → Voi/Tsavo → Mombasa Port → Kilifi → Malindi",
                kind = RouteKind.FrrParcelCargo,
                stopHubIndices = new[] { 0, 36, 37, 38, 39, 40 },
                highwayIndices = new[] { 0, 14 },
                bonusKes = 8200
            }
        };

        public static RouteDef GetRoute(int index)
        {
            int idx = Mathf.Clamp(index, 0, Routes.Length - 1);
            return Routes[idx];
        }

        public static bool IsHighwayOnRoute(int routeIndex, int highwayIndex)
        {
            var r = GetRoute(routeIndex);
            for (int i = 0; i < r.highwayIndices.Length; i++)
            {
                if (r.highwayIndices[i] == highwayIndex) return true;
            }
            return false;
        }

        public static bool IsHubOnRoute(int routeIndex, int hubIndex)
        {
            var r = GetRoute(routeIndex);
            for (int i = 0; i < r.stopHubIndices.Length; i++)
            {
                if (r.stopHubIndices[i] == hubIndex) return true;
            }
            return false;
        }

        public static List<Vector3> BuildOrderedRoutePoints(int routeIndex, float stepMeters = 10f)
        {
            var r = GetRoute(routeIndex);
            var pts = new List<Vector3>(512);
            for (int i = 0; i < r.highwayIndices.Length; i++)
            {
                int h = r.highwayIndices[i];
                if (h < 0 || h >= KenyaNetworkData.Highways.Length) continue;
                var seg = KenyaNetworkData.SampleHighway(KenyaNetworkData.Highways[h], stepMeters);
                for (int k = 0; k < seg.Count; k++)
                {
                    if (pts.Count == 0 || (pts[pts.Count - 1] - seg[k]).sqrMagnitude > 4f)
                    {
                        pts.Add(seg[k]);
                    }
                }
            }
            return pts;
        }
    }
}
