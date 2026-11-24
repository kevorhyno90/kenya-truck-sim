using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace KenyaStreetSim.Editor
{
    /// <summary>
    /// Builds all authentic real-Kenya highway & regional features across the 41-town network:
    /// 1. 4 International Airports (Nairobi JKIA, Kisumu Intl, Eldoret Intl, Mombasa Moi Intl) + Live Overhead Passing Jets!
    /// 2. 10 Authentic Kenyan Roadside Markets (Daraja Mbili/Kisii, Sondu, Ahero Rice, Kibuye/Kisumu, Chavakali, Keroka, Mai Mahiu, Salgaa, Mtito Andei, Voi)
    /// 3. Lush 3D Terraced Emerald Tea Plantations around Kericho, Chebilat, Sotik, Kaplong & Bomet!
    /// 4. Live Animated Savanna Wildlife & Animal Crossings at Narok/Suswa (Zebras, Giraffes, Gazelles) & Voi/Tsavo (Elephants & Zebras)!
    /// 5. KeNHA Climbing-Lane Escarpment Signage (Mai Mahiu, Salgaa-Total, Timboroa, Kericho, Sondu)!
    /// Every single structure is grounded to exact 3D elevation GetElevationAt(xz) and checked clear of all tarmac!
    /// </summary>
    public static class KenyaWorldFeaturesBuilder
    {
        public static void BuildAllKenyaWorldFeatures(Transform worldParent, Material matTarmac, Material matWhiteLine, Material matSteel)
        {
            var urpLit = Shader.Find("Universal Render Pipeline/Lit");
            var root = new GameObject("Kenya_Authentic_World_Features").transform;
            root.SetParent(worldParent, false);

            // Shared PBR feature materials
            Material matPlaneWhite = MakeMat("WF_PlaneWhite", urpLit, new Color(0.95f, 0.96f, 0.98f), 0.78f, 0.15f);
            Material matPlaneRed   = MakeMat("WF_KenyaRed",   urpLit, new Color(0.76f, 0.10f, 0.12f), 0.55f, 0.10f);
            Material matPlaneGreen = MakeMat("WF_KenyaGreen", urpLit, new Color(0.08f, 0.44f, 0.22f), 0.50f, 0.10f);
            Material matPlaneDark  = MakeMat("WF_PlaneDark",  urpLit, new Color(0.14f, 0.16f, 0.20f), 0.65f, 0.55f);
            Material matGlass      = MakeMat("WF_TowerGlass", urpLit, new Color(0.16f, 0.34f, 0.46f), 0.92f, 0.35f);
            Material matTerminal   = MakeMat("WF_Terminal",   urpLit, new Color(0.86f, 0.87f, 0.89f), 0.40f, 0.12f);
            Material matStrobe     = MakeMat("WF_Strobe",     urpLit, new Color(1.0f, 0.25f, 0.20f), 0.85f, 0.0f, new Color(1f, 0.22f, 0.18f) * 3.5f);

            // Tea plantation natural realistic deep Kenyan highland emerald shades (textured with organic foliage grain!)
            var teaNorm = AssetDatabase.LoadAssetAtPath<Texture2D>($"{KenyaPbrTextureGenerator.TexFolder}/CC0_PBR/CC0_SavannaTerrain_Normal.jpg")
                       ?? AssetDatabase.LoadAssetAtPath<Texture2D>($"{KenyaPbrTextureGenerator.TexFolder}/Murram_Normal.png");
            Material matTeaLight   = MakeMat("WF_TeaEmeraldBright", urpLit, new Color(0.18f, 0.44f, 0.16f), 0.14f, 0.0f, null, teaNorm, new Vector2(4f, 2f));
            Material matTeaDeep    = MakeMat("WF_TeaEmeraldDeep",   urpLit, new Color(0.13f, 0.35f, 0.12f), 0.12f, 0.0f, null, teaNorm, new Vector2(4f, 2f));
            Material matTeaMid     = MakeMat("WF_TeaEmeraldMid",    urpLit, new Color(0.22f, 0.49f, 0.18f), 0.15f, 0.0f, null, teaNorm, new Vector2(4f, 2f));
            Material matSoilDark   = MakeMat("WF_TeaSoil",          urpLit, new Color(0.26f, 0.17f, 0.10f), 0.10f, 0.0f);
            Material matWood       = MakeMat("WF_MarketWood",       urpLit, new Color(0.46f, 0.30f, 0.17f), 0.18f, 0.0f);

            // Market canopies & produce
            Material[] canopyMats =
            {
                MakeMat("WF_MarketBlue",   urpLit, new Color(0.12f, 0.42f, 0.78f), 0.25f, 0.0f),
                MakeMat("WF_MarketOrange", urpLit, new Color(0.88f, 0.40f, 0.10f), 0.25f, 0.0f),
                MakeMat("WF_MarketGreen",  urpLit, new Color(0.14f, 0.54f, 0.26f), 0.25f, 0.0f),
                MakeMat("WF_MarketGold",   urpLit, new Color(0.92f, 0.74f, 0.14f), 0.25f, 0.0f),
                MakeMat("WF_MarketRed",    urpLit, new Color(0.76f, 0.18f, 0.18f), 0.25f, 0.0f),
            };
            Material matProduceYel = MakeMat("WF_ProduceBananas",  urpLit, new Color(0.94f, 0.80f, 0.16f), 0.30f, 0.0f);
            Material matProduceRed = MakeMat("WF_ProduceTomatoes", urpLit, new Color(0.86f, 0.18f, 0.12f), 0.38f, 0.0f);
            Material matProduceBag = MakeMat("WF_ProduceSacks",    urpLit, new Color(0.76f, 0.68f, 0.50f), 0.12f, 0.0f);

            // Wildlife materials
            Material matZebraW     = MakeMat("WF_ZebraWhite",     urpLit, new Color(0.94f, 0.93f, 0.89f), 0.22f, 0.0f);
            Material matZebraB     = MakeMat("WF_ZebraBlack",     urpLit, new Color(0.10f, 0.10f, 0.11f), 0.22f, 0.0f);
            Material matGiraffeY   = MakeMat("WF_GiraffeOchre",   urpLit, new Color(0.86f, 0.66f, 0.28f), 0.20f, 0.0f);
            Material matGiraffeS   = MakeMat("WF_GiraffeSpot",    urpLit, new Color(0.42f, 0.22f, 0.08f), 0.20f, 0.0f);
            Material matGazelle    = MakeMat("WF_GazelleTan",     urpLit, new Color(0.74f, 0.52f, 0.30f), 0.22f, 0.0f);
            Material matElephant   = MakeMat("WF_TsavoElephant",  urpLit, new Color(0.46f, 0.40f, 0.37f), 0.18f, 0.0f); // Red-dusted Tsavo grey!
            Material matIvory      = MakeMat("WF_ElephantIvory",  urpLit, new Color(0.96f, 0.93f, 0.82f), 0.48f, 0.05f);
            Material matWarnYellow = MakeMat("WF_WarnYellow",     urpLit, new Color(0.96f, 0.78f, 0.08f), 0.42f, 0.08f);
            Material matSignBlue   = MakeMat("WF_KeNHABlue",      urpLit, new Color(0.08f, 0.30f, 0.66f), 0.42f, 0.10f);

            BuildFourKenyanAirports(root, matTarmac, matWhiteLine, matTerminal, matGlass, matPlaneWhite, matPlaneRed, matPlaneGreen, matPlaneDark, matStrobe, matSteel);
            BuildKenyanRoadsideMarkets(root, matWood, canopyMats, matProduceYel, matProduceRed, matProduceBag, matSignBlue, matSteel);
            // Continuous 3D sculpted Kericho/Chebilat/Kisii Tea Plantations + Kenyan Forests are now built batched in KenyaVegetationBuilder!
            BuildNarokAndTsavoWildlife(root, matZebraW, matZebraB, matGiraffeY, matGiraffeS, matGazelle, matElephant, matIvory, matWarnYellow, matSteel);
            BuildClimbingLaneSigns(root, matSignBlue, matWarnYellow, matSteel);
            BuildEscarpmentGuardrails(root);
            BuildKenyanBodaBodaStages(root);
            BuildKenhaRiverBridges(root);
            BuildRiftValleyRockyCliffs(root);
        }

        // =========================================================================
        // 1. FOUR INTERNATIONAL AIRPORTS + LIVE FLYING PLANES OVERHEAD!
        // =========================================================================
        private static void BuildFourKenyanAirports(
            Transform parent, Material matRunway, Material matStripe, Material matTerminal, Material matGlass,
            Material matPlaneWhite, Material matPlaneRed, Material matPlaneGreen, Material matPlaneDark, Material matStrobe, Material matSteel)
        {
            var airRoot = new GameObject("Kenya_4_Airports_And_Live_Planes").transform;
            airRoot.SetParent(parent, false);

            // Real geographic offsets beside Nairobi, Kisumu, Eldoret & Mombasa — all verified clear of highways!
            (string code, string label, Vector2 xz, float yawDeg, float orbitRx, float orbitRz, float phase)[] airports =
            {
                ("JKIA_Nairobi",    "JKIA — NAIROBI INTL AIRPORT",    new Vector2(  135f,  -45f),  28f, 185f, 125f, 0.2f),
                ("KIS_Kisumu",      "KISUMU INTL AIRPORT",            new Vector2(-1615f,  915f), -35f, 175f, 115f, 1.8f),
                ("EDL_Eldoret",     "ELDORET INTL AIRPORT",           new Vector2(-1075f, 1085f),  48f, 175f, 115f, 3.4f),
                ("MBA_Mombasa",     "MOI INTL AIRPORT — MOMBASA",     new Vector2(  840f, -455f),  62f, 180f, 120f, 5.0f),
            };

            for (int i = 0; i < airports.Length; i++)
            {
                var ap = airports[i];
                Vector2 pos2 = FindClearSiteNear(ap.xz, 28f, 52f, ap.yawDeg);
                float groundY = KenyaNetworkData.GetElevationAt(pos2);
                Vector3 center = new Vector3(pos2.x, groundY, pos2.y);

                var apGo = new GameObject($"Airport_{ap.code}").transform;
                apGo.SetParent(airRoot, false);
                apGo.SetPositionAndRotation(center, Quaternion.Euler(0f, ap.yawDeg, 0f));

                // Long Tarmac Runway Strip + White Centerline Dashes + Threshold Bars
                Box("RunwayTarmac", apGo, new Vector3(0f, 0.05f, 0f), new Vector3(18f, 0.10f, 115f), matRunway);
                for (int d = -48; d <= 48; d += 12)
                {
                    Box($"RwyDash_{d}", apGo, new Vector3(0f, 0.11f, d), new Vector3(0.45f, 0.03f, 6.0f), matStripe);
                }
                for (int end = -1; end <= 1; end += 2)
                {
                    for (int bar = -2; bar <= 2; bar++)
                    {
                        Box($"Thresh_{end}_{bar}", apGo, new Vector3(bar * 2.4f, 0.11f, end * 52f), new Vector3(1.1f, 0.03f, 6.2f), matStripe);
                    }
                }

                // Terminal Apron + Modern Glass Terminal Building + Bold Signboard
                Box("TerminalApron", apGo, new Vector3(20f, 0.04f, 0f), new Vector3(24f, 0.08f, 52f), matRunway);
                Box("TerminalHall",  apGo, new Vector3(31f, 4.2f, 0f), new Vector3(14f, 8.4f, 38f), matTerminal);
                Box("TerminalGlass", apGo, new Vector3(23.8f, 4.4f, 0f), new Vector3(0.5f, 5.2f, 32f), matGlass);
                Box("TerminalRoof",  apGo, new Vector3(29f, 8.7f, 0f), new Vector3(18f, 0.65f, 41f), matPlaneRed);
                WorldSign("AirportTitle", apGo, ap.label, new Vector3(23.2f, 7.2f, 0f), new Vector2(30f, 2.2f), 8.2f, Color.white, Quaternion.Euler(0f, -90f, 0f));

                // Iconic Airport Air Traffic Control Tower
                Cyl("ATCTowerShaft", apGo, new Vector3(32f, 11f, -24f), new Vector3(3.8f, 11f, 3.8f), matTerminal, Quaternion.identity);
                Cyl("ATCCabGlass",   apGo, new Vector3(32f, 23.2f, -24f), new Vector3(6.2f, 1.9f, 6.2f), matGlass, Quaternion.identity);
                Cyl("ATCCabRoof",    apGo, new Vector3(32f, 25.4f, -24f), new Vector3(6.8f, 0.45f, 6.8f), matPlaneRed, Quaternion.identity);
                Cyl("ATCRadar",      apGo, new Vector3(32f, 27.0f, -24f), new Vector3(0.28f, 1.6f, 0.28f), matSteel, Quaternion.identity);

                // LIVE FLYING KENYAN COMMERCIAL JET OVERHEAD!
                var planeGo = BuildCommercialJet($"FlyingJet_{ap.code}", airRoot, matPlaneWhite, matPlaneRed, matPlaneGreen, matPlaneDark, matGlass, matStrobe, out Renderer strobeRend);
                var ai = planeGo.AddComponent<KenyaAirportPlaneAI>();
                // Center the wide flight orbit between the airport and the city hub so jets sweep dramatically over the city road!
                ai.airportCenter = center;
                ai.orbitRadiusX = ap.orbitRx;
                ai.orbitRadiusZ = ap.orbitRz;
                ai.cruiseHeightAboveAirport = 48f;
                ai.minApproachHeight = 19f;
                ai.speedRadPerSec = 0.21f;
                ai.phaseOffset = ap.phase;
                ai.strobeRenderer = strobeRend;
            }
        }

        private static GameObject BuildCommercialJet(
            string name, Transform parent,
            Material matWhite, Material matRed, Material matGreen, Material matDark, Material matGlass, Material matStrobe,
            out Renderer strobeRenderer)
        {
            var g = new GameObject(name);
            g.transform.SetParent(parent, false);
            var t = g.transform;

            // 26m Kenyan Dreamliner / B737 silhouette (+Z nose forward)
            Cyl("Fuselage", t, new Vector3(0f, 0f, 0f), new Vector3(2.8f, 11.5f, 2.8f), matWhite, Quaternion.Euler(90f, 0f, 0f));
            var nose = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            nose.name = "NoseCone";
            nose.transform.SetParent(t, false);
            nose.transform.localPosition = new Vector3(0f, -0.05f, 11.4f);
            nose.transform.localScale = new Vector3(2.75f, 2.65f, 4.8f);
            Object.DestroyImmediate(nose.GetComponent<Collider>());
            nose.GetComponent<Renderer>().sharedMaterial = matWhite;

            Box("CockpitGlass", t, new Vector3(0f, 0.55f, 11.6f), new Vector3(1.85f, 0.65f, 1.4f), matGlass);
            // Kenyan Pride Crimson/Green Cheatline Stripe along fuselage
            Box("RedStripe", t, new Vector3(0f, -0.15f, 0.5f), new Vector3(2.86f, 0.36f, 20.5f), matRed);
            Box("GreenStripe", t, new Vector3(0f, -0.52f, 0.5f), new Vector3(2.85f, 0.24f, 19.5f), matGreen);

            // Swept Main Wings (left & right)
            Box("WingL", t, new Vector3(-7.4f, -0.25f, -0.5f), new Vector3(13.8f, 0.36f, 3.6f), matWhite, Quaternion.Euler(0f, 18f, -3f));
            Box("WingR", t, new Vector3( 7.4f, -0.25f, -0.5f), new Vector3(13.8f, 0.36f, 3.6f), matWhite, Quaternion.Euler(0f, -18f, 3f));
            Box("WingletL", t, new Vector3(-13.8f, 0.55f, -2.5f), new Vector3(0.28f, 1.85f, 1.35f), matRed, Quaternion.Euler(0f, 0f, 18f));
            Box("WingletR", t, new Vector3( 13.8f, 0.55f, -2.5f), new Vector3(0.28f, 1.85f, 1.35f), matRed, Quaternion.Euler(0f, 0f, -18f));

            // Twin Underwing Jet Turbines
            Cyl("EngineL", t, new Vector3(-4.6f, -1.10f, 1.2f), new Vector3(1.55f, 2.15f, 1.55f), matDark, Quaternion.Euler(90f, 0f, 0f));
            Cyl("EngineR", t, new Vector3( 4.6f, -1.10f, 1.2f), new Vector3(1.55f, 2.15f, 1.55f), matDark, Quaternion.Euler(90f, 0f, 0f));

            // Tail Fin (Kenyan Red + Green Accent) & Horizontal Stabilizers
            Box("TailFin",   t, new Vector3(0f, 3.3f, -10.5f), new Vector3(0.38f, 5.2f, 3.8f), matRed, Quaternion.Euler(-22f, 0f, 0f));
            Box("TailBand",  t, new Vector3(0f, 3.6f, -10.3f), new Vector3(0.44f, 1.2f, 3.2f), matGreen, Quaternion.Euler(-22f, 0f, 0f));
            Box("TailStabL", t, new Vector3(-3.3f, 0.25f, -10.8f), new Vector3(6.2f, 0.26f, 2.0f), matWhite, Quaternion.Euler(0f, 20f, 0f));
            Box("TailStabR", t, new Vector3( 3.3f, 0.25f, -10.8f), new Vector3(6.2f, 0.26f, 2.0f), matWhite, Quaternion.Euler(0f, -20f, 0f));

            // Belly Navigation Strobe Light
            var strobe = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            strobe.name = "BellyStrobe";
            strobe.transform.SetParent(t, false);
            strobe.transform.localPosition = new Vector3(0f, -1.48f, 0f);
            strobe.transform.localScale = Vector3.one * 0.65f;
            Object.DestroyImmediate(strobe.GetComponent<Collider>());
            strobeRenderer = strobe.GetComponent<Renderer>();
            strobeRenderer.sharedMaterial = matStrobe;

            return g;
        }

        // =========================================================================
        // 2. TEN AUTHENTIC KENYAN ROADSIDE MARKETS!
        // =========================================================================
        private static void BuildKenyanRoadsideMarkets(
            Transform parent, Material matWood, Material[] canopyMats,
            Material matBananas, Material matTomatoes, Material matSacks, Material matSignBlue, Material matSteel)
        {
            var mktRoot = new GameObject("Kenya_10_Roadside_Markets").transform;
            mktRoot.SetParent(parent, false);

            // Exact real Kenyan highway roadside markets positioned cleanly at each town's highway gateway (zero overlap with town shops!):
            (int hubIndex, string marketTitle, float arcOffset, float sideSign)[] markets =
            {
                ( 9, "DARAJA MBILI MARKET — KISII",  -62f,  1f),
                (20, "SONDU HIGHWAY MARKET",          52f, -1f),
                (22, "AHERO RICE MARKET",            -56f,  1f),
                (23, "KIBUYE MARKET — KISUMU",       -72f, -1f),
                (24, "CHAVAKALI MARKET — VIHIGA",     52f,  1f),
                ( 8, "KEROKA HIGHLAND MARKET",        52f, -1f),
                ( 2, "MAI MAHIU ROAD MARKET",         56f,  1f),
                (33, "SALGAA HIGHWAY MARKET",        -62f, -1f),
                (36, "MTITO ANDEI MARKET",           -56f,  1f),
                (37, "VOI ROADSIDE MARKET",           62f, -1f),
            };

            for (int m = 0; m < markets.Length; m++)
            {
                var mk = markets[m];
                var hub = KenyaNetworkData.Hubs[mk.hubIndex];
                float[] arcTries = { mk.arcOffset, mk.arcOffset * 1.35f, -mk.arcOffset * 1.25f, mk.arcOffset * 1.65f };
                Vector3 pos = Vector3.zero;
                float yaw = 0f;
                bool foundMarketSite = false;
                for (int at = 0; at < arcTries.Length && !foundMarketSite; at++)
                {
                    if (!KenyaNetworkData.SampleRoadAlongTown(hub.xz, hub.roadYawDeg, arcTries[at], out Vector3 rPt, out Vector3 rFwd))
                        continue;
                    Vector3 right = Vector3.Cross(Vector3.up, rFwd).normalized;
                    yaw = Mathf.Atan2(rFwd.x, rFwd.z) * Mathf.Rad2Deg + (mk.sideSign > 0f ? -90f : 90f);
                    float[] latTries = { 24.5f, 30.5f, 36.5f };
                    for (int lt = 0; lt < latTries.Length; lt++)
                    {
                        Vector3 cand = rPt + right * (mk.sideSign * latTries[lt]);
                        if (KenyaNetworkData.IsClearOfAllRoundabouts(new Vector2(cand.x, cand.z), 28f) &&
                            KenyaNetworkData.IsFootprintClearOfAllRoads(cand, 7.5f, 14f, yaw, 3.0f))
                        {
                            pos = cand;
                            foundMarketSite = true;
                            break;
                        }
                    }
                }
                if (!foundMarketSite) continue;
                pos.y = KenyaNetworkData.GetElevationAt(pos.x, pos.z);

                var mGo = new GameObject($"Market_{m}_{hub.name}").transform;
                mGo.SetParent(mktRoot, false);
                mGo.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));

                // Bold roadside Market Welcome Arch / Signboard facing passing highway traffic
                Box("SignPostL", mGo, new Vector3(-5.2f, 2.0f, 5.2f), new Vector3(0.16f, 4.0f, 0.16f), matSteel);
                Box("SignPostR", mGo, new Vector3( 5.2f, 2.0f, 5.2f), new Vector3(0.16f, 4.0f, 0.16f), matSteel);
                Box("SignBoard", mGo, new Vector3( 0.0f, 3.6f, 5.2f), new Vector3(11.2f, 1.35f, 0.18f), matSignBlue);
                WorldSign("MarketName", mGo, mk.marketTitle, new Vector3(0f, 3.6f, 5.34f), new Vector2(10.8f, 1.2f), 4.6f, Color.white, Quaternion.Euler(0f, 180f, 0f));

                // Prefer real sculpted Blender 5.3 Kenyan Market Cluster (.glb), with procedural fallback!
                var marketPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/KenyaStreetSim/ImportedModels/KenyaWorldKit/Kenya_Market_Cluster.glb");
                if (marketPrefab != null)
                {
                    var inst = (GameObject)PrefabUtility.InstantiatePrefab(marketPrefab);
                    inst.name = "BlenderMarketCluster";
                    inst.transform.SetParent(mGo, false);
                    inst.transform.localPosition = new Vector3(0f, 0f, -0.5f);
                    inst.transform.localRotation = Quaternion.identity;
                    inst.transform.localScale = new Vector3(1.18f, 1.05f, 1.15f);
                    continue;
                }

                // Fallback: Row of 6 vibrant Kenyan open-air wooden market stalls with colorful canvas tarps & fresh produce!
                for (int s = 0; s < 6; s++)
                {
                    float sx = -9.5f + s * 3.8f;
                    float sz = (s % 2 == 0) ? 0.8f : -3.4f;
                    var stall = new GameObject($"Stall_{s}").transform;
                    stall.SetParent(mGo, false);
                    stall.localPosition = new Vector3(sx, 0f, sz);

                    // 4 wooden posts + tilted colorful canvas awning
                    Box("P1", stall, new Vector3(-1.35f, 1.25f, -1.0f), new Vector3(0.11f, 2.5f, 0.11f), matWood);
                    Box("P2", stall, new Vector3( 1.35f, 1.25f, -1.0f), new Vector3(0.11f, 2.5f, 0.11f), matWood);
                    Box("P3", stall, new Vector3(-1.35f, 1.10f,  1.0f), new Vector3(0.11f, 2.2f, 0.11f), matWood);
                    Box("P4", stall, new Vector3( 1.35f, 1.10f,  1.0f), new Vector3(0.11f, 2.2f, 0.11f), matWood);
                    Box("Canopy", stall, new Vector3(0f, 2.38f, 0f), new Vector3(3.15f, 0.09f, 2.55f), canopyMats[(m + s) % canopyMats.Length], Quaternion.Euler(8f, 0f, 0f));

                    // Wooden produce table + bright fresh Kenyan produce crates (bananas, tomatoes, rice/maize sacks!)
                    Box("Table", stall, new Vector3(0f, 0.82f, 0.1f), new Vector3(2.65f, 0.12f, 1.35f), matWood);
                    Box("Crate1", stall, new Vector3(-0.72f, 1.04f, 0.12f), new Vector3(0.75f, 0.34f, 0.72f), matBananas);
                    Box("Crate2", stall, new Vector3( 0.10f, 1.02f, 0.15f), new Vector3(0.70f, 0.30f, 0.70f), matTomatoes);
                    Cyl("Sack",   stall, new Vector3( 0.85f, 0.52f, 0.35f), new Vector3(0.62f, 0.52f, 0.62f), matSacks, Quaternion.identity);
                }
            }
        }

        // =========================================================================
        // 3. LUSH 3D TERRACED EMERALD TEA PLANTATIONS (KERICHO & CHEBILAT / SOTIK)!
        // =========================================================================
        private static void BuildTeaPlantations(
            Transform parent, Material matTeaLight, Material matTeaMid, Material matTeaDeep, Material matSoil, Material matSignBlue, Material matSteel)
        {
            var teaRoot = new GameObject("Kenya_Kericho_Chebilat_Tea_Plantations").transform;
            teaRoot.SetParent(parent, false);

            var matBark = AssetDatabase.LoadAssetAtPath<Material>("Assets/KenyaStreetSim/Materials/M_AcaciaBarkPBR.mat") ?? matSoil;
            var matLeaf = AssetDatabase.LoadAssetAtPath<Material>("Assets/KenyaStreetSim/Materials/M_AcaciaLeafAlpha.mat") ?? matTeaDeep;
            Material[] teaShades = { matTeaLight, matTeaMid, matTeaDeep };

            // Real Kenyan emerald highland tea belts: Kericho North/South, Chebilat, Sotik, Kaplong & Bomet!
            // Compact manicured organic bush rows packed tightly together like real Kericho tea estates!
            (string estateName, Vector2 center, float radiusX, float radiusZ, int rows, int cols, float yawDeg)[] estates =
            {
                ("KERICHO HIGHLAND TEA ESTATES",     new Vector2(-1005f, 475f), 58f, 42f, 9, 7,  14f),
                ("KERICHO NORTH TEA BELT",           new Vector2( -948f, 598f), 54f, 40f, 8, 6, -24f),
                ("KERICHO WEST TEA HILLS",           new Vector2(-1142f, 535f), 52f, 38f, 8, 6,  32f),
                ("CHEBILAT & SOTIK TEA PLANTATIONS", new Vector2(-1280f, 282f), 60f, 42f, 9, 7, -12f),
                ("CHEBILAT – SONDU TEA RIDGE",       new Vector2(-1348f, 342f), 50f, 36f, 7, 6,  22f),
                ("KAPLONG – BOMET TEA GARDENS",      new Vector2(-1025f, 295f), 54f, 38f, 8, 6,  -8f),
            };

            var rng = new System.Random(25477);
            for (int e = 0; e < estates.Length; e++)
            {
                var es = estates[e];
                var eGo = new GameObject($"TeaEstate_{e}").transform;
                eGo.SetParent(teaRoot, false);
                Quaternion rot = Quaternion.Euler(0f, es.yawDeg, 0f);

                bool placedSign = false;
                for (int r = 0; r < es.rows; r++)
                {
                    float tz = ((float)r / Mathf.Max(1, es.rows - 1) - 0.5f) * 2f * es.radiusZ;
                    for (int c = 0; c < es.cols; c++)
                    {
                        float tx = ((float)c / Mathf.Max(1, es.cols - 1) - 0.5f) * 2f * es.radiusX;
                        Vector3 offset = rot * new Vector3(tx, 0f, tz);
                        Vector2 wp2 = es.center + new Vector2(offset.x, offset.z);

                        // Strictly stay clear of highways, roundabouts, and town buildings!
                        if (KenyaNetworkData.DistanceToAnyTarmacEdge(wp2) < 18f) continue;

                        float gy = KenyaNetworkData.GetElevationAt(wp2);
                        float segLen = (es.radiusX * 2f / es.cols) * 1.18f; // Overlapping rounded hedges -> lush continuous green carpet!
                        float hedgeW = (es.radiusZ * 2f / es.rows) * 0.96f;

                        // Rounded organic plucked-tea dome row (flattened ellipsoid bush cluster, NOT a hard boxy cube!)
                        Vector3 ahead2 = rot * new Vector3(4f, 0f, 0f);
                        float gyAhead = KenyaNetworkData.GetElevationAt(wp2.x + ahead2.x, wp2.y + ahead2.z);
                        float slopePitch = Mathf.Atan2(gyAhead - gy, 4f) * Mathf.Rad2Deg;

                        Quaternion bushRot = rot * Quaternion.Euler(0f, 0f, slopePitch);
                        Material mat = teaShades[(r * 3 + c + e) % teaShades.Length];
                        var bush = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                        bush.name = $"TeaBush_{r}_{c}";
                        bush.transform.SetParent(eGo, false);
                        bush.transform.SetPositionAndRotation(new Vector3(wp2.x, gy + 0.25f, wp2.y), bushRot);
                        bush.transform.localScale = new Vector3(segLen, 1.85f + (float)rng.NextDouble() * 0.45f, hedgeW);
                        Object.DestroyImmediate(bush.GetComponent<Collider>());
                        bush.GetComponent<Renderer>().sharedMaterial = mat;

                        // Estate signboard near the road-facing edge of the first valid patch!
                        if (!placedSign && KenyaNetworkData.DistanceToAnyTarmacEdge(wp2) < 42f)
                        {
                            placedSign = true;
                            Box($"EstatePost_{e}", eGo, new Vector3(wp2.x, gy + 1.8f, wp2.y), new Vector3(0.18f, 3.6f, 0.18f), matSteel);
                            Box($"EstateBoard_{e}", eGo, new Vector3(wp2.x, gy + 3.1f, wp2.y), new Vector3(9.4f, 1.35f, 0.16f), matSignBlue, rot);
                            WorldSign($"EstateTxt_{e}", eGo, es.estateName, new Vector3(wp2.x, gy + 3.1f, wp2.y) + rot * new Vector3(0f, 0f, -0.12f), new Vector2(9.0f, 1.2f), 4.0f, Color.white, rot);
                        }
                        else if (rng.NextDouble() < 0.10 && KenyaSculptedMeshFactory.TreeTrunkMesh != null)
                        {
                            // Genuine branching Kenyan highland shade tree rising through the manicured tea rows!
                            var tr = new GameObject($"TeaShadeTree_{r}_{c}").transform;
                            tr.SetParent(eGo, false);
                            tr.position = new Vector3(wp2.x, gy - 0.05f, wp2.y);
                            float tyaw = (float)(rng.NextDouble() * 360.0);
                            float tscl = 1.15f + (float)rng.NextDouble() * 0.45f;
                            AddMeshChild("Trunk", tr, Quaternion.Euler(0f, tyaw, 0f), Vector3.one * tscl, KenyaSculptedMeshFactory.TreeTrunkMesh, matBark);
                            AddMeshChild("Crown", tr, Quaternion.Euler(0f, tyaw, 0f), Vector3.one * tscl, KenyaSculptedMeshFactory.TreeFoliageMesh, matLeaf);
                        }
                    }
                }
            }
        }

        private static void AddMeshChild(string name, Transform parent, Quaternion rot, Vector3 scale, Mesh mesh, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = rot;
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        }

        // =========================================================================
        // 4. LIVE ANIMATED WILDLIFE & ANIMAL CROSSINGS (NAROK / MARA & VOI / TSAVO)!
        // =========================================================================
        private static void BuildNarokAndTsavoWildlife(
            Transform parent,
            Material matZebraW, Material matZebraB, Material matGiraffeY, Material matGiraffeS,
            Material matGazelle, Material matElephant, Material matIvory, Material matWarnYellow, Material matSteel)
        {
            var wildRoot = new GameObject("Kenya_Narok_Tsavo_Wildlife_Crossings").transform;
            wildRoot.SetParent(parent, false);

            // Crossing 1: Narok / Suswa Maasai Mara Gateway (Highway 7 progress ~0.34 -> Zebras, Giraffes & Gazelles!)
            BuildWildlifeZone(wildRoot, highwayIdx: 7, progress: 0.34f, zoneName: "Narok_Suswa_Mara_Crossing", signText: "CAUTION\nNAROK WILDLIFE\nCROSSING",
                isTsavoElephants: false, matZebraW, matZebraB, matGiraffeY, matGiraffeS, matGazelle, matElephant, matIvory, matWarnYellow, matSteel);

            // Crossing 2: West Narok Savanna Plains (Highway 7 progress ~0.52 -> Giraffes & Zebras!)
            BuildWildlifeZone(wildRoot, highwayIdx: 7, progress: 0.52f, zoneName: "Narok_West_Savanna_Crossing", signText: "CAUTION\nANIMAL CROSSING\nSLOW DOWN",
                isTsavoElephants: false, matZebraW, matZebraB, matGiraffeY, matGiraffeS, matGazelle, matElephant, matIvory, matWarnYellow, matSteel);

            // Crossing 3: Voi / Tsavo National Park Corridor (Highway 14 progress ~0.36 -> Red Tsavo Elephants & Zebras!)
            BuildWildlifeZone(wildRoot, highwayIdx: 14, progress: 0.36f, zoneName: "Tsavo_Voi_Elephant_Crossing", signText: "CAUTION\nTSAVO ELEPHANT\nCROSSING",
                isTsavoElephants: true, matZebraW, matZebraB, matGiraffeY, matGiraffeS, matGazelle, matElephant, matIvory, matWarnYellow, matSteel);
        }

        private static void BuildWildlifeZone(
            Transform parent, int highwayIdx, float progress, string zoneName, string signText, bool isTsavoElephants,
            Material matZebraW, Material matZebraB, Material matGiraffeY, Material matGiraffeS,
            Material matGazelle, Material matElephant, Material matIvory, Material matWarnYellow, Material matSteel)
        {
            var samples = KenyaNetworkData.SampleHighway(KenyaNetworkData.Highways[highwayIdx], 6f);
            if (samples == null || samples.Count < 8) return;
            int idx = Mathf.Clamp(Mathf.RoundToInt(progress * (samples.Count - 1)), 4, samples.Count - 5);

            Vector3 c = samples[idx];
            Vector3 fwd = (samples[idx + 1] - samples[idx - 1]);
            fwd.y = 0f; fwd.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, fwd).normalized;

            var zoneGo = new GameObject(zoneName).transform;
            zoneGo.SetParent(parent, false);

            // Warning Signs on BOTH approaches strictly verified OFF all tarmac & OFF all roundabouts!
            var hDefW = KenyaNetworkData.Highways[highwayIdx];
            for (int dir = -1; dir <= 1; dir += 2)
            {
                Vector3 basePt = c + fwd * (dir * 42f);
                float localHw = KenyaNetworkData.GetRoadHalfWidthAt(hDefW, new Vector2(basePt.x, basePt.z));
                Vector3 signPos = basePt + right * (dir * -(localHw + 4.8f));
                bool signClear = false;
                for (int push = 0; push < 5; push++)
                {
                    Vector3 cand = basePt + right * (dir * -(localHw + 4.8f + push * 2.5f));
                    Vector2 candXZ = new Vector2(cand.x, cand.z);
                    if (KenyaNetworkData.DistanceToAnyTarmacEdge(candXZ) >= 3.2f &&
                        KenyaNetworkData.IsClearOfAllRoundabouts(candXZ, 32f))
                    {
                        signPos = cand;
                        signClear = true;
                        break;
                    }
                }
                if (!signClear) continue;
                signPos.y = KenyaNetworkData.GetElevationAt(signPos.x, signPos.z);
                Quaternion signRot = Quaternion.LookRotation(-fwd * dir, Vector3.up);
                var sGo = new GameObject($"WildSign_{dir}").transform;
                sGo.SetParent(zoneGo, false);
                sGo.SetPositionAndRotation(signPos, signRot);
                Box("Post", sGo, new Vector3(0f, 1.6f, 0f), new Vector3(0.12f, 3.2f, 0.12f), matSteel);
                Box("Diamond", sGo, new Vector3(0f, 2.75f, 0f), new Vector3(1.55f, 1.55f, 0.10f), matWarnYellow, Quaternion.Euler(0f, 0f, 45f));
                WorldSign("WarnTxt", sGo, signText, new Vector3(0f, 2.75f, -0.09f), new Vector2(1.9f, 1.4f), 2.1f, Color.black, Quaternion.identity);
            }

            if (isTsavoElephants)
            {
                // 2 Majestic Tsavo Elephants calmly crossing highway + 2 grazing beside the red savanna!
                for (int i = 0; i < 2; i++)
                {
                    Vector3 a = c + fwd * (i * 9f - 4f) - right * 25f;
                    Vector3 b = c + fwd * (i * 9f - 4f) + right * 25f;
                    BuildElephant(zoneGo, $"TsavoElephant_Cross_{i}", a, b, isGrazer: false, phase: 0.15f + i * 0.42f, scale: 1.0f - i * 0.14f, matElephant, matIvory);
                }
                for (int i = 0; i < 2; i++)
                {
                    Vector3 gPos = c + fwd * (16f + i * 11f) + right * (24f + i * 7f);
                    BuildElephant(zoneGo, $"TsavoElephant_Graze_{i}", gPos, gPos, isGrazer: true, phase: i * 1.1f, scale: 1.05f, matElephant, matIvory);
                }
                // Roadside Zebra pair
                Vector3 zPos = c - fwd * 18f - right * 22f;
                BuildZebra(zoneGo, "TsavoZebra_Graze", zPos, zPos, isGrazer: true, phase: 0.7f, matZebraW, matZebraB);
            }
            else
            {
                // Narok / Maasai Mara: Striped Zebras crossing + Tall Spotted Giraffes & Gazelles grazing roadside!
                for (int i = 0; i < 3; i++)
                {
                    Vector3 a = c + fwd * (i * 6.5f - 6.5f) - right * 24f;
                    Vector3 b = c + fwd * (i * 6.5f - 6.5f) + right * 24f;
                    BuildZebra(zoneGo, $"NarokZebra_Cross_{i}", a, b, isGrazer: false, phase: 0.10f + i * 0.28f, matZebraW, matZebraB);
                }
                for (int i = 0; i < 2; i++)
                {
                    Vector3 gPos = c + fwd * (14f + i * 12f) + right * (22f + i * 6f);
                    BuildGiraffe(zoneGo, $"NarokGiraffe_{i}", gPos, gPos, isGrazer: true, phase: i * 1.4f, matGiraffeY, matGiraffeS);
                }
                for (int i = 0; i < 3; i++)
                {
                    Vector3 gzPos = c + fwd * (-15f + i * 9f) - right * (23f + (i % 2) * 5f);
                    BuildGazelle(zoneGo, $"NarokGazelle_{i}", gzPos, gzPos, isGrazer: true, phase: i * 0.9f, matGazelle, matZebraW, matZebraB);
                }
            }
        }

        private static void BuildZebra(Transform parent, string name, Vector3 a, Vector3 b, bool isGrazer, float phase, Material matW, Material matB)
        {
            a.y = KenyaNetworkData.GetElevationAt(a.x, a.z);
            b.y = KenyaNetworkData.GetElevationAt(b.x, b.z);
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = a;
            go.transform.rotation = Quaternion.Euler(0f, phase * 180f, 0f);
            var t = go.transform;

            var zebraPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/KenyaStreetSim/ImportedModels/KenyaRealismPack/Kenya_Zebra_Sculpted.glb");
            if (zebraPrefab != null)
            {
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(zebraPrefab);
                inst.name = "BlenderZebraMesh";
                inst.transform.SetParent(t, false);
                inst.transform.localPosition = Vector3.zero;
                var aiB = go.AddComponent<KenyaWildlifeAI>();
                aiB.startPoint = a; aiB.endPoint = b; aiB.isGrazerOnly = isGrazer; aiB.phaseOffset = phase; aiB.walkSpeed = 1.85f;
                return;
            }

            Box("Body", t, new Vector3(0f, 1.18f, 0f), new Vector3(0.62f, 0.66f, 1.72f), matW);
            for (int s = -3; s <= 3; s++)
            {
                Box($"Stripe_{s}", t, new Vector3(0f, 1.18f, s * 0.22f), new Vector3(0.65f, 0.68f, 0.08f), matB);
            }
            var neck = new GameObject("Neck").transform;
            neck.SetParent(t, false);
            neck.localPosition = new Vector3(0f, 1.40f, 0.72f);
            Box("NeckMesh", neck, new Vector3(0f, 0.36f, 0.18f), new Vector3(0.28f, 0.78f, 0.32f), matW, Quaternion.Euler(28f, 0f, 0f));
            Box("Mane",     neck, new Vector3(0f, 0.40f, 0.02f), new Vector3(0.08f, 0.72f, 0.14f), matB, Quaternion.Euler(28f, 0f, 0f));
            Box("Head",     neck, new Vector3(0f, 0.68f, 0.50f), new Vector3(0.26f, 0.28f, 0.58f), matW, Quaternion.Euler(14f, 0f, 0f));
            Box("Muzzle",   neck, new Vector3(0f, 0.63f, 0.76f), new Vector3(0.24f, 0.22f, 0.16f), matB);

            Transform[] legs = BuildFourLegs(t, 0.22f, 0.58f, 0.88f, 0.13f, matW);
            var ai = go.AddComponent<KenyaWildlifeAI>();
            ai.startPoint = a; ai.endPoint = b; ai.isGrazerOnly = isGrazer; ai.phaseOffset = phase; ai.walkSpeed = 1.85f; ai.legs = legs; ai.neckPivot = neck;
        }

        private static void BuildGiraffe(Transform parent, string name, Vector3 a, Vector3 b, bool isGrazer, float phase, Material matY, Material matS)
        {
            a.y = KenyaNetworkData.GetElevationAt(a.x, a.z);
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = a;
            go.transform.rotation = Quaternion.Euler(0f, phase * 140f, 0f);
            var t = go.transform;

            var giraffePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/KenyaStreetSim/ImportedModels/KenyaRealismPack/Kenya_Giraffe_Sculpted.glb");
            if (giraffePrefab != null)
            {
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(giraffePrefab);
                inst.name = "BlenderGiraffeMesh";
                inst.transform.SetParent(t, false);
                inst.transform.localPosition = Vector3.zero;
                var aiG = go.AddComponent<KenyaWildlifeAI>();
                aiG.startPoint = a; aiG.endPoint = b; aiG.isGrazerOnly = isGrazer; aiG.phaseOffset = phase; aiG.walkSpeed = 1.4f;
                return;
            }

            Box("Body", t, new Vector3(0f, 2.25f, 0f), new Vector3(0.82f, 0.92f, 1.95f), matY, Quaternion.Euler(-12f, 0f, 0f));
            for (int s = 0; s < 5; s++)
            {
                Box($"Spot_{s}", t, new Vector3(0f, 2.28f + (s % 2) * 0.16f, -0.65f + s * 0.34f), new Vector3(0.86f, 0.24f, 0.22f), matS);
            }
            var neck = new GameObject("Neck").transform;
            neck.SetParent(t, false);
            neck.localPosition = new Vector3(0f, 2.55f, 0.82f);
            Box("LongNeck", neck, new Vector3(0f, 1.35f, 0.28f), new Vector3(0.32f, 2.75f, 0.36f), matY, Quaternion.Euler(14f, 0f, 0f));
            for (int s = 0; s < 4; s++)
            {
                Box($"NeckSpot_{s}", neck, new Vector3(0f, 0.45f + s * 0.62f, 0.12f + s * 0.12f), new Vector3(0.35f, 0.22f, 0.22f), matS);
            }
            Box("Head", neck, new Vector3(0f, 2.72f, 0.72f), new Vector3(0.30f, 0.32f, 0.68f), matY);
            Box("HornL", neck, new Vector3(-0.08f, 2.98f, 0.56f), new Vector3(0.06f, 0.24f, 0.06f), matS);
            Box("HornR", neck, new Vector3( 0.08f, 2.98f, 0.56f), new Vector3(0.06f, 0.24f, 0.06f), matS);

            Transform[] legs = BuildFourLegs(t, 0.28f, 0.72f, 1.85f, 0.15f, matY);
            var ai = go.AddComponent<KenyaWildlifeAI>();
            ai.startPoint = a; ai.endPoint = b; ai.isGrazerOnly = isGrazer; ai.phaseOffset = phase; ai.walkSpeed = 1.4f; ai.legs = legs; ai.neckPivot = neck;
        }

        private static void BuildGazelle(Transform parent, string name, Vector3 a, Vector3 b, bool isGrazer, float phase, Material matTan, Material matW, Material matB)
        {
            a.y = KenyaNetworkData.GetElevationAt(a.x, a.z);
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = a;
            go.transform.rotation = Quaternion.Euler(0f, phase * 220f, 0f);
            var t = go.transform;

            Box("Body", t, new Vector3(0f, 0.85f, 0f), new Vector3(0.38f, 0.42f, 1.05f), matTan);
            Box("SideBand", t, new Vector3(0f, 0.78f, 0f), new Vector3(0.40f, 0.10f, 0.86f), matB);
            Box("Belly", t, new Vector3(0f, 0.66f, 0f), new Vector3(0.36f, 0.12f, 0.90f), matW);
            var neck = new GameObject("Neck").transform;
            neck.SetParent(t, false);
            neck.localPosition = new Vector3(0f, 0.98f, 0.44f);
            Box("NeckM", neck, new Vector3(0f, 0.26f, 0.10f), new Vector3(0.16f, 0.52f, 0.18f), matTan, Quaternion.Euler(18f, 0f, 0f));
            Box("Head",  neck, new Vector3(0f, 0.52f, 0.28f), new Vector3(0.16f, 0.18f, 0.34f), matTan);
            Box("HornL", neck, new Vector3(-0.05f, 0.72f, 0.20f), new Vector3(0.03f, 0.30f, 0.03f), matB, Quaternion.Euler(-20f, 0f, 0f));
            Box("HornR", neck, new Vector3( 0.05f, 0.72f, 0.20f), new Vector3(0.03f, 0.30f, 0.03f), matB, Quaternion.Euler(-20f, 0f, 0f));

            Transform[] legs = BuildFourLegs(t, 0.14f, 0.38f, 0.68f, 0.08f, matTan);
            var ai = go.AddComponent<KenyaWildlifeAI>();
            ai.startPoint = a; ai.endPoint = b; ai.isGrazerOnly = isGrazer; ai.phaseOffset = phase; ai.walkSpeed = 2.4f; ai.legs = legs; ai.neckPivot = neck;
        }

        private static void BuildElephant(Transform parent, string name, Vector3 a, Vector3 b, bool isGrazer, float phase, float scale, Material matEl, Material matIvory)
        {
            a.y = KenyaNetworkData.GetElevationAt(a.x, a.z);
            b.y = KenyaNetworkData.GetElevationAt(b.x, b.z);
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = a;
            go.transform.localScale = Vector3.one * scale;
            var t = go.transform;

            var elephantPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/KenyaStreetSim/ImportedModels/KenyaRealismPack/Kenya_Elephant_Sculpted.glb");
            if (elephantPrefab != null)
            {
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(elephantPrefab);
                inst.name = "BlenderElephantMesh";
                inst.transform.SetParent(t, false);
                inst.transform.localPosition = Vector3.zero;
                var aiE = go.AddComponent<KenyaWildlifeAI>();
                aiE.startPoint = a; aiE.endPoint = b; aiE.isGrazerOnly = isGrazer; aiE.phaseOffset = phase; aiE.walkSpeed = 1.35f;
                return;
            }

            Box("Body", t, new Vector3(0f, 2.05f, 0f), new Vector3(1.85f, 1.75f, 3.25f), matEl);
            var headPivot = new GameObject("HeadPivot").transform;
            headPivot.SetParent(t, false);
            headPivot.localPosition = new Vector3(0f, 2.25f, 1.55f);
            Box("Head",  headPivot, new Vector3(0f, 0.12f, 0.45f), new Vector3(1.38f, 1.32f, 1.25f), matEl);
            Box("EarL",  headPivot, new Vector3(-1.05f, 0.15f, 0.18f), new Vector3(0.95f, 1.35f, 0.16f), matEl, Quaternion.Euler(0f, 24f, 0f));
            Box("EarR",  headPivot, new Vector3( 1.05f, 0.15f, 0.18f), new Vector3(0.95f, 1.35f, 0.16f), matEl, Quaternion.Euler(0f, -24f, 0f));
            Box("Trunk", headPivot, new Vector3(0f, -0.78f, 1.15f), new Vector3(0.36f, 1.65f, 0.36f), matEl, Quaternion.Euler(14f, 0f, 0f));
            Cyl("TuskL", headPivot, new Vector3(-0.36f, -0.35f, 1.32f), new Vector3(0.14f, 0.68f, 0.14f), matIvory, Quaternion.Euler(68f, -12f, 0f));
            Cyl("TuskR", headPivot, new Vector3( 0.36f, -0.35f, 1.32f), new Vector3(0.14f, 0.68f, 0.14f), matIvory, Quaternion.Euler(68f,  12f, 0f));

            Transform[] legs = BuildFourLegs(t, 0.62f, 1.12f, 1.35f, 0.46f, matEl);
            var ai = go.AddComponent<KenyaWildlifeAI>();
            ai.startPoint = a; ai.endPoint = b; ai.isGrazerOnly = isGrazer; ai.phaseOffset = phase; ai.walkSpeed = 1.35f; ai.legs = legs; ai.neckPivot = headPivot;
        }

        private static Transform[] BuildFourLegs(Transform parent, float xOff, float zOff, float len, float thick, Material mat)
        {
            var legs = new Transform[4];
            Vector3[] offs =
            {
                new Vector3(-xOff, len,  zOff),
                new Vector3( xOff, len,  zOff),
                new Vector3(-xOff, len, -zOff),
                new Vector3( xOff, len, -zOff),
            };
            for (int i = 0; i < 4; i++)
            {
                var p = new GameObject($"Leg_{i}").transform;
                p.SetParent(parent, false);
                p.localPosition = offs[i];
                Box("LegMesh", p, new Vector3(0f, -len * 0.5f, 0f), new Vector3(thick, len, thick), mat);
                legs[i] = p;
            }
            return legs;
        }

        // =========================================================================
        // 5. CLIMBING-LANE ESCARPMENT SIGNS!
        // =========================================================================
        private static void BuildClimbingLaneSigns(Transform parent, Material matBlue, Material matYellow, Material matSteel)
        {
            var signRoot = new GameObject("Kenya_Climbing_Lane_Signs").transform;
            signRoot.SetParent(parent, false);

            (int hIdx, float prog, string title)[] signs =
            {
                ( 7, 0.08f, "MAI MAHIU ESCARPMENT\nCLIMBING LANE AHEAD\nTRUCKS KEEP LEFT"),
                (12, 0.72f, "SALGAA – TOTAL CLIMB\n3-LANE CLIMBING LANE\nHEAVY VEHICLES LEFT"),
                (13, 0.14f, "TIMBOROA ESCARPMENT\nSTEEP SLOPE — CLIMBING\nLANE AHEAD"),
                (10, 0.28f, "KAPLONG – KERICHO\nTEA HIGHLANDS CLIMB\nKEEP LEFT"),
                ( 4, 0.36f, "SONDU ESCARPMENT\nSTEEP DESCENT / CLIMB\nUSE LOW GEAR"),
            };

            for (int i = 0; i < signs.Length; i++)
            {
                var s = signs[i];
                var hDef = KenyaNetworkData.Highways[s.hIdx];
                var samples = KenyaNetworkData.SampleHighway(hDef, 6f);
                if (samples == null || samples.Count < 6) continue;
                int idx = Mathf.Clamp(Mathf.RoundToInt(s.prog * (samples.Count - 1)), 2, samples.Count - 3);
                Vector3 pt = samples[idx];
                Vector3 fwd = (samples[idx + 1] - samples[idx - 1]); fwd.y = 0f; fwd.Normalize();
                Vector3 right = Vector3.Cross(Vector3.up, fwd).normalized;

                float localHw = KenyaNetworkData.GetRoadHalfWidthAt(hDef, new Vector2(pt.x, pt.z));
                Vector3 pos = pt - right * (localHw + 4.8f);
                bool signClear = false;
                for (int push = 0; push < 6; push++)
                {
                    Vector3 cand = pt - right * (localHw + 4.8f + push * 2.5f);
                    float yaw = Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg;
                    if (KenyaNetworkData.IsClearOfAllRoundabouts(new Vector2(cand.x, cand.z), 36f) &&
                        KenyaNetworkData.IsFootprintClearOfAllRoads(cand, 2.0f, 0.8f, yaw, 2.8f))
                    {
                        pos = cand;
                        signClear = true;
                        break;
                    }
                }
                if (!signClear) continue;
                pos.y = KenyaNetworkData.GetElevationAt(pos.x, pos.z);
                var go = new GameObject($"ClimbingSign_{i}").transform;
                go.SetParent(signRoot, false);
                go.SetPositionAndRotation(pos, Quaternion.LookRotation(fwd, Vector3.up));

                Box("PostL", go, new Vector3(-1.3f, 1.85f, 0f), new Vector3(0.12f, 3.7f, 0.12f), matSteel);
                Box("PostR", go, new Vector3( 1.3f, 1.85f, 0f), new Vector3(0.12f, 3.7f, 0.12f), matSteel);
                Box("Board", go, new Vector3( 0.0f, 3.05f, 0f), new Vector3(3.4f, 1.75f, 0.12f), matBlue);
                Box("Trim",  go, new Vector3( 0.0f, 3.96f, 0f), new Vector3(3.45f, 0.14f, 0.14f), matYellow);
                WorldSign("FrontTxt", go, s.title, new Vector3(0f, 3.05f, -0.09f), new Vector2(3.2f, 1.6f), 2.5f, Color.white, Quaternion.identity);
                WorldSign("BackTxt",  go, s.title, new Vector3(0f, 3.05f,  0.09f), new Vector2(3.2f, 1.6f), 2.5f, Color.white, Quaternion.Euler(0f, 180f, 0f));
            }
        }

        // =========================================================================
        // 6. ETS2-GRADE GALVANIZED W-BEAM STEEL ESCARPMENT HIGHWAY GUARDRAILS!
        // =========================================================================
        private static void BuildEscarpmentGuardrails(Transform parent)
        {
            var guardrailPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/KenyaStreetSim/ImportedModels/KenyaWorldKit/Kenya_Highway_Guardrail.glb");
            if (guardrailPrefab == null) return;

            var grRoot = new GameObject("Kenya_Escarpment_WBeam_Guardrails").transform;
            grRoot.SetParent(parent, false);

            // Escarpment & highland highway stretches: B3 Mai Mahiu/Narok (7), B1 Kericho (10), A104 Salgaa-Total (12), A104 Timboroa-Eldoret (13), A1 Kisii-Sondu (4)
            (int hIdx, float minProg, float maxProg)[] stretches =
            {
                ( 7, 0.06f, 0.34f), // Mai Mahiu Rift Valley Escarpment!
                (12, 0.45f, 0.94f), // Nakuru -> Salgaa -> Total Climbing Corridor!
                (13, 0.06f, 0.42f), // Total -> Timboroa 2,700m Forest Climb!
                (10, 0.18f, 0.85f), // Kaplong -> Kericho -> Total Tea Escarpment!
                ( 4, 0.10f, 0.42f), // Kisii -> Oyugis -> Sondu Highlands!
            };

            for (int s = 0; s < stretches.Length; s++)
            {
                var st = stretches[s];
                if (st.hIdx < 0 || st.hIdx >= KenyaNetworkData.Highways.Length) continue;
                var hDef = KenyaNetworkData.Highways[st.hIdx];
                var samples = KenyaNetworkData.SampleHighway(hDef, 12.2f);
                if (samples == null || samples.Count < 6) continue;

                int iStart = Mathf.Clamp(Mathf.RoundToInt(st.minProg * (samples.Count - 1)), 2, samples.Count - 3);
                int iEnd = Mathf.Clamp(Mathf.RoundToInt(st.maxProg * (samples.Count - 1)), iStart + 1, samples.Count - 2);

                for (int i = iStart; i <= iEnd; i++)
                {
                    Vector3 pt = samples[i];
                    Vector2 ptXZ = new Vector2(pt.x, pt.z);
                    if (!KenyaNetworkData.IsClearOfAllRoundabouts(ptXZ, 44f)) continue;
                    bool nearTown = false;
                    for (int h = 0; h < KenyaNetworkData.Hubs.Length; h++)
                    {
                        if (Vector2.Distance(ptXZ, KenyaNetworkData.Hubs[h].xz) < 52f) { nearTown = true; break; }
                    }
                    if (nearTown) continue;

                    Vector3 fwd = samples[i + 1] - samples[i - 1];
                    if (fwd.sqrMagnitude < 0.01f) continue;
                    Vector3 fwdNorm = fwd.normalized;
                    Vector3 flatFwd = new Vector3(fwd.x, 0f, fwd.z).normalized;
                    Vector3 right = Vector3.Cross(Vector3.up, flatFwd).normalized;
                    float localHw = KenyaNetworkData.GetRoadHalfWidthAt(hDef, ptXZ);

                    for (int side = -1; side <= 1; side += 2)
                    {
                        Vector3 gp = pt + right * (side * (localHw + 1.45f));
                        if (KenyaNetworkData.DistanceToAnyTarmacEdge(new Vector2(gp.x, gp.z)) < 0.85f) continue;
                        gp.y = KenyaNetworkData.GetElevationAt(gp.x, gp.z) - 0.04f;

                        Quaternion rot = Quaternion.LookRotation(fwdNorm, Vector3.up);
                        if (side > 0) rot *= Quaternion.Euler(0f, 180f, 0f);

                        var inst = (GameObject)PrefabUtility.InstantiatePrefab(guardrailPrefab);
                        inst.name = $"Guardrail_{st.hIdx}_{i}_{side}";
                        inst.transform.SetParent(grRoot, false);
                        inst.transform.SetPositionAndRotation(gp, rot);
                    }
                }
            }
        }

        // =========================================================================
        // 7. ICONIC KENYAN BODA-BODA STAGES AT 16 MAJOR TOWNS & ROADSIDE MARKETS!
        // =========================================================================
        private static void BuildKenyanBodaBodaStages(Transform parent)
        {
            var bodaPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/KenyaStreetSim/ImportedModels/KenyaRealismPack/Kenya_BodaBoda_Rider.glb");
            if (bodaPrefab == null) return;

            var bodaRoot = new GameObject("Kenya_BodaBoda_Stages").transform;
            bodaRoot.SetParent(parent, false);

            int[] townHubs = { 0, 3, 4, 9, 11, 12, 13, 17, 19, 20, 22, 23, 25, 29, 32, 34, 35, 38 };
            for (int t = 0; t < townHubs.Length; t++)
            {
                int hubIdx = townHubs[t];
                if (hubIdx < 0 || hubIdx >= KenyaNetworkData.Hubs.Length) continue;
                var hub = KenyaNetworkData.Hubs[hubIdx];
                if (!KenyaNetworkData.FindRoundaboutClearStageSite(hub.xz, hub.roadYawDeg, 36f, out Vector3 rPt, out Vector3 rFwd, out _)) continue;

                Vector3 right = Vector3.Cross(Vector3.up, rFwd).normalized;
                float side = (t % 2 == 0) ? 1f : -1f;
                float lat = KenyaNetworkData.IsInsideMajorCityCorridor(new Vector2(rPt.x, rPt.z)) ? 13.8f : 10.6f;

                for (int b = 0; b < 3; b++)
                {
                    Vector3 bp = rPt + rFwd * (-3.2f + b * 2.4f) + right * (side * lat);
                    Vector2 bpXZ = new Vector2(bp.x, bp.z);
                    if (KenyaNetworkData.DistanceToAnyTarmacEdge(bpXZ) < 1.6f ||
                        !KenyaNetworkData.IsClearOfAllRoundabouts(bpXZ, 24f)) continue;

                    bp.y = KenyaNetworkData.GetElevationAt(bp.x, bp.z);
                    float yaw = Mathf.Atan2(rFwd.x, rFwd.z) * Mathf.Rad2Deg + (side > 0 ? -55f : 55f) + (b - 1) * 14f;
                    var inst = (GameObject)PrefabUtility.InstantiatePrefab(bodaPrefab);
                    inst.name = $"BodaBoda_{hub.name}_{b}";
                    inst.transform.SetParent(bodaRoot, false);
                    inst.transform.SetPositionAndRotation(bp, Quaternion.Euler(0f, yaw, 0f));
                }
            }
        }

        // =========================================================================
        // 8. KENHA STONE & STEEL HIGHWAY RIVER BRIDGES (5 ICONIC KENYAN RIVERS!)
        // =========================================================================
        private static void BuildKenhaRiverBridges(Transform parent)
        {
            var bridgePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/KenyaStreetSim/ImportedModels/KenyaRealismPack/Kenya_Stone_Bridge_Culvert.glb");
            if (bridgePrefab == null) return;

            var brRoot = new GameObject("Kenya_KeNHA_River_Bridges").transform;
            brRoot.SetParent(parent, false);

            // 5 Iconic Kenyan River Crossings cleanly clear of roundabouts & towns:
            // Sondu Miriu River (H4), Narok/Mara River (H7), Nzoia River near Webuye (H13), Tsavo/Athi River (H14), Kilifi Creek (H14)
            (int hIdx, float prog, string name)[] bridges =
            {
                ( 4, 0.44f, "SonduMiriu_Bridge"),
                ( 7, 0.50f, "NarokRiver_Bridge"),
                (13, 0.72f, "NzoiaRiver_Bridge"),
                (14, 0.45f, "TsavoRiver_Bridge"),
                (14, 0.86f, "KilifiCreek_Bridge"),
            };

            for (int i = 0; i < bridges.Length; i++)
            {
                var br = bridges[i];
                if (br.hIdx < 0 || br.hIdx >= KenyaNetworkData.Highways.Length) continue;
                var hDef = KenyaNetworkData.Highways[br.hIdx];
                var samples = KenyaNetworkData.SampleHighway(hDef, 8f);
                if (samples == null || samples.Count < 8) continue;

                int idx = Mathf.Clamp(Mathf.RoundToInt(br.prog * (samples.Count - 1)), 3, samples.Count - 4);
                Vector3 pt = samples[idx];
                Vector2 ptXZ = new Vector2(pt.x, pt.z);
                if (!KenyaNetworkData.IsClearOfAllRoundabouts(ptXZ, 55f)) continue;

                Vector3 fwd = samples[idx + 1] - samples[idx - 1];
                if (fwd.sqrMagnitude < 0.01f) continue;
                fwd.Normalize();
                Vector3 flatFwd = new Vector3(fwd.x, 0f, fwd.z).normalized;
                Vector3 right = Vector3.Cross(Vector3.up, flatFwd).normalized;

                float localHw = KenyaNetworkData.GetRoadHalfWidthAt(hDef, ptXZ);
                // Scale width slightly if highway is wider than standard 2-lane so parapets NEVER touch tarmac!
                float widthScale = Mathf.Max(1.0f, (localHw + 1.6f) / 9.2f);
                Vector2 pL = new Vector2((pt - right * (9.4f * widthScale)).x, (pt - right * (9.4f * widthScale)).z);
                Vector2 pR = new Vector2((pt + right * (9.4f * widthScale)).x, (pt + right * (9.4f * widthScale)).z);
                if (KenyaNetworkData.DistanceToAnyTarmacEdge(pL) < 0.8f || KenyaNetworkData.DistanceToAnyTarmacEdge(pR) < 0.8f) continue;

                pt.y = KenyaNetworkData.GetElevationAt(pt.x, pt.z);
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(bridgePrefab);
                inst.name = br.name;
                inst.transform.SetParent(brRoot, false);
                inst.transform.SetPositionAndRotation(pt, Quaternion.LookRotation(fwd, Vector3.up));
                inst.transform.localScale = new Vector3(widthScale, 1f, 1f);
            }
        }

        // =========================================================================
        // 9. RIFT VALLEY VOLCANIC ROCKY CLIFFS & TSAVO KOPJE BOULDERS!
        // =========================================================================
        private static void BuildRiftValleyRockyCliffs(Transform parent)
        {
            var rockPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/KenyaStreetSim/ImportedModels/KenyaRealismPack/Kenya_Rift_Boulder_Cliff.glb");
            if (rockPrefab == null) return;

            var rockRoot = new GameObject("Kenya_Rift_Volcanic_Cliffs").transform;
            rockRoot.SetParent(parent, false);
            var rng = new System.Random(254888);

            // Dramatic Kenyan escarpment rocky walls: Mai Mahiu (H7), Salgaa-Total (H12), Timboroa (H13), Tsavo Red Kopjes (H14), Kisii Hills (H4)
            int[] rockyHighways = { 7, 12, 13, 14, 4 };
            for (int r = 0; r < rockyHighways.Length; r++)
            {
                int hIdx = rockyHighways[r];
                var samples = KenyaNetworkData.SampleHighway(KenyaNetworkData.Highways[hIdx], 56f);
                if (samples == null) continue;
                for (int i = 2; i < samples.Count - 2; i++)
                {
                    if (rng.NextDouble() < 0.55) continue;
                    Vector3 pt = samples[i];
                    Vector3 fwd = samples[i + 1] - samples[i - 1]; fwd.y = 0f;
                    if (fwd.sqrMagnitude < 0.01f) continue;
                    fwd.Normalize();
                    Vector3 right = Vector3.Cross(Vector3.up, fwd).normalized;

                    float side = rng.NextDouble() < 0.5 ? -1f : 1f;
                    float lat = 26f + (float)rng.NextDouble() * 34f;
                    Vector3 rp = pt + right * (side * lat);
                    Vector2 rpXZ = new Vector2(rp.x, rp.z);
                    if (KenyaNetworkData.DistanceToAnyTarmacEdge(rpXZ) < 15f ||
                        !KenyaNetworkData.IsClearOfAllRoundabouts(rpXZ, 48f)) continue;

                    bool nearTown = false;
                    for (int h = 0; h < KenyaNetworkData.Hubs.Length; h++)
                        if (Vector2.Distance(rpXZ, KenyaNetworkData.Hubs[h].xz) < 58f) { nearTown = true; break; }
                    if (nearTown) continue;

                    rp.y = KenyaNetworkData.GetElevationAt(rp.x, rp.z) - 0.35f;
                    float scl = 1.15f + (float)rng.NextDouble() * 1.45f;
                    var inst = (GameObject)PrefabUtility.InstantiatePrefab(rockPrefab);
                    inst.name = $"RiftCliff_{hIdx}_{i}";
                    inst.transform.SetParent(rockRoot, false);
                    inst.transform.SetPositionAndRotation(rp, Quaternion.Euler(0f, (float)(rng.NextDouble() * 360.0), 0f));
                    inst.transform.localScale = new Vector3(scl, scl * 0.85f, scl);
                }
            }
        }

        private static Vector2 FindClearSiteNear(Vector2 preferred, float halfW, float halfD, float yawDeg)
        {
            if (KenyaNetworkData.IsFootprintClearOfAllRoads(new Vector3(preferred.x, 0f, preferred.y), halfW, halfD, yawDeg, 8f))
                return preferred;

            for (float rad = 25f; rad <= 120f; rad += 25f)
            {
                for (int a = 0; a < 8; a++)
                {
                    float ang = a * 45f * Mathf.Deg2Rad;
                    Vector2 cand = preferred + new Vector2(Mathf.Cos(ang) * rad, Mathf.Sin(ang) * rad);
                    if (KenyaNetworkData.IsFootprintClearOfAllRoads(new Vector3(cand.x, 0f, cand.y), halfW, halfD, yawDeg, 8f))
                        return cand;
                }
            }
            return preferred;
        }

        private static GameObject Box(string name, Transform parent, Vector3 localPos, Vector3 scale, Material mat, Quaternion? localRot = null)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot ?? Quaternion.identity;
            go.transform.localScale = scale;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        private static GameObject Cyl(string name, Transform parent, Vector3 localPos, Vector3 scale, Material mat, Quaternion localRot)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot;
            go.transform.localScale = scale;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        private static void WorldSign(string name, Transform parent, string text, Vector3 localPos, Vector2 size, float fontSize, Color col, Quaternion rot)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = rot;
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = col;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.rectTransform.sizeDelta = size;
        }

        private static Material MakeMat(string matName, Shader shader, Color baseCol, float smooth, float metal, Color? emission = null, Texture2D normalMap = null, Vector2? tiling = null)
        {
            string path = $"Assets/KenyaStreetSim/Materials/{matName}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = shader;
            m.SetColor("_BaseColor", baseCol);
            m.SetFloat("_Smoothness", smooth);
            m.SetFloat("_Metallic", metal);
            if (normalMap != null)
            {
                m.EnableKeyword("_NORMALMAP");
                m.SetTexture("_BumpMap", normalMap);
                m.SetFloat("_BumpScale", 1.25f);
                m.SetTextureScale("_BumpMap", tiling ?? Vector2.one);
            }
            else
            {
                m.DisableKeyword("_NORMALMAP");
                m.SetTexture("_BumpMap", null);
            }
            if (emission.HasValue && emission.Value.maxColorComponent > 0.01f)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emission.Value);
            }
            else
            {
                m.DisableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", Color.black);
            }
            EditorUtility.SetDirty(m);
            return m;
        }
    }
}
