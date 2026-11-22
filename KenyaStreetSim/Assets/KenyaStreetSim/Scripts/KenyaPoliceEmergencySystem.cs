using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace KenyaStreetSim
{
    /// <summary>
    /// Working Kenya Police Land Cruiser 79 Emergency Roof Lightbar + Wailing Siren Controller.
    /// Toggle anytime with [G] key (or automatically enabled in Kenya Police Patrol mode).
    /// Strobes alternating high-intensity Red & Blue LED banks and real URP PointLights.
    /// </summary>
    public class KenyaPoliceEmergencySystem : MonoBehaviour
    {
        public Renderer[] redBarRenderers;
        public Renderer[] blueBarRenderers;
        public Light redStrobeLight;
        public Light blueStrobeLight;

        public bool SirenActive { get; private set; }

        private float strobeTimer;
        private MaterialPropertyBlock mpb;
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        private void Awake()
        {
            mpb = new MaterialPropertyBlock();
            ApplyStrobeVisuals(false, false);
        }

        private void OnDisable()
        {
            SetSirenActive(false, silent: true);
        }

        public void SetSirenActive(bool active, bool silent = false)
        {
            SirenActive = active;
            if (!active)
            {
                ApplyStrobeVisuals(false, false);
                KenyaStreetAudio.Instance?.SetPoliceSiren(false);
            }
            else
            {
                KenyaStreetAudio.Instance?.SetPoliceSiren(true);
            }
            if (!silent && StreetGameLoop.Instance != null && StreetGameLoop.Instance.hud != null)
            {
                StreetGameLoop.Instance.hud.ShowToast(active ? "KENYA POLICE SIREN & LIGHTBAR: ON [G]" : "KENYA POLICE SIREN: OFF [G]");
            }
        }

        private void Update()
        {
            var loop = StreetGameLoop.Instance;
            bool playing = loop != null && loop.State == StreetGameLoop.RunState.Running;

            if (!playing)
            {
                if (SirenActive)
                {
                    KenyaStreetAudio.Instance?.SetPoliceSiren(false);
                }
                // Still show gentle show-floor lightbar flash on Title Screen if previewing the Land Cruiser!
                if (loop != null && loop.State == StreetGameLoop.RunState.Ready)
                {
                    strobeTimer += Time.unscaledDeltaTime * 7.5f;
                    bool phaseA = ((int)strobeTimer % 2) == 0;
                    ApplyStrobeVisuals(phaseA, !phaseA);
                }
                else
                {
                    ApplyStrobeVisuals(false, false);
                }
                return;
            }

            if (loop.routeMap == null || !loop.routeMap.IsFullMapOpen)
            {
                if (WasSirenKeyPressed())
                {
                    SetSirenActive(!SirenActive);
                }
            }

            if (SirenActive)
            {
                KenyaStreetAudio.Instance?.SetPoliceSiren(true);
                strobeTimer += Time.deltaTime * 11f;
                int step = ((int)strobeTimer) % 4;
                bool redOn = (step == 0 || step == 1);
                bool blueOn = (step == 2 || step == 3);
                ApplyStrobeVisuals(redOn, blueOn);
            }
        }

        private void ApplyStrobeVisuals(bool redOn, bool blueOn)
        {
            Color redGlow = redOn ? new Color(1.0f, 0.08f, 0.08f) * 5.2f : new Color(0.28f, 0.02f, 0.02f);
            Color blueGlow = blueOn ? new Color(0.08f, 0.36f, 1.0f) * 5.5f : new Color(0.02f, 0.06f, 0.28f);

            ApplyEmission(redBarRenderers, redGlow);
            ApplyEmission(blueBarRenderers, blueGlow);

            bool inCockpit = StreetGameLoop.Instance != null &&
                             StreetGameLoop.Instance.streetCamera != null &&
                             StreetGameLoop.Instance.streetCamera.CurrentMode == KenyaStreetCamera.CameraMode.CockpitRHD;
            float lightPower = inCockpit ? 2.2f : 16f;

            if (redStrobeLight != null)
            {
                redStrobeLight.enabled = redOn;
                redStrobeLight.intensity = redOn ? lightPower : 0f;
            }
            if (blueStrobeLight != null)
            {
                blueStrobeLight.enabled = blueOn;
                blueStrobeLight.intensity = blueOn ? lightPower : 0f;
            }
        }

        private void ApplyEmission(Renderer[] renderers, Color emissive)
        {
            if (renderers == null || mpb == null) return;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                renderers[i].GetPropertyBlock(mpb);
                mpb.SetColor(EmissionColorId, emissive);
                renderers[i].SetPropertyBlock(mpb);
            }
        }

        private static bool WasSirenKeyPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.gKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.G);
#endif
        }
    }
}
