using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace KenyaStreetSim
{
    /// <summary>
    /// ETS2 / RK 254 KE style Time-of-Day atmosphere controller (Press T to cycle):
    /// Kenyan Mid-Afternoon -> Golden Nairobi Sunset -> Night Highway (activates street lamps & petrol canopy lights).
    /// </summary>
    public class KenyaTimeOfDay : MonoBehaviour
    {
        public enum TimePreset { Day, Sunset, Night }

        public static KenyaTimeOfDay Instance { get; private set; }

        [Header("Scene Lighting References")]
        public Light sunLight;
        public Material skyboxMaterial;
        public Texture2D daySkyPanoramic;
        public Texture2D sunsetSkyPanoramic;
        public Texture2D nightSkyPanoramic;
        public Light[] streetPointLights;
        public Renderer[] lampGlowRenderers;

        public TimePreset CurrentPreset { get; private set; } = TimePreset.Day;

        private MaterialPropertyBlock lampMpb;
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            lampMpb = new MaterialPropertyBlock();
        }

        private void Start()
        {
            ApplyPreset(TimePreset.Day);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (StreetGameLoop.Instance == null || StreetGameLoop.Instance.State != StreetGameLoop.RunState.Running)
                return;

            bool pressed = false;
#if ENABLE_INPUT_SYSTEM
            pressed = Keyboard.current != null && Keyboard.current.tKey.wasPressedThisFrame;
#else
            pressed = Input.GetKeyDown(KeyCode.T);
#endif
            if (pressed)
            {
                CyclePreset();
            }
        }

        public void CyclePreset()
        {
            var next = (TimePreset)(((int)CurrentPreset + 1) % 3);
            ApplyPreset(next);
            if (KenyaStreetAudio.Instance != null)
            {
                KenyaStreetAudio.Instance.PlayUiClick();
            }
            if (StreetGameLoop.Instance != null && StreetGameLoop.Instance.hud != null)
            {
                string label = next == TimePreset.Day ? "Kenyan Afternoon" : (next == TimePreset.Sunset ? "Golden Nairobi Sunset" : "Nairobi Night Drive");
                StreetGameLoop.Instance.hud.ShowToast($"Time of Day: {label}");
            }
        }

        public void ApplyPreset(TimePreset preset)
        {
            CurrentPreset = preset;

            if (sunLight == null)
            {
                sunLight = RenderSettings.sun;
            }

            bool lampsOn = preset != TimePreset.Day;
            float lampIntensity = preset == TimePreset.Night ? 62f : (preset == TimePreset.Sunset ? 24f : 0f);
            Color lampEmission = preset == TimePreset.Night
                ? Palette.LampGlow * 4.5f
                : (preset == TimePreset.Sunset ? Palette.LampGlow * 2.4f : Palette.LampGlow * 0.4f);

            switch (preset)
            {
                case TimePreset.Day:
                    if (sunLight != null)
                    {
                        sunLight.color = Palette.SunKey;
                        sunLight.intensity = 1.55f;
                        sunLight.transform.rotation = Quaternion.Euler(42f, -34f, 0f);
                    }
                    RenderSettings.ambientSkyColor = Palette.AmbientSky;
                    RenderSettings.ambientEquatorColor = Palette.AmbientEquator;
                    RenderSettings.ambientGroundColor = Palette.AmbientGround;
                    RenderSettings.fogColor = new Color(0.72f, 0.82f, 0.93f, 1f);
                    RenderSettings.fogDensity = 0.00024f;
                    SetSkyParams(Palette.SkyTint, Palette.SkyGround, 0.72f, 0.78f, daySkyPanoramic, Color.white);
                    break;

                case TimePreset.Sunset:
                    if (sunLight != null)
                    {
                        sunLight.color = Palette.Hex("#FF9C54");
                        sunLight.intensity = 1.22f;
                        sunLight.transform.rotation = Quaternion.Euler(14f, -68f, 0f);
                    }
                    RenderSettings.ambientSkyColor = Palette.Hex("#5B5C82");
                    RenderSettings.ambientEquatorColor = Palette.Hex("#CC7D52");
                    RenderSettings.ambientGroundColor = Palette.Hex("#36261E");
                    RenderSettings.fogColor = Palette.Hex("#C28668");
                    RenderSettings.fogDensity = 0.00060f;
                    SetSkyParams(Palette.Hex("#B8674A"), Palette.Hex("#3B2B22"), 1.45f, 0.42f, sunsetSkyPanoramic, Color.white);
                    break;

                case TimePreset.Night:
                    if (sunLight != null)
                    {
                        sunLight.color = Palette.Hex("#6B8BC2");
                        sunLight.intensity = 0.28f;
                        sunLight.transform.rotation = Quaternion.Euler(52f, 24f, 0f);
                    }
                    RenderSettings.ambientSkyColor = Palette.Hex("#1A243B");
                    RenderSettings.ambientEquatorColor = Palette.Hex("#1E2838");
                    RenderSettings.ambientGroundColor = Palette.Hex("#10141C");
                    RenderSettings.fogColor = Palette.Hex("#141B29");
                    RenderSettings.fogDensity = 0.00105f;
                    SetSkyParams(Palette.Hex("#0B1120"), Palette.Hex("#0D121C"), 0.28f, 0.55f, nightSkyPanoramic, new Color(0.85f, 0.90f, 1f, 1f));
                    break;
            }

            if (streetPointLights != null)
            {
                for (int i = 0; i < streetPointLights.Length; i++)
                {
                    if (streetPointLights[i] == null) continue;
                    streetPointLights[i].enabled = lampsOn;
                    streetPointLights[i].intensity = lampIntensity;
                }
            }

            if (lampGlowRenderers != null)
            {
                for (int i = 0; i < lampGlowRenderers.Length; i++)
                {
                    var r = lampGlowRenderers[i];
                    if (r == null) continue;
                    r.GetPropertyBlock(lampMpb);
                    lampMpb.SetColor(EmissionColorId, lampEmission);
                    r.SetPropertyBlock(lampMpb);
                }
            }

            // Auto-toggle player headlights on at Sunset/Night for instant RK 254 KE night-drive realism
            if (StreetGameLoop.Instance != null && StreetGameLoop.Instance.ActiveVehicle != null)
            {
                StreetGameLoop.Instance.ActiveVehicle.SetHeadlights(preset != TimePreset.Day);
            }
        }

        private void SetSkyParams(Color tint, Color ground, float thickness, float exposure, Texture2D panoramicTex = null, Color? panoTint = null)
        {
            var mat = skyboxMaterial != null ? skyboxMaterial : RenderSettings.skybox;
            if (mat == null) return;
            if (mat.HasProperty("_MainTex") && panoramicTex != null)
            {
                mat.SetTexture("_MainTex", panoramicTex);
                if (mat.HasProperty("_Tint")) mat.SetColor("_Tint", panoTint ?? Color.white);
                if (mat.HasProperty("_Exposure")) mat.SetFloat("_Exposure", exposure);
                DynamicGI.UpdateEnvironment();
                return;
            }
            if (mat.HasProperty("_SkyTint")) mat.SetColor("_SkyTint", tint);
            if (mat.HasProperty("_GroundColor")) mat.SetColor("_GroundColor", ground);
            if (mat.HasProperty("_AtmosphereThickness")) mat.SetFloat("_AtmosphereThickness", thickness);
            if (mat.HasProperty("_Exposure")) mat.SetFloat("_Exposure", exposure);
        }
    }
}
