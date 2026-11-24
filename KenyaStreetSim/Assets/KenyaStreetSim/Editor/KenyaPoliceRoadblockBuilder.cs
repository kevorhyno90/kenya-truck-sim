using TMPro;
using UnityEditor;
using UnityEngine;

namespace KenyaStreetSim.Editor
{
    /// <summary>
    /// Builds 4 Authentic Kenyan Police Highway Roadblocks across major corridors:
    /// - Movable Red/White Striped Boom Barrier (NO blocking collider so AI never jams!)
    /// - High-Visibility Orange/White Cones + Blue/White "SLOW DOWN - KENYA POLICE CHECK" Signboard
    /// - Pulsing Blue/Red Tripod Beacon + Parked Kenya Police Patrol Truck cleanly off on the left shoulder
    /// - 2 Uniformed 3D Kenyan Police Officers (Royal Navy uniform + neon lime reflective safety vest)
    ///   wired to KenyaPoliceRoadblock so Officer #1 walks ALL THE WAY AROUND your vehicle & waves you through!
    /// </summary>
    public static class KenyaPoliceRoadblockBuilder
    {
        public static int BuildAllRoadblocks(Transform worldParent)
        {
            var urpLit = Shader.Find("Universal Render Pipeline/Lit");
            var root = new GameObject("Kenya_Police_Highway_Roadblocks").transform;
            root.SetParent(worldParent, false);

            Material matRed     = MakeMat("RB_Red", urpLit, new Color(0.88f, 0.14f, 0.12f), 0.55f, 0.1f);
            Material matWhite   = MakeMat("RB_White", urpLit, new Color(0.95f, 0.96f, 0.98f), 0.62f, 0.05f);
            Material matYellow  = MakeMat("RB_Yellow", urpLit, new Color(0.96f, 0.78f, 0.10f), 0.45f, 0.1f);
            Material matOrange  = MakeMat("RB_Orange", urpLit, new Color(0.98f, 0.42f, 0.06f), 0.45f, 0.05f);
            Material matSteel   = MakeMat("RB_Steel", urpLit, new Color(0.22f, 0.25f, 0.30f), 0.65f, 0.72f);
            Material matSignBlu = MakeMat("RB_PoliceBlue", urpLit, new Color(0.08f, 0.24f, 0.62f), 0.45f, 0.1f);
            Material matNavy    = MakeMat("RB_OfficerNavy", urpLit, new Color(0.07f, 0.13f, 0.32f), 0.28f, 0.05f);
            Material matVest    = MakeMat("RB_OfficerVest", urpLit, new Color(0.68f, 0.98f, 0.12f), 0.35f, 0.0f, new Color(0.35f, 0.55f, 0.06f));
            Material matSkin    = MakeMat("RB_OfficerSkin", urpLit, new Color(0.32f, 0.19f, 0.12f), 0.25f, 0.0f);
            Material matBoot    = MakeMat("RB_OfficerBoot", urpLit, new Color(0.09f, 0.09f, 0.10f), 0.30f, 0.1f);
            Material matBeacon  = MakeMat("RB_BeaconGlow", urpLit, new Color(0.25f, 0.62f, 1.0f), 0.85f, 0.0f, new Color(0.15f, 0.55f, 1f) * 3.5f);
            Material matGlass   = MakeMat("RB_PatrolGlass", urpLit, new Color(0.12f, 0.18f, 0.24f), 0.90f, 0.25f);

            // ONLY THE 5 MAJOR KENYAN HIGHWAY POLICE ROADBLOCKS — STRICTLY ON OPEN HIGHWAY STRETCHES
            // (>= 95m clear of EVERY roundabout & >= 60m clear of town centers — NEVER on roundabouts!):
            // 1. Mai Mahiu – Narok Open Plains (B3), 2. Salgaa Mid-Climb (A104), 3. Sondu – Katito Plains (A1),
            // 4. Emali – Mtito Andei Open Highway (A109), 5. Eldoret – Turbo Open Highway (A104)!
            (int highwayIdx, float progress, string title)[] placements =
            {
                ( 7, 0.28f, "MAI MAHIU – NAROK B3 MAJOR POLICE ROADBLOCK"),
                (12, 0.84f, "SALGAA – TOTAL A104 MAJOR POLICE ROADBLOCK"),
                ( 4, 0.56f, "KATITO – AHERO A1 MAJOR POLICE ROADBLOCK"),
                (14, 0.17f, "EMALI – MTITO ANDEI A109 MAJOR POLICE ROADBLOCK"),
                (13, 0.55f, "ELDORET – TURBO A104 MAJOR POLICE ROADBLOCK")
            };

            int built = 0;
            for (int i = 0; i < placements.Length; i++)
            {
                var p = placements[i];
                if (p.highwayIdx < 0 || p.highwayIdx >= KenyaNetworkData.Highways.Length) continue;
                var hDef = KenyaNetworkData.Highways[p.highwayIdx];
                var samples = KenyaNetworkData.SampleHighway(hDef, 6f);
                if (samples == null || samples.Count < 8) continue;

                // Search cleanly outward from target progress until strictly >= 95m from ALL roundabouts & >= 55m from towns!
                int baseIdx = Mathf.Clamp(Mathf.RoundToInt(p.progress * (samples.Count - 1)), 3, samples.Count - 4);
                int idx = -1;
                for (int delta = 0; delta < samples.Count; delta++)
                {
                    int[] tries = delta == 0 ? new[] { baseIdx } : new[] { baseIdx + delta, baseIdx - delta };
                    for (int t = 0; t < tries.Length; t++)
                    {
                        int candIdx = tries[t];
                        if (candIdx < 4 || candIdx >= samples.Count - 4) continue;
                        Vector2 candXZ = new Vector2(samples[candIdx].x, samples[candIdx].z);
                        if (!KenyaNetworkData.IsClearOfAllRoundabouts(candXZ, 95f)) continue;
                        bool nearTown = false;
                        for (int h = 0; h < KenyaNetworkData.Hubs.Length; h++)
                        {
                            if (Vector2.Distance(candXZ, KenyaNetworkData.Hubs[h].xz) < 55f) { nearTown = true; break; }
                        }
                        if (!nearTown) { idx = candIdx; break; }
                    }
                    if (idx >= 0) break;
                }
                if (idx < 0) continue;

                Vector3 centerPt = samples[idx];
                centerPt.y = KenyaNetworkData.GetElevationAt(centerPt.x, centerPt.z);
                Vector3 fwd = (samples[idx + 1] - samples[idx - 1]);
                if (fwd.sqrMagnitude < 0.01f) continue;
                fwd.Normalize();

                float hw = KenyaNetworkData.GetRoadHalfWidthAt(hDef, new Vector2(centerPt.x, centerPt.z));
                float extraShift = Mathf.Max(0f, hw - 7.0f);

                Quaternion rot = Quaternion.LookRotation(fwd, Vector3.up);
                var cpGo = new GameObject($"PoliceRoadblock_{i}_{p.highwayIdx}");
                cpGo.transform.SetParent(root, false);
                cpGo.transform.SetPositionAndRotation(centerPt, rot);

                var ctrl = cpGo.AddComponent<KenyaPoliceRoadblock>();
                ctrl.checkpointName = p.title;

                // Place pedestal, signboard, parked patrol truck & cones shifted cleanly to this highway's true left shoulder!
                float pedX = -6.45f - extraShift;
                Box("BarrierPedestal", cpGo.transform, new Vector3(pedX, 0.58f, 0f), new Vector3(0.56f, 1.16f, 0.56f), matYellow);
                Box("PedestalCap",     cpGo.transform, new Vector3(pedX, 1.20f, 0f), new Vector3(0.62f, 0.10f, 0.62f), matSteel);

                // Flashing Blue/Red Police Beacon on top of Pedestal
                var beaconObj = Cyl("CheckpointBeacon", cpGo.transform, new Vector3(pedX, 1.38f, 0f), new Vector3(0.24f, 0.14f, 0.24f), matBeacon, Quaternion.identity);
                ctrl.beaconLightRenderer = beaconObj.GetComponent<Renderer>();

                // Animated Red/White Striped Boom Barrier Pole spanning across the left inspection lane (raises up to -76° when cleared!)
                var boomPivot = new GameObject("BoomBarrierPivot").transform;
                boomPivot.SetParent(cpGo.transform, false);
                boomPivot.localPosition = new Vector3(pedX + 0.25f, 1.02f, 0f);
                int boomSegs = Mathf.Clamp(Mathf.RoundToInt((hw - 0.6f) / 0.85f), 6, 10);
                for (int seg = 0; seg < boomSegs; seg++)
                {
                    float xSeg = 0.42f + seg * 0.85f;
                    Box($"BoomStripe_{seg}", boomPivot, new Vector3(xSeg, 0f, 0f), new Vector3(0.84f, 0.12f, 0.10f), (seg % 2 == 0) ? matRed : matWhite);
                }
                ctrl.boomBarrierPivot = boomPivot;

                // Tall Reflective "STOP - KENYA POLICE CHECK" Signboard 10m ahead — cleanly on grass shoulder (NEVER overhanging tarmac!)
                float signX = pedX - 2.4f;
                Box("SignPost", cpGo.transform, new Vector3(signX, 1.25f, -10.5f), new Vector3(0.10f, 2.50f, 0.10f), matSteel);
                Box("SignBoard", cpGo.transform, new Vector3(signX, 2.15f, -10.5f), new Vector3(2.25f, 0.92f, 0.08f), matSignBlu);
                WorldSign("SignFrontTxt", cpGo.transform, "STOP — KENYA POLICE\nMAJOR ROADBLOCK", new Vector3(signX, 2.15f, -10.56f), new Vector2(2.15f, 0.84f), 2.1f, Color.white, Quaternion.identity);
                WorldSign("SignRearTxt",  cpGo.transform, "KENYA POLICE CHECK\nSAFARI NJEMA", new Vector3(signX, 2.15f, -10.44f), new Vector2(2.15f, 0.84f), 2.0f, Color.white, Quaternion.Euler(0f, 180f, 0f));

                // 4 High-Visibility Orange/White Traffic Cones guiding cars calmly into the inspection lane
                Vector3[] conePos =
                {
                    new Vector3(pedX - 0.45f, 0f, -6.5f),
                    new Vector3(pedX - 0.45f, 0f, -3.2f),
                    new Vector3(-0.35f, 0f, -3.5f),
                    new Vector3(-0.35f, 0f,  2.8f)
                };
                for (int c = 0; c < conePos.Length; c++)
                {
                    BuildTrafficCone(cpGo.transform, $"PoliceCone_{c}", conePos[c], matOrange, matWhite, matBoot);
                }

                // Inspector Officer #1 standing beside the barrier — walks ALL THE WAY AROUND your vehicle!
                var off1 = BuildKenyaPoliceOfficer(cpGo.transform, "InspectorOfficer_WalkAround",
                    new Vector3(pedX + 0.45f, 0f, -2.4f), Quaternion.Euler(0f, 118f, 0f),
                    matNavy, matVest, matSkin, matBoot, matYellow,
                    out Transform off1ArmR, out Transform off1LegL, out Transform off1LegR);
                ctrl.inspectorOfficer = off1;
                ctrl.officerRightArm = off1ArmR;
                ctrl.officerLeftLeg = off1LegL;
                ctrl.officerRightLeg = off1LegR;

                // Partner Officer #2 standing guard beside the parked Police Patrol Truck on the far shoulder!
                BuildKenyaPoliceOfficer(cpGo.transform, "GuardOfficer_Post",
                    new Vector3(pedX - 2.0f, 0f, 2.2f), Quaternion.Euler(0f, 145f, 0f),
                    matNavy, matVest, matSkin, matBoot, matYellow,
                    out _, out _, out _);

                // Parked Kenya Police Patrol Truck cleanly off on the grass shoulder (clear of tarmac!)
                BuildParkedPolicePatrolTruck(cpGo.transform, new Vector3(pedX - 3.8f, 0f, 4.2f), Quaternion.Euler(0f, 14f, 0f),
                    matWhite, matSignBlu, matRed, matYellow, matBoot, matGlass, matSteel);

                built++;
            }

            return built;
        }

        private static Transform BuildKenyaPoliceOfficer(
            Transform parent, string name, Vector3 localPos, Quaternion localRot,
            Material matNavy, Material matVest, Material matSkin, Material matBoot, Material matGold,
            out Transform rightArmPivot, out Transform leftLegPivot, out Transform rightLegPivot)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.localPosition = localPos;
            root.localRotation = localRot;

            // Articulated Left & Right Legs (Royal Navy Police Trousers + Black Boots)
            leftLegPivot = new GameObject("LegL").transform;
            leftLegPivot.SetParent(root, false);
            leftLegPivot.localPosition = new Vector3(-0.11f, 0.86f, 0f);
            Box("TrouserL", leftLegPivot, new Vector3(0f, -0.38f, 0f), new Vector3(0.16f, 0.72f, 0.18f), matNavy);
            Box("BootL",    leftLegPivot, new Vector3(0f, -0.78f, 0.04f), new Vector3(0.17f, 0.16f, 0.28f), matBoot);

            rightLegPivot = new GameObject("LegR").transform;
            rightLegPivot.SetParent(root, false);
            rightLegPivot.localPosition = new Vector3(0.11f, 0.86f, 0f);
            Box("TrouserR", rightLegPivot, new Vector3(0f, -0.38f, 0f), new Vector3(0.16f, 0.72f, 0.18f), matNavy);
            Box("BootR",    rightLegPivot, new Vector3(0f, -0.78f, 0.04f), new Vector3(0.17f, 0.16f, 0.28f), matBoot);

            // Torso: Royal Navy Tunic + Bright Lime-Green Reflective Kenyan Traffic Police Vest!
            Box("TorsoNavy", root, new Vector3(0f, 1.18f, 0f), new Vector3(0.42f, 0.62f, 0.24f), matNavy);
            Box("ReflectiveVest", root, new Vector3(0f, 1.20f, 0f), new Vector3(0.45f, 0.50f, 0.26f), matVest);
            Box("Belt", root, new Vector3(0f, 0.90f, 0f), new Vector3(0.44f, 0.08f, 0.26f), matBoot);

            // Head + Peaked Kenyan Police Service Cap with Gold Badge!
            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Head";
            head.transform.SetParent(root, false);
            head.transform.localPosition = new Vector3(0f, 1.62f, 0f);
            head.transform.localScale = new Vector3(0.24f, 0.27f, 0.24f);
            Object.DestroyImmediate(head.GetComponent<Collider>());
            head.GetComponent<Renderer>().sharedMaterial = matSkin;

            Cyl("PoliceCapCrown", root, new Vector3(0f, 1.77f, 0f), new Vector3(0.28f, 0.06f, 0.28f), matNavy, Quaternion.identity);
            Box("PoliceCapPeak",  root, new Vector3(0f, 1.72f, 0.12f), new Vector3(0.22f, 0.03f, 0.14f), matBoot);
            Box("CapGoldBadge",   root, new Vector3(0f, 1.77f, 0.13f), new Vector3(0.06f, 0.06f, 0.03f), matGold);

            // Left Arm (relaxed) & Right Arm Pivot (waves drivers to slow down & proceed!)
            Box("ArmL", root, new Vector3(-0.28f, 1.16f, 0f), new Vector3(0.13f, 0.56f, 0.14f), matNavy);

            rightArmPivot = new GameObject("ArmR").transform;
            rightArmPivot.SetParent(root, false);
            rightArmPivot.localPosition = new Vector3(0.28f, 1.42f, 0f);
            Box("ArmRMesh", rightArmPivot, new Vector3(0f, -0.26f, 0f), new Vector3(0.13f, 0.56f, 0.14f), matNavy);
            Box("HandR",    rightArmPivot, new Vector3(0f, -0.56f, 0f), new Vector3(0.11f, 0.12f, 0.11f), matSkin);

            return root;
        }

        private static void BuildTrafficCone(Transform parent, string name, Vector3 localPos, Material matOrange, Material matWhite, Material matBase)
        {
            var g = new GameObject(name).transform;
            g.SetParent(parent, false);
            g.localPosition = localPos;
            Box("Base", g, new Vector3(0f, 0.04f, 0f), new Vector3(0.46f, 0.08f, 0.46f), matBase);
            Cyl("Lower", g, new Vector3(0f, 0.26f, 0f), new Vector3(0.32f, 0.20f, 0.32f), matOrange, Quaternion.identity);
            Cyl("Band",  g, new Vector3(0f, 0.48f, 0f), new Vector3(0.22f, 0.08f, 0.22f), matWhite, Quaternion.identity);
            Cyl("Top",   g, new Vector3(0f, 0.64f, 0f), new Vector3(0.13f, 0.10f, 0.13f), matOrange, Quaternion.identity);
        }

        private static void BuildParkedPolicePatrolTruck(
            Transform parent, Vector3 localPos, Quaternion localRot,
            Material matWhite, Material matBlue, Material matRed, Material matYellow, Material matTire, Material matGlass, Material matSteel)
        {
            var g = new GameObject("ParkedKenyaPolicePatrol").transform;
            g.SetParent(parent, false);
            g.localPosition = localPos;
            g.localRotation = localRot;

            // Classic white/blue/red/yellow Kenya Police patrol pickup parked off on the shoulder
            Box("LowerBody", g, new Vector3(0f, 0.85f, 0f), new Vector3(1.86f, 0.78f, 4.75f), matWhite);
            Box("BlueStripe", g, new Vector3(0f, 0.92f, 0f), new Vector3(1.89f, 0.20f, 4.72f), matBlue);
            Box("RedStripe",  g, new Vector3(0f, 0.78f, 0f), new Vector3(1.89f, 0.08f, 4.72f), matRed);
            Box("YelStripe",  g, new Vector3(0f, 0.70f, 0f), new Vector3(1.89f, 0.07f, 4.72f), matYellow);
            Box("CabUpper",   g, new Vector3(0f, 1.56f, 0.45f), new Vector3(1.72f, 0.68f, 1.75f), matWhite);
            Box("Windshield", g, new Vector3(0f, 1.56f, 1.32f), new Vector3(1.56f, 0.56f, 0.08f), matGlass);
            Box("CanopyRear", g, new Vector3(0f, 1.62f, -1.25f), new Vector3(1.76f, 0.80f, 2.05f), matSteel);
            Box("BarRed",     g, new Vector3(-0.36f, 2.02f, 0.65f), new Vector3(0.55f, 0.16f, 0.24f), matRed);
            Box("BarBlue",    g, new Vector3( 0.36f, 2.02f, 0.65f), new Vector3(0.55f, 0.16f, 0.24f), matBlue);

            float[] wz = { 1.48f, -1.42f };
            for (int a = 0; a < 2; a++)
            {
                for (int s = -1; s <= 1; s += 2)
                {
                    Cyl($"Wheel_{a}_{s}", g, new Vector3(s * 0.88f, 0.42f, wz[a]), new Vector3(0.84f, 0.16f, 0.84f), matTire, Quaternion.Euler(0f, 0f, 90f));
                }
            }
        }

        private static GameObject Box(string name, Transform parent, Vector3 localPos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
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

        private static Material MakeMat(string matName, Shader shader, Color baseCol, float smooth, float metal, Color? emission = null)
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
