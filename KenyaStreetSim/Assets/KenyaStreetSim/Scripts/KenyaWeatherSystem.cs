using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace KenyaStreetSim
{
    /// <summary>
    /// Dynamic Kenyan Highland Weather System:
    /// - 3D drifting volumetric Kenyan cumulus & storm clouds overhead
    /// - Periodic automatic Kenyan highland rain showers + instant manual toggle ([R] key)
    /// - 3D falling rain particle curtain following active camera with driving wind slant
    /// - Dynamic wet reflective highway tarmac sheen & darkened storm ambience during rain
    /// </summary>
    public class KenyaWeatherSystem : MonoBehaviour
    {
        public static KenyaWeatherSystem Instance { get; private set; }

        [Header("References")]
        public Material highwayTarmacMaterial;
        public Light sunLight;

        public bool IsRaining { get; private set; }
        public float RainIntensity { get; private set; } // 0 = clear sunny, 1 = full Kenyan rain shower

        private ParticleSystem rainParticles;
        private Transform rainAnchor;
        private Transform cloudLayerRoot;
        private Material cloudMat;
        private readonly List<Transform> cloudClusters = new List<Transform>();
        private float autoCycleTimer;
        private float nextWeatherChangeSec = 105f;
        private Color dryTarmacColor = new Color(0.533f, 0.549f, 0.580f, 1f);
        private float dryTarmacSmooth = 0.24f;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            if (highwayTarmacMaterial != null)
            {
                if (highwayTarmacMaterial.HasProperty("_BaseColor")) dryTarmacColor = highwayTarmacMaterial.GetColor("_BaseColor");
                if (highwayTarmacMaterial.HasProperty("_Smoothness")) dryTarmacSmooth = highwayTarmacMaterial.GetFloat("_Smoothness");
            }

            Create3DCloudDeck();
            CreateRainParticleSystem();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            // Restore dry tarmac properties cleanly on Exit Play Mode so asset stays crisp
            if (highwayTarmacMaterial != null)
            {
                if (highwayTarmacMaterial.HasProperty("_BaseColor")) highwayTarmacMaterial.SetColor("_BaseColor", dryTarmacColor);
                if (highwayTarmacMaterial.HasProperty("_Smoothness")) highwayTarmacMaterial.SetFloat("_Smoothness", dryTarmacSmooth);
            }
            if (cloudMat != null) Destroy(cloudMat);
        }

        public void SetRaining(bool raining, bool showToast = true)
        {
            IsRaining = raining;
            autoCycleTimer = 0f;
            nextWeatherChangeSec = raining ? 55f : 120f;

            if (showToast && StreetGameLoop.Instance != null && StreetGameLoop.Instance.hud != null)
            {
                StreetGameLoop.Instance.hud.ShowToast(raining
                    ? "WEATHER: KENYAN HIGHLAND RAIN & STORM CLOUDS [R]  •  WIPERS AUTO-ON"
                    : "WEATHER: CLEAR KENYAN SUNSHINE [R]");
            }
        }

        private void Create3DCloudDeck()
        {
            var urpLit = Shader.Find("Universal Render Pipeline/Lit");
            cloudMat = new Material(urpLit);
            cloudMat.SetColor("_BaseColor", new Color(0.95f, 0.96f, 0.99f, 1f));
            cloudMat.SetFloat("_Smoothness", 0.05f);
            cloudMat.SetFloat("_Metallic", 0f);

            cloudLayerRoot = new GameObject("Kenya3DCloudDeck").transform;
            cloudLayerRoot.SetParent(transform, false);

            // Spawn 28 sculpted soft multi-puff Kenyan highland cloud formations drifting across the sky!
            var rng = new System.Random(254);
            for (int i = 0; i < 28; i++)
            {
                var cluster = new GameObject($"KenyaCloud_{i}").transform;
                cluster.SetParent(cloudLayerRoot, false);
                float angle = (i / 28f) * Mathf.PI * 2f + (float)rng.NextDouble() * 0.4f;
                float dist = 120f + (float)rng.NextDouble() * 580f;
                float height = 105f + (float)rng.NextDouble() * 55f;
                cluster.localPosition = new Vector3(Mathf.Cos(angle) * dist, height, Mathf.Sin(angle) * dist);

                int puffs = 3 + (i % 3);
                for (int p = 0; p < puffs; p++)
                {
                    var puff = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    Destroy(puff.GetComponent<Collider>());
                    puff.transform.SetParent(cluster, false);
                    float ox = (p - (puffs - 1) * 0.5f) * 26f + (float)(rng.NextDouble() - 0.5) * 10f;
                    float oy = (float)(rng.NextDouble() - 0.5) * 8f;
                    float oz = (float)(rng.NextDouble() - 0.5) * 18f;
                    float sx = 34f + (float)rng.NextDouble() * 30f;
                    float sy = 11f + (float)rng.NextDouble() * 9f;
                    float sz = 24f + (float)rng.NextDouble() * 22f;
                    puff.transform.localPosition = new Vector3(ox, oy, oz);
                    puff.transform.localScale = new Vector3(sx, sy, sz);
                    var r = puff.GetComponent<Renderer>();
                    r.sharedMaterial = cloudMat;
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    r.receiveShadows = false;
                }
                cloudClusters.Add(cluster);
            }
        }

        private void CreateRainParticleSystem()
        {
            var go = new GameObject("KenyaRainCurtain");
            go.transform.SetParent(transform, false);
            rainAnchor = go.transform;

            rainParticles = go.AddComponent<ParticleSystem>();
            var main = rainParticles.main;
            main.loop = true;
            main.startLifetime = 0.72f;
            main.startSpeed = 34f;
            main.startSize = 0.026f;
            main.maxParticles = 2400;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = new Color(0.84f, 0.92f, 1f, 0.36f);

            var emission = rainParticles.emission;
            emission.rateOverTime = 0f;

            var shape = rainParticles.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(32f, 1f, 38f);
            go.transform.localRotation = Quaternion.Euler(96f, 0f, 0f);

            var rend = go.GetComponent<ParticleSystemRenderer>();
            rend.renderMode = ParticleSystemRenderMode.Stretch;
            rend.lengthScale = 12.0f;
            rend.velocityScale = 0.03f;
            var sh = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default");
            var rMat = new Material(sh);
            rMat.SetFloat("_Surface", 1f); // Transparent blending in URP Particles/Unlit
            rMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            rMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            rMat.SetInt("_ZWrite", 0);
            rMat.renderQueue = 3000;
            rMat.SetColor("_BaseColor", new Color(0.84f, 0.92f, 1f, 0.36f));
            rend.sharedMaterial = rMat;

            rainParticles.Play();
        }

        private void Update()
        {
            var loop = StreetGameLoop.Instance;
            bool running = loop != null && loop.State == StreetGameLoop.RunState.Running;

            if (running)
            {
                if (WasRainTogglePressed())
                {
                    SetRaining(!IsRaining, showToast: true);
                }

                // Periodic natural Kenyan highland rain showers every ~2 minutes!
                autoCycleTimer += Time.deltaTime;
                if (autoCycleTimer >= nextWeatherChangeSec)
                {
                    SetRaining(!IsRaining, showToast: true);
                }
            }

            float targetRain = IsRaining ? 1f : 0f;
            RainIntensity = Mathf.MoveTowards(RainIntensity, targetRain, Time.deltaTime * 0.45f);

            // Follow active vehicle smoothly with cloud deck & rain curtain
            Vector3 anchorCenter = Vector3.zero;
            float speedKmh = 0f;
            if (loop != null && loop.ActiveVehicle != null)
            {
                anchorCenter = loop.ActiveVehicle.transform.position;
                speedKmh = loop.ActiveVehicle.SpeedKmh;
            }
            else if (Camera.main != null)
            {
                anchorCenter = Camera.main.transform.position;
            }

            // Drift 3D clouds smoothly overhead + darken them into dramatic Kenyan rain clouds when raining!
            if (cloudLayerRoot != null)
            {
                cloudLayerRoot.position = new Vector3(anchorCenter.x, 0f, anchorCenter.z);
                for (int i = 0; i < cloudClusters.Count; i++)
                {
                    var c = cloudClusters[i];
                    Vector3 lp = c.localPosition;
                    lp.x += (3.8f + (i % 3) * 1.4f) * Time.deltaTime;
                    if (lp.x > 660f) lp.x -= 1320f;
                    // Lower & thicken clouds slightly as rain builds
                    lp.y = Mathf.Lerp(118f + (i % 5) * 12f, 78f + (i % 5) * 8f, RainIntensity);
                    c.localPosition = lp;
                }
                if (cloudMat != null)
                {
                    Color clearCloud = new Color(0.96f, 0.97f, 1.0f, 1f);
                    Color stormCloud = new Color(0.32f, 0.36f, 0.43f, 1f);
                    cloudMat.SetColor("_BaseColor", Color.Lerp(clearCloud, stormCloud, RainIntensity));
                }
            }

            // Rain particle emitter anchored ahead of camera + wind tilt with driving speed
            if (rainParticles != null && rainAnchor != null)
            {
                Vector3 fwd = Camera.main != null ? Camera.main.transform.forward : Vector3.forward;
                fwd.y = 0f;
                if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
                fwd.Normalize();
                rainAnchor.position = anchorCenter + fwd * 12f + Vector3.up * 19f;
                float windTilt = Mathf.Clamp(speedKmh * 0.25f, 0f, 22f);
                rainAnchor.rotation = Quaternion.LookRotation(fwd, Vector3.up) * Quaternion.Euler(96f + windTilt, 0f, 0f);

                var em = rainParticles.emission;
                em.rateOverTime = RainIntensity * 1450f;
            }

            // Dynamic wet reflective tarmac sheen & gentle rain darkening during Kenyan showers!
            if (highwayTarmacMaterial != null)
            {
                if (highwayTarmacMaterial.HasProperty("_Smoothness"))
                {
                    highwayTarmacMaterial.SetFloat("_Smoothness", Mathf.Lerp(dryTarmacSmooth, 0.78f, RainIntensity));
                }
                if (highwayTarmacMaterial.HasProperty("_BaseColor"))
                {
                    Color wetCol = dryTarmacColor * 0.68f;
                    wetCol.a = 1f;
                    highwayTarmacMaterial.SetColor("_BaseColor", Color.Lerp(dryTarmacColor, wetCol, RainIntensity));
                }
            }

            // Gently dim sunlight & thicken distant rain mist when raining (without washing out clarity!)
            if (sunLight != null && KenyaTimeOfDay.Instance != null && KenyaTimeOfDay.Instance.CurrentPreset == KenyaTimeOfDay.TimePreset.Day)
            {
                sunLight.intensity = Mathf.Lerp(1.55f, 0.88f, RainIntensity);
            }
            if (KenyaTimeOfDay.Instance != null && KenyaTimeOfDay.Instance.CurrentPreset == KenyaTimeOfDay.TimePreset.Day)
            {
                RenderSettings.fogDensity = Mathf.Lerp(0.00024f, 0.00145f, RainIntensity);
            }

            KenyaStreetAudio.Instance?.SetRainIntensity(RainIntensity);
        }

        private static bool WasRainTogglePressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.R);
#endif
        }
    }
}
