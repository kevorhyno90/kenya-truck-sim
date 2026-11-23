using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace KenyaStreetSim.Editor
{
    /// <summary>
    /// Builds the finished, fully playable Kenya Police Land Cruiser 79 Series Patrol Truck (KCP 380D)
    /// faithfully matching the user's reference concept:
    /// - Deep Royal Navy Blue 79-Series single-cab pickup with Red & Yellow arched stripes
    /// - Gold/Red Kenya Police Service crest + "KENYA POLICE" side lettering + "POLICE" hood stencil
    /// - Olive-Khaki woven PBR canvas troop canopy with 2 clear side windows, rolled rear flap & buckle straps
    /// - Open rear troop bed with diamond-plate floor, tubular steel roll cage & 2 black vinyl troop benches
    /// - Left-side raised black Air Snorkel, heavy steel front bullbar, side steps, rear tow bumper & mudflaps
    /// - Working flashing Red/Blue roof lightbar + real URP PointLights + [G] Kenyan Police Siren
    /// - Detailed RHD [C] Cockpit Dashboard, 3-spoke steering wheel & BOTH live rear-view Side Mirrors!
    /// </summary>
    public static class KenyaPoliceLandCruiserBuilder
    {
        public static KenyaStreetVehicle BuildKenyaPoliceLandCruiser(
            Transform parent,
            Vector3 spawnPos,
            Material matTire,
            Material matHead,
            Material matTail,
            Material matIndicator,
            Material matPlate,
            Material matInterior,
            Material matBlack,
            Material matChrome,
            Material matClearGlass)
        {
            KenyaPoliceLC79MeshFactory.BuildAllMeshes();

            var root = new GameObject("KenyaPolice_LandCruiser79_KCP380D");
            root.transform.SetParent(parent, false);
            root.transform.position = spawnPos;

            var rb = root.AddComponent<Rigidbody>();
            rb.mass = 2950f;
#if UNITY_6000_0_OR_NEWER
            rb.linearDamping = 0.04f;
            rb.angularDamping = 0.40f;
#else
            rb.drag = 0.04f;
            rb.angularDrag = 0.40f;
#endif

            var ctrl = root.AddComponent<KenyaStreetVehicle>();
            ctrl.displayName = "Kenya Police LC79 (KCP 380D)";
            ctrl.plateNumber = "KCP 380D";
            ctrl.isCoach = false;
            ctrl.jobRole = KenyaStreetVehicle.VehicleJobRole.PolicePatrol;
            ctrl.motorTorque = 2450f;
            ctrl.reverseTorque = 1500f;
            ctrl.brakeTorque = 4600f;
            ctrl.handbrakeTorque = 6500f;
            ctrl.maxSteerAngle = 36f;
            ctrl.highSpeedSteerAngle = 14f;
            ctrl.maxSpeedKmh = 128f;
            ctrl.antiRollStiffness = 7800f;
            ctrl.centerOfMassOffset = new Vector3(0f, -0.42f, 0.12f);

            var chassisCol = root.AddComponent<BoxCollider>();
            chassisCol.center = new Vector3(0f, 1.32f, 0f);
            chassisCol.size = new Vector3(1.88f, 1.65f, 5.10f);

            // Materials matching the exact KCP 380D reference photos!
            var urpLit = Shader.Find("Universal Render Pipeline/Lit");
            var matNavy = LoadOrCreateMat("PoliceLC_NavyGloss", urpLit, Palette.Hex("#101E46"), 0.84f, 0.28f);
            var matStripeRed = LoadOrCreateMat("PoliceLC_StripeRed", urpLit, Palette.Hex("#DC2222"), 0.65f, 0.10f);
            var matStripeYellow = LoadOrCreateMat("PoliceLC_StripeYellow", urpLit, Palette.Hex("#F6C21A"), 0.65f, 0.10f);
            var matSteelBlack = LoadOrCreateMat("PoliceLC_SteelBlack", urpLit, Palette.Hex("#1A1D22"), 0.32f, 0.62f);
            var matVinylSeat = LoadOrCreateMat("PoliceLC_VinylSeat", urpLit, Palette.Hex("#181A1E"), 0.42f, 0.08f);
            var matLeatherStrap = LoadOrCreateMat("PoliceLC_LeatherStrap", urpLit, Palette.Hex("#2F2B20"), 0.28f, 0.05f);
            var matWheelSteel = LoadOrCreateMat("PoliceLC_OffroadRim", urpLit, Palette.Hex("#1D2026"), 0.48f, 0.72f);

            var texCanvasAlb = AssetDatabase.LoadAssetAtPath<Texture2D>($"{KenyaPbrTextureGenerator.TexFolder}/PoliceLC_Canvas_Albedo.png");
            var texCanvasNrm = AssetDatabase.LoadAssetAtPath<Texture2D>($"{KenyaPbrTextureGenerator.TexFolder}/PoliceLC_Canvas_Normal.png");
            var matCanvas = LoadOrCreateMat("PoliceLC_OliveCanvas", urpLit, Color.white, 0.14f, 0f, texCanvasAlb, texCanvasNrm, new Vector2(2f, 2f), 1.25f);

            var texDiamondAlb = AssetDatabase.LoadAssetAtPath<Texture2D>($"{KenyaPbrTextureGenerator.TexFolder}/DiamondPlate_Albedo.png");
            var texDiamondNrm = AssetDatabase.LoadAssetAtPath<Texture2D>($"{KenyaPbrTextureGenerator.TexFolder}/DiamondPlate_Normal.png");
            var matBedFloor = LoadOrCreateMat("PoliceLC_DiamondBed", urpLit, Color.white, 0.38f, 0.65f, texDiamondAlb, texDiamondNrm, new Vector2(4f, 5f), 1.4f);

            var texCrest = AssetDatabase.LoadAssetAtPath<Texture2D>($"{KenyaPbrTextureGenerator.TexFolder}/PoliceLC_Crest_Albedo.png");
            var matCrest = LoadOrCreateMat("PoliceLC_CrestDecal", urpLit, Color.white, 0.45f, 0.12f, texCrest, null, Vector2.one, 1f, alphaClip: true);

            var matBarRed = LoadOrCreateMat("PoliceLC_BarRed", urpLit, new Color(0.95f, 0.10f, 0.10f), 0.90f, 0.05f, emission: new Color(0.95f, 0.10f, 0.10f) * 2.2f);
            var matBarBlue = LoadOrCreateMat("PoliceLC_BarBlue", urpLit, new Color(0.10f, 0.42f, 1.0f), 0.90f, 0.05f, emission: new Color(0.10f, 0.42f, 1.0f) * 2.4f);

            var body = new GameObject("VisualBodyRoot").transform;
            body.SetParent(root.transform, false);

            // =========================================================================
            // 1. CHASSIS LADDER FRAME, SCULPTED TUBULAR STEEL BULLBAR & REAR BUMPER
            // =========================================================================
            Box("LadderFrame", body, new Vector3(0f, 0.54f, -0.05f), new Vector3(1.06f, 0.20f, 4.78f), matSteelBlack);
            Box("FrontBumperBeam", body, new Vector3(0f, 0.68f, 2.58f), new Vector3(1.94f, 0.24f, 0.22f), matSteelBlack);
            Box("FrontSkidPlate", body, new Vector3(0f, 0.48f, 2.42f), new Vector3(1.18f, 0.10f, 0.42f), matSteelBlack, Quaternion.Euler(22f, 0f, 0f));
            // Classic 79-Series Round Bent-Tube Wrap-Around Steel Bullbar!
            CustomMesh("SculptedTubularBullbar", body, Vector3.zero, Quaternion.identity, Vector3.one, KenyaPoliceLC79MeshFactory.TubularBullbarMesh, matSteelBlack);

            // Heavy matte-black rear steel bumper + center tow shackle + mudflaps
            Box("RearBumperL", body, new Vector3(-0.68f, 0.62f, -2.56f), new Vector3(0.60f, 0.24f, 0.26f), matSteelBlack);
            Box("RearBumperR", body, new Vector3( 0.68f, 0.62f, -2.56f), new Vector3(0.60f, 0.24f, 0.26f), matSteelBlack);
            Box("RearStepCenter", body, new Vector3(0f, 0.66f, -2.52f), new Vector3(0.76f, 0.12f, 0.24f), matSteelBlack);
            Box("TowHitchMount", body, new Vector3(0f, 0.48f, -2.58f), new Vector3(0.22f, 0.22f, 0.22f), matSteelBlack);
            Box("MudflapL", body, new Vector3(-0.88f, 0.42f, -2.06f), new Vector3(0.32f, 0.44f, 0.04f), matSteelBlack);
            Box("MudflapR", body, new Vector3( 0.88f, 0.42f, -2.06f), new Vector3(0.32f, 0.44f, 0.04f), matSteelBlack);

            // Side Running Boards / Heavy Foot Steps
            for (int side = -1; side <= 1; side += 2)
            {
                Box($"SideStep_{side}", body, new Vector3(side * 0.98f, 0.56f, 0.58f), new Vector3(0.24f, 0.07f, 1.48f), matSteelBlack);
            }

            // =========================================================================
            // 2. SCULPTED ROYAL NAVY 79-SERIES HOOD, CAB, FENDERS & OPEN PICKUP BED
            // =========================================================================
            // Smooth Contoured 79-Series Hood & Beveled Front Fenders with center power bulge!
            CustomMesh("SculptedHoodAndFenders", body, Vector3.zero, Quaternion.identity, Vector3.one, KenyaPoliceLC79MeshFactory.HoodNoseMesh, matNavy);
            WorldText("HoodPoliceTxt", body, "POLICE", new Vector3(0f, 1.46f, 1.98f), new Vector2(1.2f, 0.34f), 2.3f, Color.white, Quaternion.Euler(90f, 180f, 0f));

            // Iconic Toyota Land Cruiser 79 Slatted Front Grille + Chrome Badge Bar
            Box("GrilleRecess", body, new Vector3(0f, 1.12f, 2.49f), new Vector3(1.16f, 0.38f, 0.06f), matSteelBlack);
            Box("GrilleChromeBar", body, new Vector3(0f, 1.14f, 2.53f), new Vector3(1.08f, 0.05f, 0.04f), matChrome);
            Box("GrilleEmblem", body, new Vector3(0f, 1.14f, 2.55f), new Vector3(0.24f, 0.14f, 0.04f), matChrome);

            // Cab Lower Doors & Floor Pan + Roof + A/B Pillars (leaving clear interior for [C] Cockpit!)
            Box("CabFloorPan", body, new Vector3(0f, 0.78f, 0.56f), new Vector3(1.80f, 0.16f, 1.32f), matSteelBlack);
            Box("CabDoorLowerL", body, new Vector3(-0.87f, 1.12f, 0.56f), new Vector3(0.10f, 0.68f, 1.24f), matNavy);
            Box("CabDoorLowerR", body, new Vector3( 0.87f, 1.12f, 0.56f), new Vector3(0.10f, 0.68f, 1.24f), matNavy);
            Box("CabRearWall", body, new Vector3(0f, 1.38f, -0.06f), new Vector3(1.80f, 1.18f, 0.10f), matNavy);
            Box("CabRoof", body, new Vector3(0f, 2.08f, 0.44f), new Vector3(1.76f, 0.10f, 1.28f), matNavy);
            // A-Pillars & B-Pillars & Clear Windshield + Side Windows
            Box("CabAPillarL", body, new Vector3(-0.82f, 1.74f, 1.06f), new Vector3(0.10f, 0.70f, 0.10f), matNavy, Quaternion.Euler(-16f, 0f, 0f));
            Box("CabAPillarR", body, new Vector3( 0.82f, 1.74f, 1.06f), new Vector3(0.10f, 0.70f, 0.10f), matNavy, Quaternion.Euler(-16f, 0f, 0f));
            Box("CabBPillarL", body, new Vector3(-0.84f, 1.74f, -0.04f), new Vector3(0.10f, 0.70f, 0.12f), matNavy);
            Box("CabBPillarR", body, new Vector3( 0.84f, 1.74f, -0.04f), new Vector3(0.10f, 0.70f, 0.12f), matNavy);
            Box("WindshieldGlass", body, new Vector3(0f, 1.74f, 1.08f), new Vector3(1.56f, 0.62f, 0.03f), matClearGlass, Quaternion.Euler(-16f, 0f, 0f));
            Box("DoorGlassL", body, new Vector3(-0.87f, 1.74f, 0.50f), new Vector3(0.03f, 0.58f, 0.96f), matClearGlass);
            Box("DoorGlassR", body, new Vector3( 0.87f, 1.74f, 0.50f), new Vector3(0.03f, 0.58f, 0.96f), matClearGlass);
            // Chrome Door Handles
            Box("DoorHandleL", body, new Vector3(-0.94f, 1.26f, 0.16f), new Vector3(0.04f, 0.06f, 0.16f), matChrome);
            Box("DoorHandleR", body, new Vector3( 0.94f, 1.26f, 0.16f), new Vector3(0.04f, 0.06f, 0.16f), matChrome);

            // Left-Side Curved Cylindrical 4x4 Safari Air Snorkel hugging the fender & A-pillar!
            CustomMesh("SculptedSafariSnorkel", body, Vector3.zero, Quaternion.identity, Vector3.one, KenyaPoliceLC79MeshFactory.CurvedSnorkelMesh, matSteelBlack);
            Box("SnorkelRamHead", body, new Vector3(-0.92f, 2.23f, 1.10f), new Vector3(0.14f, 0.14f, 0.18f), matSteelBlack);

            // Open Rear Pickup Troop Bed (Diamond-Plate Floor + Side Walls + Wheel Arches)
            Box("BedDiamondFloor", body, new Vector3(0f, 0.84f, -1.28f), new Vector3(1.76f, 0.10f, 2.38f), matBedFloor);
            Box("BedFrontBulkhead", body, new Vector3(0f, 1.16f, -0.14f), new Vector3(1.78f, 0.66f, 0.08f), matNavy);
            Box("BedSideWallL", body, new Vector3(-0.87f, 1.14f, -1.28f), new Vector3(0.11f, 0.64f, 2.38f), matNavy);
            Box("BedSideWallR", body, new Vector3( 0.87f, 1.14f, -1.28f), new Vector3(0.11f, 0.64f, 2.38f), matNavy);

            // Curved 16-Segment Semi-Circular Off-Road Wheel-Arch Fender Flares (positive scale + Y-rot preserves outward winding!)
            for (int side = -1; side <= 1; side += 2)
            {
                Quaternion archRot = side < 0 ? Quaternion.Euler(0f, 180f, 0f) : Quaternion.identity;
                CustomMesh($"CurvedFenderArchFront_{side}", body, new Vector3(side * 0.89f, 0.52f, 1.58f), archRot, Vector3.one, KenyaPoliceLC79MeshFactory.FenderArchFlareMesh, matSteelBlack);
                CustomMesh($"CurvedFenderArchRear_{side}",  body, new Vector3(side * 0.89f, 0.52f, -1.52f), archRot, Vector3.one, KenyaPoliceLC79MeshFactory.FenderArchFlareMesh, matSteelBlack);
            }

            // =========================================================================
            // 3. ICONIC KENYA POLICE LIVERY: RED & YELLOW STRIPES + CREST + LETTERING!
            // =========================================================================
            for (int side = -1; side <= 1; side += 2)
            {
                float sx = side * 0.935f;
                // Red upper stripe + Yellow lower stripe sweeping along doors, front fenders & bed sides
                Box($"RedStripeCab_{side}", body, new Vector3(sx, 0.98f, 0.56f), new Vector3(0.025f, 0.10f, 1.28f), matStripeRed);
                Box($"YelStripeCab_{side}", body, new Vector3(sx, 0.88f, 0.56f), new Vector3(0.025f, 0.09f, 1.28f), matStripeYellow);
                Box($"RedStripeFront_{side}", body, new Vector3(sx, 1.14f, 1.82f), new Vector3(0.025f, 0.09f, 1.22f), matStripeRed);
                Box($"YelStripeFront_{side}", body, new Vector3(sx, 1.05f, 1.82f), new Vector3(0.025f, 0.08f, 1.22f), matStripeYellow);
                Box($"RedStripeBed_{side}", body, new Vector3(sx, 1.16f, -1.28f), new Vector3(0.025f, 0.10f, 2.34f), matStripeRed);
                Box($"YelStripeBed_{side}", body, new Vector3(sx, 1.06f, -1.28f), new Vector3(0.025f, 0.09f, 2.34f), matStripeYellow);

                // Gold/Red Kenya Police Coat of Arms Crest Quad cleanly forward on front door!
                var crestQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                crestQuad.name = $"KenyaPoliceCrest_{side}";
                crestQuad.transform.SetParent(body, false);
                crestQuad.transform.localPosition = new Vector3(side * 0.948f, 1.15f, 0.88f);
                crestQuad.transform.localRotation = Quaternion.Euler(0f, side < 0 ? 90f : -90f, 0f);
                crestQuad.transform.localScale = new Vector3(0.42f, 0.42f, 1f);
                Object.DestroyImmediate(crestQuad.GetComponent<Collider>());
                crestQuad.GetComponent<Renderer>().sharedMaterial = matCrest;

                // Bold white "KENYA POLICE" lettering clearly separated behind the crest!
                WorldText($"DoorKenyaPoliceTxt_{side}", body, "KENYA POLICE",
                    new Vector3(side * 0.952f, 1.16f, -0.18f), new Vector2(1.15f, 0.28f), 1.48f, Color.white,
                    Quaternion.Euler(0f, side < 0 ? 90f : -90f, 0f));
            }

            // =========================================================================
            // 4. OLIVE-KHAKI CANVAS TROOP CANOPY + CLEAR WINDOWS + STEEL ROLL CAGE + BENCHES!
            // =========================================================================
            var canopy = new GameObject("TroopCarrierCanopy").transform;
            canopy.SetParent(body, false);

            // Black tubular steel interior roll-cage hoops (front, mid, rear) + roof rails
            float[] hoopZs = { -0.22f, -1.26f, -2.34f };
            for (int i = 0; i < hoopZs.Length; i++)
            {
                float hz = hoopZs[i];
                Box($"CagePostL_{i}", canopy, new Vector3(-0.80f, 1.88f, hz), new Vector3(0.055f, 0.92f, 0.055f), matSteelBlack);
                Box($"CagePostR_{i}", canopy, new Vector3( 0.80f, 1.88f, hz), new Vector3(0.055f, 0.92f, 0.055f), matSteelBlack);
                Box($"CageCrossTop_{i}", canopy, new Vector3(0f, 2.34f, hz), new Vector3(1.66f, 0.055f, 0.055f), matSteelBlack);
            }
            Box("CageRailTopL", canopy, new Vector3(-0.80f, 2.34f, -1.28f), new Vector3(0.055f, 0.055f, 2.18f), matSteelBlack);
            Box("CageRailTopR", canopy, new Vector3( 0.80f, 2.34f, -1.28f), new Vector3(0.055f, 0.055f, 2.18f), matSteelBlack);
            Box("CageRailMidC", canopy, new Vector3( 0.00f, 2.38f, -1.28f), new Vector3(0.055f, 0.055f, 2.18f), matSteelBlack);

            // Sculpted Arched & Gently Pleated Olive-Khaki Canvas Canopy Roof draping naturally over steel hoops!
            CustomMesh("SculptedCanvasRoofCrown", canopy, Vector3.zero, Quaternion.identity, Vector3.one, KenyaPoliceLC79MeshFactory.DrapedCanvasRoofMesh, matCanvas);
            Box("CanvasFrontCurtain", canopy, new Vector3(0f, 1.92f, -0.16f), new Vector3(1.78f, 0.94f, 0.06f), matCanvas);

            // Side canvas curtains with 2 clear glossy plastic windows on each side!
            for (int side = -1; side <= 1; side += 2)
            {
                float cx = side * 0.89f;
                // Upper & lower canvas hem + 3 vertical pillars framing the 2 clear plastic side windows
                Box($"CanvasUpperHem_{side}", canopy, new Vector3(cx, 2.24f, -1.28f), new Vector3(0.05f, 0.28f, 2.36f), matCanvas);
                Box($"CanvasLowerHem_{side}", canopy, new Vector3(cx, 1.56f, -1.28f), new Vector3(0.05f, 0.24f, 2.36f), matCanvas);
                Box($"CanvasPillarFront_{side}", canopy, new Vector3(cx, 1.90f, -0.28f), new Vector3(0.05f, 0.56f, 0.36f), matCanvas);
                Box($"CanvasPillarMid_{side}", canopy, new Vector3(cx, 1.90f, -1.28f), new Vector3(0.05f, 0.56f, 0.34f), matCanvas);
                Box($"CanvasPillarRear_{side}", canopy, new Vector3(cx, 1.90f, -2.28f), new Vector3(0.05f, 0.56f, 0.36f), matCanvas);
                // 2 clear plastic windows inside the olive canvas!
                Box($"CanvasClearWin1_{side}", canopy, new Vector3(cx, 1.90f, -0.78f), new Vector3(0.02f, 0.46f, 0.68f), matClearGlass);
                Box($"CanvasClearWin2_{side}", canopy, new Vector3(cx, 1.90f, -1.78f), new Vector3(0.02f, 0.46f, 0.68f), matClearGlass);
            }

            // Rolled-up Rear Canvas Flap with 3 leather buckle straps across top of rear opening!
            Cylinder("CanvasRearRoll", canopy, new Vector3(0f, 2.34f, -2.42f), new Vector3(0.24f, 0.92f, 0.24f), matCanvas, Quaternion.Euler(0f, 0f, 90f));
            float[] strapXs = { -0.58f, 0f, 0.58f };
            for (int s = 0; s < strapXs.Length; s++)
            {
                Cylinder($"RearStrap_{s}", canopy, new Vector3(strapXs[s], 2.34f, -2.42f), new Vector3(0.26f, 0.035f, 0.26f), matLeatherStrap, Quaternion.Euler(0f, 0f, 90f));
            }

            // 2 Glossy Black Vinyl Longitudinal Troop Benches inside the open rear bed!
            for (int side = -1; side <= 1; side += 2)
            {
                Box($"TroopBenchSeat_{side}", canopy, new Vector3(side * 0.54f, 1.24f, -1.28f), new Vector3(0.42f, 0.12f, 2.08f), matVinylSeat);
                Box($"TroopBenchBack_{side}", canopy, new Vector3(side * 0.74f, 1.56f, -1.28f), new Vector3(0.10f, 0.52f, 2.08f), matVinylSeat);
                Box($"TroopBenchLeg1_{side}", canopy, new Vector3(side * 0.40f, 1.04f, -0.45f), new Vector3(0.05f, 0.34f, 0.05f), matSteelBlack);
                Box($"TroopBenchLeg2_{side}", canopy, new Vector3(side * 0.40f, 1.04f, -1.28f), new Vector3(0.05f, 0.34f, 0.05f), matSteelBlack);
                Box($"TroopBenchLeg3_{side}", canopy, new Vector3(side * 0.40f, 1.04f, -2.10f), new Vector3(0.05f, 0.34f, 0.05f), matSteelBlack);
            }

            // =========================================================================
            // 5. WORKING FLASHING RED / BLUE ROOF LIGHTBAR + KENYA POLICE SIREN!
            // =========================================================================
            var barRoot = new GameObject("KenyaPoliceLightbar").transform;
            barRoot.SetParent(body, false);
            barRoot.localPosition = new Vector3(0f, 2.22f, 0.56f);

            Box("LightbarMountL", barRoot, new Vector3(-0.62f, -0.06f, 0f), new Vector3(0.10f, 0.10f, 0.14f), matSteelBlack);
            Box("LightbarMountR", barRoot, new Vector3( 0.62f, -0.06f, 0f), new Vector3(0.10f, 0.10f, 0.14f), matSteelBlack);
            Box("LightbarCenterBox", barRoot, Vector3.zero, new Vector3(0.46f, 0.18f, 0.28f), matSteelBlack);
            WorldText("BarPoliceFront", barRoot, "POLICE", new Vector3(0f, 0f,  0.15f), new Vector2(0.44f, 0.15f), 0.92f, Color.white, Quaternion.Euler(0f, 180f, 0f));
            WorldText("BarPoliceRear",  barRoot, "POLICE", new Vector3(0f, 0f, -0.15f), new Vector2(0.44f, 0.15f), 0.92f, Color.white, Quaternion.identity);

            var redPod = CustomMesh("LightbarRedBank", barRoot, new Vector3(-0.46f, 0f, 0f), Quaternion.identity, Vector3.one, KenyaPoliceLC79MeshFactory.BeveledLightPodMesh, matBarRed);
            var bluePod = CustomMesh("LightbarBlueBank", barRoot, new Vector3( 0.46f, 0f, 0f), Quaternion.identity, Vector3.one, KenyaPoliceLC79MeshFactory.BeveledLightPodMesh, matBarBlue);

            var redPlGo = new GameObject("PoliceRedPointLight");
            redPlGo.transform.SetParent(barRoot, false);
            redPlGo.transform.localPosition = new Vector3(-0.55f, 0.22f, 0f);
            var redPl = redPlGo.AddComponent<Light>();
            redPl.type = LightType.Point;
            redPl.color = new Color(1f, 0.12f, 0.12f);
            redPl.range = 24f;
            redPl.intensity = 0f;
            redPl.enabled = false;

            var bluePlGo = new GameObject("PoliceBluePointLight");
            bluePlGo.transform.SetParent(barRoot, false);
            bluePlGo.transform.localPosition = new Vector3(0.55f, 0.22f, 0f);
            var bluePl = bluePlGo.AddComponent<Light>();
            bluePl.type = LightType.Point;
            bluePl.color = new Color(0.12f, 0.42f, 1f);
            bluePl.range = 24f;
            bluePl.intensity = 0f;
            bluePl.enabled = false;

            var emergencySys = root.AddComponent<KenyaPoliceEmergencySystem>();
            emergencySys.redBarRenderers = new[] { redPod.GetComponent<Renderer>() };
            emergencySys.blueBarRenderers = new[] { bluePod.GetComponent<Renderer>() };
            emergencySys.redStrobeLight = redPl;
            emergencySys.blueStrobeLight = bluePl;

            // =========================================================================
            // 6. HEADLIGHTS, BLINKERS, VERTICAL TAILLIGHTS & YELLOW KCP 380D PLATES
            // =========================================================================
            var headL = Box("HeadlightL", body, new Vector3(-0.68f, 1.12f, 2.50f), new Vector3(0.28f, 0.22f, 0.06f), matHead);
            var headR = Box("HeadlightR", body, new Vector3( 0.68f, 1.12f, 2.50f), new Vector3(0.28f, 0.22f, 0.06f), matHead);
            var indFL = Box("IndFrontL", body, new Vector3(-0.84f, 1.12f, 2.48f), new Vector3(0.10f, 0.20f, 0.08f), matIndicator);
            var indFR = Box("IndFrontR", body, new Vector3( 0.84f, 1.12f, 2.48f), new Vector3(0.10f, 0.20f, 0.08f), matIndicator);
            var tailL = Box("TailBrakeL", body, new Vector3(-0.84f, 1.10f, -2.48f), new Vector3(0.14f, 0.34f, 0.06f), matTail);
            var tailR = Box("TailBrakeR", body, new Vector3( 0.84f, 1.10f, -2.48f), new Vector3(0.14f, 0.34f, 0.06f), matTail);
            var indRL = Box("IndRearL", body, new Vector3(-0.84f, 0.88f, -2.48f), new Vector3(0.14f, 0.14f, 0.06f), matIndicator);
            var indRR = Box("IndRearR", body, new Vector3( 0.84f, 0.88f, -2.48f), new Vector3(0.14f, 0.14f, 0.06f), matIndicator);

            Box("FrontPlate", body, new Vector3(0f, 0.68f, 2.71f), new Vector3(0.54f, 0.16f, 0.04f), matPlate);
            WorldText("FrontPlateTxt", body, "KCP 380D", new Vector3(0f, 0.68f, 2.74f), new Vector2(0.52f, 0.15f), 1.05f, Palette.EnaStripeBlack, Quaternion.Euler(0f, 180f, 0f));
            Box("RearPlate", body, new Vector3(0f, 0.72f, -2.54f), new Vector3(0.56f, 0.17f, 0.04f), matPlate);
            WorldText("RearPlateTxt", body, "KCP 380D", new Vector3(0f, 0.72f, -2.57f), new Vector2(0.54f, 0.16f), 1.08f, Palette.EnaStripeBlack, Quaternion.identity);

            var spotL = CreateHeadlightSpot(root.transform, "HeadlightBeamL", new Vector3(-0.68f, 1.12f, 2.66f));
            var spotR = CreateHeadlightSpot(root.transform, "HeadlightBeamR", new Vector3( 0.68f, 1.12f, 2.66f));
            ctrl.headlightSpots = new[] { spotL, spotR };
            ctrl.headlightRenderers = new[] { headL.GetComponent<Renderer>(), headR.GetComponent<Renderer>() };
            ctrl.brakeLightRenderers = new[] { tailL.GetComponent<Renderer>(), tailR.GetComponent<Renderer>() };
            ctrl.leftIndicatorRenderers = new[] { indFL.GetComponent<Renderer>(), indRL.GetComponent<Renderer>() };
            ctrl.rightIndicatorRenderers = new[] { indFR.GetComponent<Renderer>(), indRR.GetComponent<Renderer>() };

            // =========================================================================
            // 7. REALISTIC RIGHT-HAND DRIVE (RHD) LAND CRUISER 79 COCKPIT + VHF RADIO + LIVE SIDE MIRRORS!
            // =========================================================================
            var cockpit = new GameObject("RhdCockpitInterior").transform;
            cockpit.SetParent(body, false);
            Box("PassengerGrabBar", cockpit, new Vector3(-0.46f, 1.44f, 0.72f), new Vector3(0.44f, 0.05f, 0.05f), matSteelBlack);
            Box("DriverSeatBack", cockpit, new Vector3(0.44f, 1.30f, 0.04f), new Vector3(0.48f, 0.68f, 0.12f), matVinylSeat);
            Box("PassengerSeatBack", cockpit, new Vector3(-0.44f, 1.30f, 0.04f), new Vector3(0.48f, 0.68f, 0.12f), matVinylSeat);

            // Properly framed Toyota LC79 RHD Dashboard + Smooth Sculpted Torus Steering Wheel + Police VHF Radio + Real Textured Gauges!
            var lcDashHost = new GameObject("LcRealisticDashHost").transform;
            lcDashHost.SetParent(cockpit, false);
            lcDashHost.localPosition = new Vector3(0f, 1.50f, 0.90f);

            KenyaVehicleFeaturesBuilder.BuildFullRealisticCabCockpit(
                root, body, lcDashHost,
                driverX: 0.42f, halfW: 0.92f, halfL: 2.50f,
                isCoach: false, isPoliceLC79: true,
                brandName: "TOYOTA", plateNum: "KCP 380D",
                matInterior: matInterior, matBlack: matBlack, matChrome: matChrome,
                matBodyAccent: matNavy, matClearGlass: matClearGlass,
                out Transform steerPivot, out TextMeshPro lcSpeedTmp, out TextMeshPro lcStatusTmp,
                buildOuterPillars: false, // LC79 already has its own sculpted real cab A-pillars & roof!
                externalWiperParent: body, externalWiperPos: new Vector3(0f, 1.53f, 1.26f));

            ctrl.steeringWheelVisual = steerPivot;
            ctrl.steeringWheelSpinAxis = Vector3.up;

            // BOTH Working Live Left & Right Exterior Side Mirrors angled cleanly toward the RHD driver's eyes!
            var mirrorL = BuildMirror(body, "PoliceMirrorL", new Vector3(-0.98f, 1.62f, 1.44f), new Vector3(-0.88f, 1.60f, 1.08f), new Vector3(0f, -34f, 0f), new Vector2(0.24f, 0.36f), matSteelBlack, matChrome);
            var mirrorR = BuildMirror(body, "PoliceMirrorR", new Vector3( 1.06f, 1.62f, 0.98f), new Vector3( 0.88f, 1.60f, 0.90f), new Vector3(0f,  26f, 0f), new Vector2(0.24f, 0.36f), matSteelBlack, matChrome);
            var camL = new GameObject("MirrorCamL").transform;
            camL.SetParent(root.transform, false);
            camL.localPosition = new Vector3(-1.24f, 1.62f, 0.96f);
            camL.localRotation = Quaternion.Euler(4f, 173f, 0f);
            var camR = new GameObject("MirrorCamR").transform;
            camR.SetParent(root.transform, false);
            camR.localPosition = new Vector3(1.14f, 1.62f, 0.86f);
            camR.localRotation = Quaternion.Euler(4f, 187f, 0f);

            var mirrors = root.AddComponent<KenyaVehicleMirrors>();
            mirrors.leftMirrorSurfaces = new[] { mirrorL };
            mirrors.rightMirrorSurfaces = new[] { mirrorR };
            mirrors.leftMirrorCameraAnchor = camL;
            mirrors.rightMirrorCameraAnchor = camR;
            mirrors.dashSpeedText = lcSpeedTmp;
            mirrors.dashStatusText = lcStatusTmp;

            var cockpitCam = new GameObject("CockpitCameraAnchor").transform;
            cockpitCam.SetParent(root.transform, false);
            cockpitCam.localPosition = new Vector3(0.42f, 1.72f, 0.24f);
            cockpitCam.localRotation = Quaternion.Euler(8.5f, 0f, 0f);
            ctrl.cockpitCameraAnchor = cockpitCam;

            var dashCam = new GameObject("DashcamAnchor").transform;
            dashCam.SetParent(root.transform, false);
            dashCam.localPosition = new Vector3(0f, 1.56f, 2.52f);
            dashCam.localRotation = Quaternion.Euler(2f, 0f, 0f);
            ctrl.bumperCameraAnchor = dashCam;

            var axleCam = new GameObject("AxleCameraAnchor").transform;
            axleCam.SetParent(root.transform, false);
            axleCam.localPosition = new Vector3(-1.65f, 0.82f, 0.65f);
            axleCam.localRotation = Quaternion.Euler(4f, 14f, 0f);
            ctrl.axleCameraAnchor = axleCam;

            // 4x4 Heavy Off-Road Mud-Terrain Tires + Black Steel 6-Lug Wheels
            ctrl.wheels = BuildOffroad4x4Wheels(root.transform, matTire, matWheelSteel, matChrome);
            return ctrl;
        }

        private static KenyaStreetVehicle.WheelEntry[] BuildOffroad4x4Wheels(Transform root, Material matTire, Material matSteelRim, Material matLug)
        {
            var colParent = new GameObject("WheelColliders").transform;
            colParent.SetParent(root, false);
            var visParent = new GameObject("WheelVisuals").transform;
            visParent.SetParent(root, false);

            float[] axleZs = { 1.58f, -1.52f };
            float halfTrack = 0.88f;
            float radius = 0.44f;
            float susp = 0.22f;
            var list = new KenyaStreetVehicle.WheelEntry[4];
            int idx = 0;

            for (int a = 0; a < 2; a++)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    bool isLeft = side < 0;
                    string tag = $"LC_{a}_{(isLeft ? "L" : "R")}";

                    var wcGo = new GameObject($"WC_{tag}");
                    wcGo.transform.SetParent(colParent, false);
                    wcGo.transform.localPosition = new Vector3(side * halfTrack, radius + susp * 0.4f, axleZs[a]);

                    var wc = wcGo.AddComponent<WheelCollider>();
                    wc.radius = radius;
                    wc.suspensionDistance = susp;
                    wc.mass = 45f;
                    wc.wheelDampingRate = 0.45f;

                    var sp = wc.suspensionSpring;
                    sp.spring = 42000f;
                    sp.damper = 4600f;
                    sp.targetPosition = 0.48f;
                    wc.suspensionSpring = sp;

                    var ff = wc.forwardFriction; ff.stiffness = 1.85f; wc.forwardFriction = ff;
                    var sf = wc.sidewaysFriction; sf.stiffness = 2.05f; wc.sidewaysFriction = sf;

                    var vis = new GameObject($"WV_{tag}").transform;
                    vis.SetParent(visParent, false);
                    vis.localPosition = wcGo.transform.localPosition;

                    // Sculpted 28-segment lathed mud-terrain tire + concave dished black steel rim + 6 chrome lug nuts!
                    CustomMesh("SculptedOffroadTire", vis, Vector3.zero, Quaternion.identity, Vector3.one, KenyaPoliceLC79MeshFactory.OffroadTireMesh, matTire);
                    CustomMesh("SculptedDishedRim",   vis, Vector3.zero, Quaternion.identity, Vector3.one, KenyaPoliceLC79MeshFactory.DishedRimMesh, matSteelRim);
                    Cylinder("HubLock", vis, Vector3.zero, new Vector3(radius * 0.34f, 0.145f, radius * 0.34f), matLug, Quaternion.Euler(0f, 0f, 90f));
                    for (int lug = 0; lug < 6; lug++)
                    {
                        float la = lug * 60f * Mathf.Deg2Rad;
                        Cylinder($"LugNut_{lug}", vis, new Vector3(side * 0.115f, Mathf.Sin(la) * 0.115f, Mathf.Cos(la) * 0.115f), new Vector3(0.024f, 0.022f, 0.024f), matLug, Quaternion.Euler(0f, 0f, 90f));
                    }

                    list[idx++] = new KenyaStreetVehicle.WheelEntry
                    {
                        collider = wc,
                        visualMesh = vis,
                        isSteer = (a == 0),
                        isDrive = true, // Full-time Kenyan 4x4 AWD torque!
                        isLeft = isLeft
                    };
                }
            }
            return list;
        }

        private static Renderer BuildMirror(Transform parent, string name, Vector3 headPos, Vector3 armRootPos, Vector3 headEuler, Vector2 glassSize, Material matHousing, Material matChrome)
        {
            var holder = new GameObject(name).transform;
            holder.SetParent(parent, false);
            holder.localPosition = headPos;
            holder.localRotation = Quaternion.Euler(headEuler);

            Vector3 backOfHousing = headPos + Quaternion.Euler(headEuler) * new Vector3(0f, 0f, 0.06f);
            Vector3 toRoot = armRootPos - backOfHousing;
            var arm = Box("MirrorArm", parent, (backOfHousing + armRootPos) * 0.5f, new Vector3(0.045f, 0.045f, toRoot.magnitude), matHousing);
            if (toRoot.sqrMagnitude > 0.001f) arm.transform.localRotation = Quaternion.LookRotation(toRoot.normalized, Vector3.up);

            Box("Housing", holder, new Vector3(0f, 0f, 0.035f), new Vector3(glassSize.x + 0.04f, glassSize.y + 0.04f, 0.055f), matHousing);
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "MirrorLiveGlass";
            quad.transform.SetParent(holder, false);
            quad.transform.localPosition = new Vector3(0f, 0f, 0.004f);
            quad.transform.localScale = new Vector3(glassSize.x, glassSize.y, 1f);
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            var r = quad.GetComponent<Renderer>();
            r.sharedMaterial = matChrome;
            return r;
        }

        private static Light CreateHeadlightSpot(Transform parent, string name, Vector3 localPos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(8f, 0f, 0f);
            var spot = go.AddComponent<Light>();
            spot.type = LightType.Spot;
            spot.color = Palette.HeadlightWarm;
            spot.range = 135f;
            spot.spotAngle = 74f;
            spot.innerSpotAngle = 36f;
            spot.intensity = 520f;
            spot.shadows = LightShadows.None;
            spot.enabled = false;
            return spot;
        }

        private static GameObject CustomMesh(string name, Transform parent, Vector3 pos, Quaternion rot, Vector3 scale, Mesh mesh, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = rot;
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        private static Material LoadOrCreateMat(
            string key, Shader urpLit, Color col, float smoothness, float metallic,
            Texture2D albedo = null, Texture2D normal = null, Vector2? tiling = null,
            float bumpScale = 1f, bool alphaClip = false, Color? emission = null)
        {
            string path = $"Assets/KenyaStreetSim/Materials/M_{key}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(urpLit);
                AssetDatabase.CreateAsset(mat, path);
            }
            else if (mat.shader != urpLit) mat.shader = urpLit;

            mat.SetColor("_BaseColor", col);
            mat.SetFloat("_Smoothness", smoothness);
            mat.SetFloat("_Metallic", metallic);
            if (albedo != null) { mat.SetTexture("_BaseMap", albedo); mat.SetTextureScale("_BaseMap", tiling ?? Vector2.one); }
            else mat.SetTexture("_BaseMap", null);

            if (normal != null)
            {
                mat.EnableKeyword("_NORMALMAP");
                mat.SetTexture("_BumpMap", normal);
                mat.SetFloat("_BumpScale", bumpScale);
                mat.SetTextureScale("_BumpMap", tiling ?? Vector2.one);
            }
            else mat.DisableKeyword("_NORMALMAP");

            if (alphaClip)
            {
                mat.EnableKeyword("_ALPHATEST_ON");
                mat.SetFloat("_AlphaClip", 1f);
                mat.SetFloat("_Cutoff", 0.35f);
                mat.SetFloat("_Cull", 0f);
            }
            else
            {
                mat.DisableKeyword("_ALPHATEST_ON");
                mat.SetFloat("_AlphaClip", 0f);
            }

            if (emission.HasValue)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", emission.Value);
            }
            else
            {
                mat.DisableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", Color.black);
            }

            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static GameObject Box(string name, Transform parent, Vector3 pos, Vector3 scale, Material mat, Quaternion? rot = null)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = rot ?? Quaternion.identity;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        private static GameObject Cylinder(string name, Transform parent, Vector3 pos, Vector3 scale, Material mat, Quaternion? rot = null)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = rot ?? Quaternion.identity;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        private static TextMeshPro WorldText(string name, Transform parent, string txt, Vector3 pos, Vector2 size, float fontSize, Color col, Quaternion rot)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = rot;
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.text = txt;
            tmp.fontSize = fontSize;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = col;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.rectTransform.sizeDelta = size;
            return tmp;
        }
    }
}
