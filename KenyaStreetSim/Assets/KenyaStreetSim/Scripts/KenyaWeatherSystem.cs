using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace KenyaStreetSim
{
    /// <summary>
    /// Photorealistic Kenyan Highland Weather System:
    /// - Soft feathered volumetric painted cloud layers (NO blocky spheres!) + dynamic overcast skybox darkening
    /// - Genuine soft translucent slender raindrop streaks (proper URP transparency + feathered teardrop texture)
    /// - Subtle wet tarmac ground splash mist & glossy wet road sheen during rain showers
    /// - Periodic automatic Kenyan highland rain showers + instant manual toggle ([R] key)
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
        private ParticleSystem groundSplashParticles;
        private Transform rainAnchor;
        private Transform splashAnchor;
        private Transform cloudLayerRoot;
        private Material cloudMat;
        private Material rainMat;
        private Material splashMat;
        private Texture2D softCloudTex;
        private Texture2D rainStreakTex;
        private Texture2D splashRingTex;
        private readonly List<Transform> cloudLayers = new List<Transform>();
        private float autoCycleTimer;
        private float nextWeatherChangeSec = 110f;
        private Color dryTarmacColor = new Color(0.533f, 0.549f, 0.580f, 1f);
        private float dryTarmacSmooth = 0.24f;
        private Color clearFogColor = new Color(0.76f, 0.84f, 0.92f, 1f);

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            if (highwayTarmacMaterial != null)
            {
                if (highwayTarmacMaterial.HasProperty("_BaseColor")) dryTarmacColor = highwayTarmacMaterial.GetColor("_BaseColor");
                if (highwayTarmacMaterial.HasProperty("_Smoothness")) dryTarmacSmooth = highwayTarmacMaterial.GetFloat("_Smoothness");
            }
            clearFogColor = RenderSettings.fogColor;

            softCloudTex = GenerateSoftPaintedCloudTexture(256);
            rainStreakTex = GenerateFeatheredRainStreakTexture(32, 128);
            splashRingTex = GenerateSoftSplashTexture(64);

            CreateVolumetricCloudDeck();
            CreateRealRainParticles();
            CreateGroundSplashParticles();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (highwayTarmacMaterial != null)
            {
                if (highwayTarmacMaterial.HasProperty("_BaseColor")) highwayTarmacMaterial.SetColor("_BaseColor", dryTarmacColor);
                if (highwayTarmacMaterial.HasProperty("_Smoothness")) highwayTarmacMaterial.SetFloat("_Smoothness", dryTarmacSmooth);
            }
            if (cloudMat != null) Destroy(cloudMat);
            if (rainMat != null) Destroy(rainMat);
            if (splashMat != null) Destroy(splashMat);
            if (softCloudTex != null) Destroy(softCloudTex);
            if (rainStreakTex != null) Destroy(rainStreakTex);
            if (splashRingTex != null) Destroy(splashRingTex);
        }

        public void SetRaining(bool raining, bool showToast = true)
        {
            IsRaining = raining;
            autoCycleTimer = 0f;
            nextWeatherChangeSec = raining ? 55f : 120f;

            if (showToast && StreetGameLoop.Instance != null && StreetGameLoop.Instance.hud != null)
            {
                StreetGameLoop.Instance.hud.ShowToast(raining
                    ? "WEATHER: KENYAN HIGHLAND RAIN [R]  •  WIPERS AUTO-ON"
                    : "WEATHER: CLEAR KENYAN SUNSHINE [R]");
            }
        }

        private static Material CreateTransparentUrpParticleMat(Texture2D mainTex, Color baseColor)
        {
            var sh = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default");
            var m = new Material(sh);
            m.SetTexture("_BaseMap", mainTex);
            m.mainTexture = mainTex;
            m.SetColor("_BaseColor", baseColor);
            // Properly configure URP Particle Unlit Transparent Surface + Keywords so alpha feathers smoothly!
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_AlphaClip", 0f);
            m.SetFloat("_ColorMode", 0f);
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.SetFloat("_Cull", 0f); // Double-sided so overhead clouds render cleanly from below!
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            m.DisableKeyword("_ALPHATEST_ON");
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            return m;
        }

        private void CreateVolumetricCloudDeck()
        {
            cloudMat = CreateTransparentUrpParticleMat(softCloudTex, new Color(0.96f, 0.98f, 1.0f, 0.22f));

            cloudLayerRoot = new GameObject("KenyaPaintedCloudDeck").transform;
            cloudLayerRoot.SetParent(transform, false);

            // 32 wide, soft-feathered atmospheric cloud sheets drifting high overhead (ZERO blocky spheres!)
            var rng = new System.Random(2541);
            for (int i = 0; i < 32; i++)
            {
                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quad.name = $"CloudSheet_{i}";
                Destroy(quad.GetComponent<Collider>());
                quad.transform.SetParent(cloudLayerRoot, false);

                float angle = (i / 32f) * Mathf.PI * 2f + (float)rng.NextDouble() * 0.45f;
                float dist = 70f + (float)rng.NextDouble() * 620f;
                float height = 145f + (float)rng.NextDouble() * 75f;
                quad.transform.localPosition = new Vector3(Mathf.Cos(angle) * dist, height, Mathf.Sin(angle) * dist);
                // Face downward toward the earth with gentle random sky rotation
                quad.transform.localRotation = Quaternion.Euler(90f, (float)rng.NextDouble() * 360f, 0f);
                float sx = 140f + (float)rng.NextDouble() * 180f;
                float sy = 110f + (float)rng.NextDouble() * 140f;
                quad.transform.localScale = new Vector3(sx, sy, 1f);

                var r = quad.GetComponent<Renderer>();
                r.sharedMaterial = cloudMat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                cloudLayers.Add(quad.transform);
            }
        }

        private void CreateRealRainParticles()
        {
            var go = new GameObject("KenyaRealRainCurtain");
            go.transform.SetParent(transform, false);
            rainAnchor = go.transform;

            rainParticles = go.AddComponent<ParticleSystem>();
            var main = rainParticles.main;
            main.loop = true;
            main.startLifetime = 0.65f;
            main.startSpeed = 36f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.018f, 0.032f);
            main.maxParticles = 2600;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = new Color(0.86f, 0.93f, 1.0f, 0.24f);

            var emission = rainParticles.emission;
            emission.rateOverTime = 0f;

            var shape = rainParticles.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(36f, 1f, 42f);
            go.transform.localRotation = Quaternion.Euler(96f, 0f, 0f);

            var rend = go.GetComponent<ParticleSystemRenderer>();
            rend.renderMode = ParticleSystemRenderMode.Stretch;
            rend.lengthScale = 16f;
            rend.velocityScale = 0.025f;
            rainMat = CreateTransparentUrpParticleMat(rainStreakTex, new Color(0.88f, 0.94f, 1.0f, 0.26f));
            rend.sharedMaterial = rainMat;

            rainParticles.Play();
        }

        private void CreateGroundSplashParticles()
        {
            var go = new GameObject("KenyaRainGroundSplashes");
            go.transform.SetParent(transform, false);
            splashAnchor = go.transform;

            groundSplashParticles = go.AddComponent<ParticleSystem>();
            var main = groundSplashParticles.main;
            main.loop = true;
            main.startLifetime = 0.24f;
            main.startSpeed = 0.35f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.14f, 0.28f);
            main.maxParticles = 450;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = new Color(0.85f, 0.92f, 1.0f, 0.18f);

            var emission = groundSplashParticles.emission;
            emission.rateOverTime = 0f;

            var shape = groundSplashParticles.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(26f, 0.1f, 34f);

            var colOverLife = groundSplashParticles.colorOverLifetime;
            colOverLife.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0.0f, 0f), new GradientAlphaKey(0.65f, 0.25f), new GradientAlphaKey(0.0f, 1f) });
            colOverLife.color = grad;

            var rend = go.GetComponent<ParticleSystemRenderer>();
            rend.renderMode = ParticleSystemRenderMode.Billboard;
            splashMat = CreateTransparentUrpParticleMat(splashRingTex, new Color(0.86f, 0.93f, 1.0f, 0.22f));
            rend.sharedMaterial = splashMat;

            groundSplashParticles.Play();
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

                // Periodic natural Kenyan highland rain showers every ~2 minutes
                autoCycleTimer += Time.deltaTime;
                if (autoCycleTimer >= nextWeatherChangeSec)
                {
                    SetRaining(!IsRaining, showToast: true);
                }
            }

            float targetRain = IsRaining ? 1f : 0f;
            RainIntensity = Mathf.MoveTowards(RainIntensity, targetRain, Time.deltaTime * 0.45f);

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

            // Drift soft painted high-altitude cloud sheets & shift into real overcast storm clouds when raining!
            if (cloudLayerRoot != null)
            {
                cloudLayerRoot.position = new Vector3(anchorCenter.x, 0f, anchorCenter.z);
                for (int i = 0; i < cloudLayers.Count; i++)
                {
                    var c = cloudLayers[i];
                    Vector3 lp = c.localPosition;
                    lp.x += (4.5f + (i % 3) * 1.6f) * Time.deltaTime;
                    if (lp.x > 680f) lp.x -= 1360f;
                    c.localPosition = lp;
                }
                if (cloudMat != null)
                {
                    // Nearly wispy subtle white in sunshine -> lush moody storm grey overcast in rain!
                    Color clearCloud = new Color(0.98f, 0.99f, 1.0f, 0.14f);
                    Color stormCloud = new Color(0.36f, 0.41f, 0.48f, 0.62f);
                    cloudMat.SetColor("_BaseColor", Color.Lerp(clearCloud, stormCloud, RainIntensity));
                }
            }

            Vector3 fwd = Camera.main != null ? Camera.main.transform.forward : Vector3.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
            fwd.Normalize();

            // Slender translucent rain streaks with realistic speed wind-slant
            if (rainParticles != null && rainAnchor != null)
            {
                rainAnchor.position = anchorCenter + fwd * 14f + Vector3.up * 21f;
                float windTilt = Mathf.Clamp(speedKmh * 0.28f, 0f, 24f);
                rainAnchor.rotation = Quaternion.LookRotation(fwd, Vector3.up) * Quaternion.Euler(96f + windTilt, 0f, 0f);

                var em = rainParticles.emission;
                em.rateOverTime = RainIntensity * 1950f;
            }

            // Subtle wet tarmac raindrop splashes around the vehicle
            if (groundSplashParticles != null && splashAnchor != null)
            {
                splashAnchor.position = new Vector3(anchorCenter.x + fwd.x * 11f, 0.08f, anchorCenter.z + fwd.z * 11f);
                var em = groundSplashParticles.emission;
                em.rateOverTime = RainIntensity * 420f;
            }

            // Dynamic glossy wet highway tarmac sheen during rain
            if (highwayTarmacMaterial != null)
            {
                if (highwayTarmacMaterial.HasProperty("_Smoothness"))
                {
                    highwayTarmacMaterial.SetFloat("_Smoothness", Mathf.Lerp(dryTarmacSmooth, 0.82f, RainIntensity));
                }
                if (highwayTarmacMaterial.HasProperty("_BaseColor"))
                {
                    Color wetCol = dryTarmacColor * 0.64f;
                    wetCol.a = 1f;
                    highwayTarmacMaterial.SetColor("_BaseColor", Color.Lerp(dryTarmacColor, wetCol, RainIntensity));
                }
            }

            // Realistic overcast skybox exposure & atmospheric rain mist during Kenyan highland showers
            if (KenyaTimeOfDay.Instance != null && KenyaTimeOfDay.Instance.CurrentPreset == KenyaTimeOfDay.TimePreset.Day)
            {
                if (sunLight != null) sunLight.intensity = Mathf.Lerp(1.55f, 0.78f, RainIntensity);
                if (RenderSettings.skybox != null && RenderSettings.skybox.HasProperty("_Exposure"))
                {
                    RenderSettings.skybox.SetFloat("_Exposure", Mathf.Lerp(0.78f, 0.44f, RainIntensity));
                }
                RenderSettings.fogDensity = Mathf.Lerp(0.00024f, 0.00175f, RainIntensity);
                RenderSettings.fogColor = Color.Lerp(clearFogColor, new Color(0.50f, 0.56f, 0.63f, 1f), RainIntensity);
            }

            KenyaStreetAudio.Instance?.SetRainIntensity(RainIntensity);
        }

        private static Texture2D GenerateSoftPaintedCloudTexture(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            float inv = 1f / size;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = x * inv * 2f - 1f;
                    float v = y * inv * 2f - 1f;
                    float radial = Mathf.Clamp01(1f - Mathf.Sqrt(u * u + v * v));
                    // Multi-octave organic fBM puffiness with ultra-soft feathered edge (ZERO hard borders!)
                    float n = Mathf.PerlinNoise(x * inv * 3.6f + 11.3f, y * inv * 3.6f + 7.1f) * 0.55f
                            + Mathf.PerlinNoise(x * inv * 7.8f + 23.7f, y * inv * 7.8f + 19.4f) * 0.30f
                            + Mathf.PerlinNoise(x * inv * 15.5f + 4.1f, y * inv * 15.5f + 31.2f) * 0.15f;
                    float alpha = Mathf.Clamp01(Mathf.Pow(radial, 1.55f) * Mathf.SmoothStep(0.24f, 0.86f, n));
                    float shade = Mathf.Lerp(0.78f, 1.0f, n);
                    tex.SetPixel(x, y, new Color(shade, shade, shade, alpha));
                }
            }
            tex.Apply();
            return tex;
        }

        private static Texture2D GenerateFeatheredRainStreakTexture(int w, int h)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            for (int y = 0; y < h; y++)
            {
                float ny = (float)y / (h - 1);
                // Tapered teardrop profile: fades softly at top & bottom, brightest slightly toward leading drop tip
                float yEnv = Mathf.Sin(ny * Mathf.PI) * Mathf.Lerp(0.35f, 1.0f, 1f - ny);
                for (int x = 0; x < w; x++)
                {
                    float nx = Mathf.Abs(((float)x / (w - 1)) * 2f - 1f);
                    float xEnv = Mathf.Pow(Mathf.Clamp01(1f - nx), 2.4f);
                    float a = Mathf.Clamp01(xEnv * yEnv);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            tex.Apply();
            return tex;
        }

        private static Texture2D GenerateSoftSplashTexture(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            float half = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - half) / half;
                    float dy = (y - half) / half;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float puff = Mathf.Pow(Mathf.Clamp01(1f - d), 1.9f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, puff));
                }
            }
            tex.Apply();
            return tex;
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
