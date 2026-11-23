using System.Collections.Generic;
using UnityEngine;

namespace KenyaStreetSim
{
    /// <summary>
    /// Authentic Kenyan Bus & Isuzu FRR Parcel Routes Catalog across all 16 Kenyan towns & 8 highways.
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
                fullTitle = "ENA Coach South-Nyanza Express",
                highwaySummary = "CBD → B3 Narok → Bomet → Kisii → A1 Migori/Sirare",
                kind = RouteKind.BusPassenger,
                stopHubIndices = new[] { 0, 1, 2, 3, 4 },
                highwayIndices = new[] { 0, 1, 2 },
                bonusKes = 4500
            },
            new RouteDef
            {
                id = 1,
                shortName = "NAIROBI – NAKURU – KISUMU",
                fullTitle = "Rift Valley & Lake Victoria Bus Express",
                highwaySummary = "CBD → A104 Naivasha → Nakuru → B1 Kericho → Kisumu",
                kind = RouteKind.BusPassenger,
                stopHubIndices = new[] { 0, 5, 6, 7, 8 },
                highwayIndices = new[] { 0, 3, 4 },
                bonusKes = 5200
            },
            new RouteDef
            {
                id = 2,
                shortName = "NAIROBI – ELDORET – KAKAMEGA",
                fullTitle = "North-Rift & Western Bus Express",
                highwaySummary = "CBD → A104 Naivasha → Nakuru → Eldoret → C39 Kakamega",
                kind = RouteKind.BusPassenger,
                stopHubIndices = new[] { 0, 5, 6, 9, 10 },
                highwayIndices = new[] { 0, 3, 5 },
                bonusKes = 5800
            },
            new RouteDef
            {
                id = 3,
                shortName = "NAIROBI – MOMBASA – MALINDI",
                fullTitle = "Mombasa Road & Coastal Bus Express",
                highwaySummary = "CBD → A109 Mtito Andei → Voi/Tsavo → Mombasa → B8 Malindi",
                kind = RouteKind.BusPassenger,
                stopHubIndices = new[] { 0, 12, 13, 14, 15 },
                highwayIndices = new[] { 0, 7 },
                bonusKes = 6500
            },
            new RouteDef
            {
                id = 4,
                shortName = "KISUMU – HOMA BAY – MIGORI",
                fullTitle = "Lake Victoria South Bus Circuit",
                highwaySummary = "Kisumu → C19 Homa Bay → Kisii → A1 Migori/Sirare",
                kind = RouteKind.BusPassenger,
                stopHubIndices = new[] { 8, 11, 3, 4 },
                highwayIndices = new[] { 6, 2 },
                bonusKes = 4200
            },
            new RouteDef
            {
                id = 5,
                shortName = "FRR PARCEL: NAIROBI – ELDORET",
                fullTitle = "Isuzu FRR Rift Parcel Courier Run",
                highwaySummary = "Nairobi Parcel Hub → Naivasha → Nakuru → Eldoret Depot",
                kind = RouteKind.FrrParcelCargo,
                stopHubIndices = new[] { 0, 5, 6, 9 },
                highwayIndices = new[] { 0, 3, 5 },
                bonusKes = 6000
            },
            new RouteDef
            {
                id = 6,
                shortName = "FRR PARCEL: NAIROBI – MOMBASA",
                fullTitle = "Isuzu FRR Coast Freight & Parcel Run",
                highwaySummary = "Nairobi Parcel Hub → Mtito Andei → Voi → Mombasa Port Depot",
                kind = RouteKind.FrrParcelCargo,
                stopHubIndices = new[] { 0, 12, 13, 14 },
                highwayIndices = new[] { 0, 7 },
                bonusKes = 7200
            },
            new RouteDef
            {
                id = 7,
                shortName = "ALL-KENYA 16-TOWN TOUR",
                fullTitle = "Grand Republic of Kenya 16-Town Highway Tour",
                highwaySummary = "All 16 Kenyan Towns & 8 Highways",
                kind = RouteKind.GrandKenyaTour,
                stopHubIndices = new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15 },
                highwayIndices = new[] { 0, 1, 2, 3, 4, 5, 6, 7 },
                bonusKes = 15000
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

        /// <summary>
        /// Samples an ordered world-space polyline along the selected route's highways for 3D GPS guidance!
        /// </summary>
        public static List<Vector3> BuildOrderedRoutePoints(int routeIndex, float stepMeters = 10f)
        {
            var r = GetRoute(routeIndex);
            var pts = new List<Vector3>(256);
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
