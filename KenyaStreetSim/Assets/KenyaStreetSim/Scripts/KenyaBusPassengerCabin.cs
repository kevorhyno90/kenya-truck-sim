using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace KenyaStreetSim
{
    /// <summary>
    /// Visible 3D Seated Kenyan Passengers inside every player Coach & Matatu!
    /// - Automatically seats up to 44 passengers inside Coaches (Savanna Express, ENA Scania, Marcopolo G7, Yutong)
    ///   and 14 passengers inside the Migori Matatu — visible through crystal-clear tinted side windows & from cockpit!
    /// - Upgrades opaque dark bus glass materials to realistic see-through tinted crystal glass so the cabin is
    ///   never mysteriously empty when you're carrying 40+ passengers!
    /// - Syncs live seated occupancy with StreetGameLoop boarding stages and animates subtle head/body sway while driving.
    /// </summary>
    public class KenyaBusPassengerCabin : MonoBehaviour
    {
        public int MaxSeatCapacity { get; private set; } = 44;
        public int CurrentOnboardCount { get; private set; }

        private struct SeatedPassenger
        {
            public Transform root;
            public Transform head;
            public Vector3 baseLocalPos;
            public float phase;
            public float swayAmp;
        }

        private KenyaStreetVehicle vehicle;
        private readonly List<SeatedPassenger> seats = new List<SeatedPassenger>(52);
        private Transform cabinRoot;
        private Light interiorDomeLight;
        private bool built;

        // Shared lightweight meshes & materials across all buses (tiny draw cost, zero shadow overhead)
        private static Mesh sharedBoxMesh;
        private static Mesh sharedSphereMesh;
        private static Mesh sharedCylMesh;
        private static Material[] sharedSkinMats;
        private static Material[] sharedOutfitMats;
        private static Material[] sharedHairMats;
        private static Material sharedPantsMat;
        private static Material sharedCrystalSideGlassMat;

        private void Awake()
        {
            vehicle = GetComponent<KenyaStreetVehicle>();
            if (vehicle != null && vehicle.jobRole == KenyaStreetVehicle.VehicleJobRole.BusPassenger)
            {
                MaxSeatCapacity = vehicle.isCoach ? 44 : 14;
            }
            else
            {
                MaxSeatCapacity = 0;
            }
        }

        private void Start()
        {
            if (vehicle == null || vehicle.jobRole != KenyaStreetVehicle.VehicleJobRole.BusPassenger)
            {
                enabled = false;
                return;
            }

            EnsureSharedAssets();
            MakeSideWindowsSeeThrough();
            BuildSeatedPassengers();

            // Realistic departing passenger load so your bus already looks alive at the start terminal,
            // then fills up further as you board commuters at Kenyan town stages!
            int initialLoad = vehicle.isCoach ? 18 : 6;
            SetOnboardCount(initialLoad);

            if (StreetGameLoop.Instance != null)
            {
                StreetGameLoop.Instance.OnStageBoarded += HandleStageBoarded;
                StreetGameLoop.Instance.OnStateChanged += HandleRunStateChanged;
            }
        }

        private void OnDestroy()
        {
            if (StreetGameLoop.Instance != null)
            {
                StreetGameLoop.Instance.OnStageBoarded -= HandleStageBoarded;
                StreetGameLoop.Instance.OnStateChanged -= HandleRunStateChanged;
            }
        }

        private void HandleRunStateChanged(StreetGameLoop.RunState state)
        {
            if (state == StreetGameLoop.RunState.Running)
            {
                int initialLoad = vehicle != null && vehicle.isCoach ? 18 : 6;
                SetOnboardCount(initialLoad);
            }
        }

        private void HandleStageBoarded(BusStageZone stage)
        {
            if (StreetGameLoop.Instance == null || StreetGameLoop.Instance.ActiveVehicle != vehicle) return;
            if (stage == null) return;

            // Realistic Kenyan corridor flow: a few passengers alight at intermediate towns while new ones board!
            int alighting = (CurrentOnboardCount > MaxSeatCapacity * 0.65f)
                ? Mathf.Min(CurrentOnboardCount - 8, Mathf.Max(2, stage.passengerCount / 3))
                : 0;
            int boarding = Mathf.Max(4, stage.passengerCount);
            int nextCount = Mathf.Clamp(CurrentOnboardCount - alighting + boarding, 1, MaxSeatCapacity);
            SetOnboardCount(nextCount);
        }

        public void SetOnboardCount(int count)
        {
            CurrentOnboardCount = Mathf.Clamp(count, 0, seats.Count);
            for (int i = 0; i < seats.Count; i++)
            {
                if (seats[i].root != null)
                {
                    seats[i].root.gameObject.SetActive(i < CurrentOnboardCount);
                }
            }
        }

        private void Update()
        {
            if (!built || vehicle == null) return;

            // Subtle warm interior dome light at sunset/night so you see all 40 passengers glowing through the windows!
            if (interiorDomeLight != null)
            {
                bool duskOrNight = KenyaTimeOfDay.Instance != null && KenyaTimeOfDay.Instance.CurrentPreset != KenyaTimeOfDay.TimePreset.Day;
                bool activeBus = StreetGameLoop.Instance != null && StreetGameLoop.Instance.ActiveVehicle == vehicle;
                interiorDomeLight.enabled = duskOrNight && activeBus;
            }

            if (CurrentOnboardCount <= 0) return;

            // Gentle organic bus-ride sway & head movement proportional to speed & steering
            float speedFactor = Mathf.Clamp01(vehicle.SpeedKmh / 60f);
            float steerLean = -vehicle.SteerInput * 0.045f * speedFactor;
            float t = Time.time;

            for (int i = 0; i < CurrentOnboardCount && i < seats.Count; i++)
            {
                var p = seats[i];
                if (p.root == null) continue;
                float bob = Mathf.Sin(t * (2.6f + p.swayAmp) + p.phase) * (0.012f + 0.018f * speedFactor);
                p.root.localPosition = p.baseLocalPos + new Vector3(steerLean, bob, 0f);
                if (p.head != null)
                {
                    float yawLook = Mathf.Sin(t * 0.75f + p.phase * 1.7f) * 14f;
                    p.head.localRotation = Quaternion.Euler(0f, yawLook, steerLean * 120f);
                }
            }
        }

        private void MakeSideWindowsSeeThrough()
        {
            // Some imported GLB/Blender coaches ship with near-opaque dark black glass (alpha 0.99) that hides the interior.
            // Convert any glass/window material with alpha > 0.45 into clean crystal-tinted see-through glass!
            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                if (r == null || r.sharedMaterial == null) continue;
                string mn = r.sharedMaterial.name.ToLowerInvariant();
                string on = r.gameObject.name.ToLowerInvariant();
                if (on.Contains("mirror") || on.Contains("head") || on.Contains("fog") || on.Contains("tail")) continue;

                bool isWindowGlass = mn.Contains("gl_glass") || mn.Contains("glass") || mn.Contains("window") ||
                                     on.Contains("window") || on.Contains("glass");
                if (!isWindowGlass) continue;
                if (mn.Contains("scaniacrystal")) continue; // Already crystal transparent!

                // Preserve existing transparent materials that are already clear enough
                if (r.sharedMaterial.HasProperty("_BaseColor"))
                {
                    var c = r.sharedMaterial.GetColor("_BaseColor");
                    if (c.a < 0.32f && r.sharedMaterial.HasProperty("_Surface") && r.sharedMaterial.GetFloat("_Surface") > 0.5f)
                        continue;
                }

                r.sharedMaterial = sharedCrystalSideGlassMat;
                r.shadowCastingMode = ShadowCastingMode.Off;
            }
        }

        private void BuildSeatedPassengers()
        {
            if (built) return;
            built = true;

            cabinRoot = new GameObject("SeatedKenyanPassengers").transform;
            cabinRoot.SetParent(transform, false);
            cabinRoot.localPosition = Vector3.zero;
            cabinRoot.localRotation = Quaternion.identity;

            var seatCoords = ComputeSeatPositions();
            MaxSeatCapacity = seatCoords.Count;

            // Seat order: fill window seats first (most visible from exterior & showcase cameras!), then aisle seats!
            seatCoords.Sort((a, b) =>
            {
                bool aWin = Mathf.Abs(a.x) > 0.62f;
                bool bWin = Mathf.Abs(b.x) > 0.62f;
                if (aWin != bWin) return aWin ? -1 : 1;
                return b.z.CompareTo(a.z); // front rows fill first
            });

            int seed = Mathf.Abs(name.GetHashCode()) + 17;
            for (int i = 0; i < seatCoords.Count; i++)
            {
                var sp = CreateSeatedPassenger(cabinRoot, $"Pax_{i:00}", seatCoords[i], seed + i * 31);
                seats.Add(sp);
            }

            // Subtle warm interior dome light (no shadows!) for dusk/night visibility
            var lightGo = new GameObject("CabinInteriorDomeLight");
            lightGo.transform.SetParent(cabinRoot, false);
            lightGo.transform.localPosition = new Vector3(0f, vehicle.isCoach ? 2.85f : 2.15f, 0.2f);
            interiorDomeLight = lightGo.AddComponent<Light>();
            interiorDomeLight.type = LightType.Point;
            interiorDomeLight.color = new Color(1.0f, 0.88f, 0.68f);
            interiorDomeLight.intensity = 2.8f;
            interiorDomeLight.range = vehicle.isCoach ? 10.5f : 5.5f;
            interiorDomeLight.shadows = LightShadows.None;
            interiorDomeLight.enabled = false;
        }

        private List<Vector3> ComputeSeatPositions()
        {
            var list = new List<Vector3>(52);

            // Detect real seat height/span if the vehicle mesh has named seat objects (Savanna Coach, ENA Coach)
            bool hasSeatMeshes = false;
            Vector3 sMin = new Vector3(999f, 999f, 999f);
            Vector3 sMax = new Vector3(-999f, -999f, -999f);

            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                if (r == null) continue;
                string nm = r.name.ToLowerInvariant();
                if (nm.Contains("coachseat") || nm.Contains("passengers_seat"))
                {
                    Vector3 c = transform.InverseTransformPoint(r.bounds.center);
                    sMin = Vector3.Min(sMin, c);
                    sMax = Vector3.Max(sMax, c);
                    hasSeatMeshes = true;
                }
            }

            if (vehicle.isCoach)
            {
                // 11 rows x 4 seats (2 left + 2 right) = 44 authentic Kenyan coach seats!
                float zFront = hasSeatMeshes ? Mathf.Clamp(sMax.z - 0.15f, 2.5f, 5.4f) : 4.2f;
                float zBack  = hasSeatMeshes ? Mathf.Clamp(sMin.z + 0.15f, -6.2f, -3.2f) : -4.8f;
                float seatY  = hasSeatMeshes ? Mathf.Clamp(sMin.y + 0.18f, 1.45f, 1.85f) : 1.62f;
                float xOuter = hasSeatMeshes ? Mathf.Clamp(Mathf.Max(Mathf.Abs(sMin.x), Mathf.Abs(sMax.x)), 0.78f, 1.02f) : 0.88f;
                float xInner = xOuter - 0.44f;

                int rows = 11;
                for (int r = 0; r < rows; r++)
                {
                    float frac = rows > 1 ? (float)r / (rows - 1) : 0f;
                    float z = Mathf.Lerp(zFront, zBack, frac);
                    list.Add(new Vector3(-xOuter, seatY, z)); // Left window
                    list.Add(new Vector3(-xInner, seatY, z)); // Left aisle
                    list.Add(new Vector3( xInner, seatY, z)); // Right aisle
                    list.Add(new Vector3( xOuter, seatY, z)); // Right window
                }
            }
            else
            {
                // Migori 14-Seater Matatu layout (4 passenger rows inside van cabin behind driver)
                float seatY = 1.02f;
                float[] rowZs = { 0.85f, -0.10f, -1.05f, -2.00f };
                float[] rowXs3 = { -0.56f, 0.0f, 0.56f };
                float[] rowXs4 = { -0.66f, -0.22f, 0.22f, 0.66f };

                // Front co-driver bench (2 seats left of RHD driver)
                list.Add(new Vector3(-0.52f, seatY, 1.65f));
                list.Add(new Vector3(-0.08f, seatY, 1.65f));

                for (int r = 0; r < rowZs.Length; r++)
                {
                    float[] xs = (r == rowZs.Length - 1) ? rowXs4 : rowXs3;
                    // Keep total exactly 14 seats (2 + 3 + 3 + 2 + 4 = 14)
                    if (r == 2)
                    {
                        list.Add(new Vector3(-0.56f, seatY, rowZs[r]));
                        list.Add(new Vector3(0.56f, seatY, rowZs[r]));
                    }
                    else
                    {
                        for (int c = 0; c < xs.Length; c++)
                        {
                            list.Add(new Vector3(xs[c], seatY, rowZs[r]));
                        }
                    }
                }
            }

            return list;
        }

        private static SeatedPassenger CreateSeatedPassenger(Transform parent, string name, Vector3 seatPos, int seed)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.localPosition = seatPos;
            root.localRotation = Quaternion.identity;

            var skinMat = sharedSkinMats[Mathf.Abs(seed) % sharedSkinMats.Length];
            var outfitMat = sharedOutfitMats[Mathf.Abs(seed * 7 + 3) % sharedOutfitMats.Length];
            var hairMat = sharedHairMats[Mathf.Abs(seed * 13 + 5) % sharedHairMats.Length];

            // Scale variation so passengers look like distinct real people (adults, youths, commuters)
            float scaleH = 0.92f + ((Mathf.Abs(seed * 19) % 100) / 100f) * 0.16f;
            root.localScale = new Vector3(1f, scaleH, 1f);

            // Seated thighs/lap
            AddPart(root, "Lap", sharedBoxMesh, sharedPantsMat, new Vector3(0f, 0.08f, 0.16f), new Vector3(0.32f, 0.14f, 0.36f));
            // Torso (vibrant Kenyan shirts, jackets, kitenge tops)
            AddPart(root, "Torso", sharedBoxMesh, outfitMat, new Vector3(0f, 0.36f, -0.02f), new Vector3(0.36f, 0.44f, 0.21f));
            // Shoulders rounded silhouette
            AddPart(root, "Shoulders", sharedCylMesh, outfitMat, new Vector3(0f, 0.54f, -0.02f), new Vector3(0.40f, 0.08f, 0.22f));
            // Neck
            AddPart(root, "Neck", sharedCylMesh, skinMat, new Vector3(0f, 0.62f, 0.0f), new Vector3(0.10f, 0.06f, 0.10f));

            // Head pivot (animates gentle natural looking around!)
            var headPivot = new GameObject("Head").transform;
            headPivot.SetParent(root, false);
            headPivot.localPosition = new Vector3(0f, 0.74f, 0.01f);

            AddPart(headPivot, "Cranium", sharedSphereMesh, skinMat, Vector3.zero, new Vector3(0.21f, 0.24f, 0.22f));
            // Hair / headwrap / cap cap-top
            bool hasBrightHeadwrap = (Mathf.Abs(seed) % 5) == 0;
            AddPart(headPivot, "HairCap", sharedSphereMesh, hasBrightHeadwrap ? outfitMat : hairMat,
                new Vector3(0f, 0.055f, -0.02f), new Vector3(0.22f, 0.18f, 0.23f));

            return new SeatedPassenger
            {
                root = root,
                head = headPivot,
                baseLocalPos = seatPos,
                phase = (seed % 360) * Mathf.Deg2Rad,
                swayAmp = (seed % 7) * 0.15f
            };
        }

        private static void AddPart(Transform parent, string name, Mesh mesh, Material mat, Vector3 localPos, Vector3 localScale)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }

        private static void EnsureSharedAssets()
        {
            if (sharedBoxMesh != null) return;

            sharedBoxMesh = GetBuiltinMesh(PrimitiveType.Cube);
            sharedSphereMesh = GetBuiltinMesh(PrimitiveType.Sphere);
            sharedCylMesh = GetBuiltinMesh(PrimitiveType.Cylinder);

            Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

            sharedSkinMats = new[]
            {
                MakeSimpleMat(lit, "PaxSkin_1", new Color(0.24f, 0.14f, 0.09f), 0.28f),
                MakeSimpleMat(lit, "PaxSkin_2", new Color(0.32f, 0.19f, 0.12f), 0.28f),
                MakeSimpleMat(lit, "PaxSkin_3", new Color(0.18f, 0.10f, 0.07f), 0.25f),
                MakeSimpleMat(lit, "PaxSkin_4", new Color(0.40f, 0.25f, 0.16f), 0.30f)
            };

            sharedOutfitMats = new[]
            {
                MakeSimpleMat(lit, "PaxOutfit_Blue",    new Color(0.12f, 0.38f, 0.72f), 0.20f),
                MakeSimpleMat(lit, "PaxOutfit_Gold",    new Color(0.88f, 0.62f, 0.12f), 0.20f),
                MakeSimpleMat(lit, "PaxOutfit_Crimson", new Color(0.76f, 0.16f, 0.20f), 0.20f),
                MakeSimpleMat(lit, "PaxOutfit_Green",   new Color(0.12f, 0.56f, 0.32f), 0.20f),
                MakeSimpleMat(lit, "PaxOutfit_Cream",   new Color(0.86f, 0.84f, 0.78f), 0.22f),
                MakeSimpleMat(lit, "PaxOutfit_Purple",  new Color(0.46f, 0.20f, 0.62f), 0.20f),
                MakeSimpleMat(lit, "PaxOutfit_Teal",    new Color(0.10f, 0.54f, 0.58f), 0.20f),
                MakeSimpleMat(lit, "PaxOutfit_Coral",   new Color(0.85f, 0.36f, 0.22f), 0.20f),
                MakeSimpleMat(lit, "PaxOutfit_Navy",    new Color(0.14f, 0.18f, 0.28f), 0.22f)
            };

            sharedHairMats = new[]
            {
                MakeSimpleMat(lit, "PaxHair_Black", new Color(0.06f, 0.06f, 0.07f), 0.15f),
                MakeSimpleMat(lit, "PaxHair_Charcoal", new Color(0.12f, 0.11f, 0.11f), 0.18f)
            };

            sharedPantsMat = MakeSimpleMat(lit, "PaxPants_Denim", new Color(0.14f, 0.18f, 0.26f), 0.18f);

            // Crystal-tinted transparent coach side glass so passengers are clearly visible from outside!
            sharedCrystalSideGlassMat = new Material(lit) { name = "KenyaCoach_CrystalSideGlass" };
            sharedCrystalSideGlassMat.SetFloat("_Surface", 1f);
            sharedCrystalSideGlassMat.SetFloat("_Blend", 0f);
            sharedCrystalSideGlassMat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            sharedCrystalSideGlassMat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            sharedCrystalSideGlassMat.SetFloat("_ZWrite", 0f);
            sharedCrystalSideGlassMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            sharedCrystalSideGlassMat.renderQueue = 3000;
            sharedCrystalSideGlassMat.SetColor("_BaseColor", new Color(0.14f, 0.22f, 0.28f, 0.22f));
            sharedCrystalSideGlassMat.SetFloat("_Smoothness", 0.92f);
            sharedCrystalSideGlassMat.SetFloat("_Metallic", 0.12f);
        }

        private static Material MakeSimpleMat(Shader shader, string name, Color col, float smoothness)
        {
            var m = new Material(shader) { name = name };
            m.SetColor("_BaseColor", col);
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Metallic", 0f);
            return m;
        }

        private static Mesh GetBuiltinMesh(PrimitiveType type)
        {
            var tmp = GameObject.CreatePrimitive(type);
            var mesh = tmp.GetComponent<MeshFilter>().sharedMesh;
            Destroy(tmp);
            return mesh;
        }
    }
}
